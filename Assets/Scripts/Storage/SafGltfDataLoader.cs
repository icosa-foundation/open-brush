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
            if (string.IsNullOrWhiteSpace(relativeFilePath))
            {
                throw new ArgumentException("Empty glTF reference.", nameof(relativeFilePath));
            }

            // The importer escapes URI references, so a name with spaces arrives percent-encoded.
            string requested = Normalize(Uri.UnescapeDataString(relativeFilePath));
            if (Path.IsPathRooted(requested) || requested.Contains(".."))
            {
                throw new IOException($"glTF reference escapes the model directory: {relativeFilePath}");
            }

            string path = string.IsNullOrEmpty(m_Directory)
                ? requested
                : $"{m_Directory}/{requested}";

            // Worker-thread reads are attached to the JVM inside the SAF backend.
            return UserStorage.Backend.OpenRead(
                m_Area, path, requireSeekable: false, CancellationToken.None);
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim('/');
        }
    }
}
