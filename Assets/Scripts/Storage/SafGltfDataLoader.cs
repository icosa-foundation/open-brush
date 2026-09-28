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
using System.Threading;
using System.Threading.Tasks;
using UnityGLTF.Loader;

namespace TiltBrush
{
    /// Resolves a glTF's external references - .bin buffers, textures - out of shared storage
    /// instead of a materialized copy on disk.
    ///
    /// UnityGLTF's loader contract is stream-based, so the importer never needed a filesystem;
    /// the stock FileLoader is simply the implementation that uses one. Implements IDataLoader2 so
    /// the importer can keep reading the glTF JSON off the main thread.
    public sealed class SafGltfDataLoader : IDataLoader2
    {
        private readonly StorageArea m_Area;
        private readonly string m_Directory;

        /// <param name="area">Storage area the model lives in.</param>
        /// <param name="areaRelativeDirectory">
        /// Directory of the glTF document, relative to that area. External references resolve
        /// against it, exactly as FileLoader resolves against its root directory.
        /// </param>
        public SafGltfDataLoader(StorageArea area, string areaRelativeDirectory)
        {
            m_Area = area;
            m_Directory = Normalize(areaRelativeDirectory);
        }

        public Task<Stream> LoadStreamAsync(string relativeFilePath)
        {
            return Task.FromResult(LoadStream(relativeFilePath));
        }

        public Stream LoadStream(string relativeFilePath)
        {
            if (!TryResolveAreaRelativePath(
                    m_Directory, relativeFilePath, out string path))
            {
                throw new IOException(
                    $"glTF reference escapes the models storage area: {relativeFilePath}");
            }

            // Worker-thread reads are attached to the JVM inside the SAF backend.
            return UserStorage.Backend.OpenRead(
                m_Area, path, requireSeekable: false, CancellationToken.None);
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        internal static bool TryResolveAreaRelativePath(
            string baseDirectory, string relativeFilePath, out string resolvedPath)
        {
            resolvedPath = null;
            if (string.IsNullOrWhiteSpace(relativeFilePath) ||
                relativeFilePath.StartsWith("/", StringComparison.Ordinal) ||
                relativeFilePath.StartsWith("\\", StringComparison.Ordinal) ||
                Path.IsPathRooted(relativeFilePath) ||
                (Uri.TryCreate(relativeFilePath, UriKind.Absolute, out Uri absolute) &&
                    absolute.IsAbsoluteUri))
            {
                return false;
            }

            string decoded;
            try
            {
                // The importer escapes URI references, so a name with spaces arrives encoded.
                decoded = Uri.UnescapeDataString(relativeFilePath).Replace('\\', '/');
            }
            catch (UriFormatException)
            {
                return false;
            }

            var segments = new System.Collections.Generic.List<string>();
            foreach (string segment in Normalize(baseDirectory).Split('/'))
            {
                if (segment.Length > 0 && segment != ".") { segments.Add(segment); }
            }
            foreach (string segment in decoded.Split('/'))
            {
                if (segment.Length == 0 || segment == ".") { continue; }
                if (segment == "..")
                {
                    if (segments.Count == 0) { return false; }
                    segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                segments.Add(segment);
            }
            if (segments.Count == 0) { return false; }
            resolvedPath = string.Join("/", segments);
            return true;
        }
    }
}
