using System.Collections.Generic;
using System;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Networking;
namespace TiltBrush
{
    [LuaDocsDescription("Functions to call remote websites or APIs. Flags.PluginWebRequestRules restrict exact hosts by HTTP method and response file type. Set Flags.EnablePluginWebRequests to true to permit unrestricted HTTP(S) access")]
    [MoonSharpUserData]
    public static class WebRequestApiWrapper
    {
        internal static bool AllowsLuaNetworkRedirects =>
            App.UserConfig.Flags.EnablePluginWebRequests;

        internal static string[] EnsureLuaNetworkAccessEnabled(string url, string method)
        {
            if (!TryGetAllowedFileTypes(
                url,
                method,
                App.UserConfig.Flags.EnablePluginWebRequests,
                App.UserConfig.Flags.PluginWebRequestRules,
                out string[] allowedFileTypes))
            {
                throw new UnauthorizedAccessException(
                    $"Lua {method.ToUpperInvariant()} access to '{url}' is not permitted by the Lua network allowlist.");
            }
            return allowedFileTypes;
        }

        internal static void EnsureLuaNetworkAccessForLocation(
            string location, string requiredFileType)
        {
            if (ApiMethods.IsHttpLocation(location))
            {
                string[] allowedFileTypes = EnsureLuaNetworkAccessEnabled(location, "GET");
                if (!AllowsFileType(allowedFileTypes, requiredFileType))
                {
                    throw new UnauthorizedAccessException(
                        $"Lua network access to '{location}' is not permitted for {requiredFileType} files.");
                }
            }
        }

        private static bool TryGetAllowedFileTypes(
            string url,
            string method,
            bool unrestrictedAccess,
            UserConfig.PluginWebRequestRule[] allowedHostRules,
            out string[] allowedFileTypes)
        {
            allowedFileTypes = null;
            if (!TryGetHttpUri(url, out Uri uri)) return false;
            if (unrestrictedAccess) return true;

            string requestHost = uri.IdnHost.TrimEnd('.');
            if (allowedHostRules == null) return false;
            allowedFileTypes = allowedHostRules
                .Where(rule =>
                    HostMatches(requestHost, rule.Host) &&
                    rule.Methods != null &&
                    rule.Methods.Any(allowedMethod =>
                        method.Equals(allowedMethod, StringComparison.OrdinalIgnoreCase)))
                .Where(rule => rule.FileTypes != null)
                .SelectMany(rule => rule.FileTypes)
                .Where(fileType => !string.IsNullOrWhiteSpace(fileType))
                .Select(fileType => fileType.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray();
            return allowedFileTypes.Length > 0;
        }

        private static bool AllowsFileType(
            string[] allowedFileTypes, string requiredFileType)
        {
            return allowedFileTypes == null ||
                allowedFileTypes.Contains("any") ||
                allowedFileTypes.Contains(requiredFileType.ToLowerInvariant());
        }

        private static bool TryGetHttpUri(string url, out Uri uri)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static bool HostMatches(string requestHost, string allowedHost)
        {
            return !string.IsNullOrWhiteSpace(allowedHost) &&
                requestHost.Equals(
                    allowedHost.Trim().TrimEnd('.'),
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsLuaNetworkAccessAllowed(
            string url,
            string method,
            UserConfig.PluginWebRequestRule[] allowedHostRules,
            string requiredFileType)
        {
            if (!TryGetAllowedFileTypes(
                url,
                method,
                unrestrictedAccess: false,
                allowedHostRules: allowedHostRules,
                allowedFileTypes: out string[] allowedFileTypes))
            {
                return false;
            }
            return AllowsFileType(allowedFileTypes, requiredFileType);
        }

        internal static bool IsContentTypeAllowed(
            string contentType, string[] allowedFileTypes)
        {
            if (allowedFileTypes == null || allowedFileTypes.Contains("any")) return true;
            if (string.IsNullOrWhiteSpace(contentType)) return false;

            string mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();
            return allowedFileTypes.Any(fileType => fileType switch
            {
                "json" => mediaType == "application/json" ||
                    mediaType == "text/json" || mediaType.EndsWith("+json"),
                "text" => mediaType.StartsWith("text/") &&
                    mediaType != "text/xml" && !mediaType.EndsWith("+xml"),
                "xml" => mediaType == "application/xml" ||
                    mediaType == "text/xml" || mediaType.EndsWith("+xml"),
                "image" => mediaType.StartsWith("image/"),
                "audio" => mediaType.StartsWith("audio/"),
                "video" => mediaType.StartsWith("video/"),
                "model" => mediaType.StartsWith("model/"),
                "archive" => mediaType == "application/zip" ||
                    mediaType == "application/gzip" ||
                    mediaType == "application/x-gzip" ||
                    mediaType == "application/x-7z-compressed" ||
                    mediaType == "application/x-rar-compressed" ||
                    mediaType == "application/vnd.rar" ||
                    mediaType == "application/x-tar" ||
                    mediaType == "application/x-bzip2" ||
                    mediaType == "application/zstd",
                "binary" => mediaType == "application/octet-stream",
                _ => false
            });
        }

        internal static void ConfigureRedirects(
            UnityWebRequest request, bool unrestrictedAccess)
        {
            if (!unrestrictedAccess)
            {
                request.redirectLimit = 0;
            }
        }

        private static void _setHeaders(ref UnityWebRequest request, Table headers)
        {
            request.SetRequestHeader("User-Agent", ApiManager.WebRequestUserAgent);
            if (headers != null)
            {
                foreach (var header in headers.Keys)
                {
                    var key = header.String;
                    var value = headers.Get(key).String;
                    request.SetRequestHeader(key, value);
                }
            }
        }

        [LuaDocsDescription("Sends a GET request permitted by Flags.PluginWebRequestRules and validates its response file type, or permits unrestricted HTTP(S) access when Flags.EnablePluginWebRequests is enabled")]
        [LuaDocsExample(@"WebRequest:Get(""https://www.example.com/"", onSuccess, onError, {[""Accept""] = ""application/json""}, context)")]
        [LuaDocsParameter("url", "An HTTP(S) URL allowed by Flags.PluginWebRequestRules or the unrestricted Flags.EnablePluginWebRequests option")]
        [LuaDocsParameter("onSuccess", "A function to call when the request succeeds")]
        [LuaDocsParameter("onError", "A function to call when the request fails")]
        [LuaDocsParameter("headers", "A table of key-value pairs to send as headers")]
        [LuaDocsParameter("context", "A value to pass to the onSuccess and onError functions")]
        public static void Get(string url, Closure onSuccess, Closure onError = null, Table headers = null, DynValue context = null)
        {
            string[] allowedFileTypes = EnsureLuaNetworkAccessEnabled(url, "GET");
            var request = UnityWebRequest.Get(url);
            ConfigureRedirects(request, AllowsLuaNetworkRedirects);
            _setHeaders(ref request, headers);
            request.SendWebRequest();
            LuaManager.Instance.QueueWebRequest(
                request, onSuccess, onError, context, allowedFileTypes);
        }

        [LuaDocsDescription("Sends a POST request permitted by Flags.PluginWebRequestRules and validates its response file type, or permits unrestricted HTTP(S) access when Flags.EnablePluginWebRequests is enabled")]
        [LuaDocsExample(@"WebRequest:Post(""https://www.example.com/"", {[""foo""] = ""bar""}, onSuccess, onError, {[""Accept""] = ""application/json""}, context)")]
        [LuaDocsParameter("url", "An HTTP(S) URL allowed by Flags.PluginWebRequestRules or the unrestricted Flags.EnablePluginWebRequests option")]
        [LuaDocsParameter("postData", "A table of key-value pairs to send as POST data")]
        [LuaDocsParameter("onSuccess", "A function to call when the request succeeds")]
        [LuaDocsParameter("onError", "A function to call when the request fails")]
        [LuaDocsParameter("headers", "A table of key-value pairs to send as headers")]
        [LuaDocsParameter("context", "A value to pass to the onSuccess and onError functions")]
        public static void Post(string url, Table postData, Closure onSuccess, Closure onError, Table headers, DynValue context)
        {
            string[] allowedFileTypes = EnsureLuaNetworkAccessEnabled(url, "POST");
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            foreach (var pair in postData.Pairs)
            {
                formData.Add(new MultipartFormDataSection($"{pair.Key}={pair.Value}"));
            }
            var request = UnityWebRequest.Post(url, formData);
            ConfigureRedirects(request, AllowsLuaNetworkRedirects);
            _setHeaders(ref request, headers);
            request.SendWebRequest();
            LuaManager.Instance.QueueWebRequest(
                request, onSuccess, onError, context, allowedFileTypes);
        }
    }
}
