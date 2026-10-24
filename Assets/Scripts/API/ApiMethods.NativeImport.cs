// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using Newtonsoft.Json;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        internal static string CacheNativeImageUrl(string url) => _DownloadMediaFileFromUrl(
            url, "Images", allowRedirects: true, requiredContentTypePrefix: "image/");

        [ApiEndpoint("import.maquette", "Start importing a Maquette project's current scene; use import.status for progress and unsupported content", "project.mqp")]
        public static string ImportMaquette(string path, string contentDirectory = "", string userLibrary = "")
        {
            return JsonConvert.SerializeObject(NativeDocumentImporter.BeginMaquette(path,
                string.IsNullOrEmpty(contentDirectory) ? null : contentDirectory,
                string.IsNullOrEmpty(userLibrary) ? null : userLibrary));
        }

        [ApiEndpoint("import.status", "Return progress and results for the most recent native import")]
        public static string NativeImportStatus() => JsonConvert.SerializeObject(NativeDocumentImporter.LastReport);
    }
}
