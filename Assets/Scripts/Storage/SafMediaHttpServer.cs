// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;

namespace TiltBrush
{
    /// Serves shared-storage documents to Unity's media loaders over the local HTTP server.
    ///
    /// UnityWebRequestMultimedia.GetAudioClip and VideoPlayer.url accept only a URL - not a stream,
    /// a byte array or a descriptor. That is the entire reason media used to be copied out of
    /// shared storage into app-private storage before playback. Both accept http://, so the copy is
    /// unnecessary: this streams the document straight from the storage backend instead.
    public static class SafMediaHttpServer
    {
        private const string kPath = "/saf-media";
        private const int kCopyBufferBytes = 128 * 1024;

        // Unguessable, regenerated each session. The HTTP server binds http://+:PORT (all interfaces)
        // and will dispatch remote requests to handlers when EnableApiRemoteCalls is set, so the
        // token - not the listener's configuration - is what keeps this from being a file
        // disclosure surface for other applications on the device and for the local network.
        private static string sm_Token;
        private static bool sm_Registered;

        public static bool IsAvailable => sm_Registered && !string.IsNullOrEmpty(sm_Token);

        public static void Register()
        {
            if (sm_Registered || !OpenBrushStorage.IsScopedStorageMode)
            {
                return;
            }
            sm_Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            App.HttpServer.AddRawHttpHandler(kPath, Handle);
            sm_Registered = true;
        }

        /// A URL Unity's media loaders can open, naming a document in shared storage.
        public static string GetUrl(StorageArea area, string relativePath)
        {
            if (!IsAvailable)
            {
                return null;
            }
            // Escaped per segment so the separators survive. Consumers that resolve siblings by
            // joining onto the directory - the OBJ loader locating its .mtl and textures - need a
            // URL whose path has real slashes in it.
            string[] segments = (relativePath ?? string.Empty)
                .Replace('\\', '/').Trim('/').Split('/');
            for (int i = 0; i < segments.Length; ++i)
            {
                segments[i] = Uri.EscapeDataString(segments[i]);
            }
            string escaped = string.Join("/", segments);
            return $"http://127.0.0.1:{HttpServer.HTTP_PORT}{kPath}/{sm_Token}/{(int)area}/{escaped}";
        }

        private static HttpListenerContext Handle(HttpListenerContext ctx)
        {
            try
            {
                // The listener itself admits remote callers when EnableApiRemoteCalls is set.
                // Shared user files are not part of that opt-in, so re-check here.
                if (!HttpServer.IsTrustedLocalBrowserRequest(ctx.Request))
                {
                    Respond(ctx, HttpStatusCode.Forbidden);
                    return ctx;
                }
                // RawUrl, not Url.LocalPath: LocalPath is already decoded, so unescaping the
                // segments below would be a second decode and a name containing a literal
                // percent escape would resolve to the wrong document.
                string rawPath = ctx.Request.RawUrl ?? string.Empty;
                int query = rawPath.IndexOf('?');
                if (query >= 0) { rawPath = rawPath.Substring(0, query); }
                if (!TryParse(rawPath, out StorageArea area, out string relativePath))
                {
                    Respond(ctx, HttpStatusCode.NotFound);
                    return ctx;
                }

                IUserStorageBackend backend = UserStorage.Backend;
                using Stream source = backend.OpenRead(
                    area, relativePath, requireSeekable: true, CancellationToken.None);
                ServeRange(ctx, source);
            }
            catch (Exception e)
            {
                // A media loader polling a missing or unreadable document must get a status code,
                // not an unhandled exception on the listener thread.
                Debug.LogWarning(
                    $"SAF_MEDIA_HTTP {e.GetType().Name}: {e.Message} " +
                    $"(url={ctx.Request.RawUrl})");
                TryRespondFailure(ctx);
            }
            return ctx;
        }

        private static bool TryParse(string localPath, out StorageArea area, out string relativePath)
        {
            area = default;
            relativePath = null;
            if (string.IsNullOrEmpty(localPath) || !localPath.StartsWith(kPath, StringComparison.Ordinal))
            {
                return false;
            }
            // <token>/<area>/<relative path, which may itself contain '/'>
            string remainder = localPath.Substring(kPath.Length).TrimStart('/');
            int firstSlash = remainder.IndexOf('/');
            if (firstSlash <= 0) { return false; }
            string token = remainder.Substring(0, firstSlash);
            if (!FixedTimeEquals(token, sm_Token)) { return false; }

            string rest = remainder.Substring(firstSlash + 1);
            int secondSlash = rest.IndexOf('/');
            if (secondSlash <= 0) { return false; }
            if (!int.TryParse(rest.Substring(0, secondSlash), out int areaValue)) { return false; }
            if (!Enum.IsDefined(typeof(StorageArea), areaValue)) { return false; }
            area = (StorageArea)areaValue;

            string[] rawSegments = rest.Substring(secondSlash + 1).Split('/');
            for (int i = 0; i < rawSegments.Length; ++i)
            {
                rawSegments[i] = Uri.UnescapeDataString(rawSegments[i]);
                // A segment that decodes to a separator would escape the intended directory.
                if (rawSegments[i].Contains("/") || rawSegments[i].Contains("\\"))
                {
                    return false;
                }
            }
            // Trailing or doubled separators produce empty segments, which the backend rejects
            // outright as an unsafe path. Dropping them here keeps a harmless URL shape working
            // and stops a stray slash reading as a traversal attempt.
            relativePath = string.Join(
                "/", Array.FindAll(rawSegments, segment => segment.Length > 0));
            // The backend rejects escaping paths, but refuse the obvious shapes before the round
            // trip and never let a caller name an absolute path.
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Array.Exists(rawSegments, segment => segment == "." || segment == "..") ||
                Path.IsPathRooted(relativePath))
            {
                relativePath = null;
                return false;
            }
            return true;
        }

        private static void ServeRange(HttpListenerContext ctx, Stream source)
        {
            long total = source.Length;
            long start = 0;
            long end = total - 1;
            bool partial = false;

            string range = ctx.Request.Headers["Range"];
            if (!string.IsNullOrEmpty(range) && range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                string spec = range.Substring("bytes=".Length).Split(',')[0].Trim();
                int dash = spec.IndexOf('-');
                if (dash >= 0)
                {
                    string from = spec.Substring(0, dash);
                    string to = spec.Substring(dash + 1);
                    if (from.Length == 0 && long.TryParse(to, out long suffix) && suffix > 0)
                    {
                        start = Math.Max(0, total - suffix);
                        partial = true;
                    }
                    else if (long.TryParse(from, out long fromValue))
                    {
                        start = fromValue;
                        if (to.Length > 0 && long.TryParse(to, out long toValue))
                        {
                            end = Math.Min(end, toValue);
                        }
                        partial = true;
                    }
                }
            }

            if (start > end || start >= total)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                ctx.Response.Headers["Content-Range"] = $"bytes */{total}";
                ctx.Response.Close();
                return;
            }

            long length = end - start + 1;
            ctx.Response.StatusCode = (int)(partial
                ? HttpStatusCode.PartialContent
                : HttpStatusCode.OK);
            ctx.Response.ContentType = "application/octet-stream";
            ctx.Response.ContentLength64 = length;
            ctx.Response.Headers["Accept-Ranges"] = "bytes";
            if (partial)
            {
                ctx.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{total}";
            }

            source.Seek(start, SeekOrigin.Begin);
            byte[] buffer = new byte[kCopyBufferBytes];
            long remaining = length;
            using Stream output = ctx.Response.OutputStream;
            while (remaining > 0)
            {
                int want = (int)Math.Min(buffer.Length, remaining);
                int read = source.Read(buffer, 0, want);
                if (read <= 0) { break; }
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private static void Respond(HttpListenerContext ctx, HttpStatusCode code)
        {
            ctx.Response.StatusCode = (int)code;
            ctx.Response.ContentLength64 = 0;
            ctx.Response.Close();
        }

        private static void TryRespondFailure(HttpListenerContext ctx)
        {
            try
            {
                Respond(ctx, HttpStatusCode.NotFound);
            }
            catch (Exception)
            {
                // The response may already be partly written, in which case there is nothing to say.
            }
        }

        /// Constant-time comparison, so a caller cannot recover the token by timing prefixes.
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }
            int difference = 0;
            for (int i = 0; i < a.Length; ++i)
            {
                difference |= a[i] ^ b[i];
            }
            return difference == 0;
        }
    }
}
