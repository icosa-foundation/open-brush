// Copyright 2020 The Tilt Brush Authors
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
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TiltBrush
{

    /// A file referenced by the .an export. You should use this instead of bare strings because
    /// the exporter treats these specially:
    /// - Does the file copying into the output
    /// - Only copies the file if it's actually referenced by the json
    /// - Does some sanity-checking about URI encoding
    public class ExportFileReference
    {
        /// For use with CreateDisambiguated().
        public class DisambiguationContext
        {
            // Only public for use by CreateDisambiguated
            // Exported file names that are being used and that are therefore off-limits.
            // Same as set(m_filesBySourceIdentity.values.m_uri)
            public HashSet<string> m_exportedFileNames = new HashSet<string>();
            // Only public for use by CreateDisambiguated
            // Non-http file refs keyed by their stable filesystem or provider identity.
            public Dictionary<string, ExportFileReference> m_filesBySourceIdentity =
                new Dictionary<string, ExportFileReference>();
        }

        // Returns a full path to the URI.
        // Throws an exception if arguments don't resolve to a local file:
        // - uri is http
        // - uri is relative, but there is no uriBase
        private static string GetFullPathForUri(string sourceUri, string uriBase)
        {
            if (IsHttp(sourceUri)) { throw new ArgumentException("sourceUri"); }

            if (sourceUri.StartsWith(ExportUtils.kBuiltInPrefix))
            {
                string defaultName = sourceUri.Substring(ExportUtils.kBuiltInPrefix.Length);
                return Path.Combine(App.SupportPath(), defaultName);
            }
            else if (Path.IsPathRooted(sourceUri))
            {
                Debug.LogFormat("Unexpected non-relative URI on export: {0}", sourceUri);
                return sourceUri;
            }
            else
            {
                if (uriBase == null) { throw new ArgumentNullException("uriBase"); }
                return Path.Combine(uriBase, sourceUri);
            }
        }

        public static bool IsHttp(string uri)
        {
            return (uri.StartsWith("http:") || uri.StartsWith("https:"));
        }

        /// Like the other CreateLocal but passes the name through a sanitizer.
        /// Use this if you don't control where "name" comes from.
        public static ExportFileReference CreateSafeLocal(string originalLocation, string unsafeName)
        {
            return CreateLocal(originalLocation,
                FileUtils.SanitizeFilenameAndPreserveUniqueness(unsafeName));
        }

        /// Returns a FileReference that will get copied into the output.
        ///
        /// Warning: nothing currently prevents you from:
        /// - copying the same originalLocation to N different names
        /// - copying N different originalLocations to the same name
        ///
        /// Pass:
        ///   originalLocation - path to an existing file
        ///   name - the name of the file in the export folder; must consist entirely of alphanums and '_'
        public static ExportFileReference CreateLocal(string originalLocation, string name)
        {
            if (!File.Exists(originalLocation))
            {
                throw new ArgumentException("originalLocation must exist");
            }
            if (Uri.EscapeUriString(name) != name)
            {
                throw new ArgumentException("name: has invalid characters");
            }
            return new ExportFileReference(
                true, originalLocation, originalLocation, null, name);
        }

        /// Returns a FileReference that will not get copied into the output.
        /// This is only useful if the uri is http:// or https://
        public static ExportFileReference CreateHttp(string uri)
        {
            if (!IsHttp(uri)) { throw new ArgumentException("Uri not http"); }
            return new ExportFileReference(false, null, null, null, uri);
        }

        /// Returns a (possibly shared) ExportFileReference for the uri.
        /// The destination file:
        /// - will have no path components in it
        /// - will not collide with any other destination files
        /// - will be similar to suggestedName (if suggestedName is passed)
        public static ExportFileReference GetOrCreateSafeLocal(
            DisambiguationContext disambiguationContext, string sourceUri, string uriBase,
            string suggestedName = null)
        {
            if (IsHttp(sourceUri))
            {
                throw new ArgumentException("sourceUri");
            }

            // Passing the same source file multiple times should give the same destination file
            string sourcePath = GetFullPathForUri(sourceUri, uriBase);
            if (disambiguationContext.m_filesBySourceIdentity.TryGetValue(
                sourcePath, out var existingRef))
            {
                return existingRef;
            }

            // A newly-seen source file should get an unused destination
            string exportedFileName = ExportUtils.CreateUniqueName(
                Path.GetFileName(suggestedName ?? sourcePath), disambiguationContext.m_exportedFileNames);
            var fileRef = CreateSafeLocal(sourcePath, exportedFileName);
            disambiguationContext.m_filesBySourceIdentity[fileRef.m_sourceIdentity] = fileRef;
            return fileRef;
        }

        /// Returns a local export reference backed either by a normal file or by a stream supplied
        /// by the material. Stream-backed sources are copied directly into the final export output;
        /// never materialize them merely to accommodate a path-only exporter API.
        public static ExportFileReference GetOrCreateSafeLocal(
            DisambiguationContext disambiguationContext, IExportableMaterial material,
            string sourceUri, string suggestedName = null)
        {
            if (material is IExportableMaterialTextureSource source &&
                source.TryGetTextureSource(
                    sourceUri, out string sourceIdentity, out Func<Stream> openRead))
            {
                if (disambiguationContext.m_filesBySourceIdentity.TryGetValue(
                    sourceIdentity, out ExportFileReference existingRef))
                {
                    return existingRef;
                }

                string exportedFileName = ExportUtils.CreateUniqueName(
                    Path.GetFileName(suggestedName ?? sourceUri),
                    disambiguationContext.m_exportedFileNames);
                var fileRef = new ExportFileReference(
                    true, sourceIdentity, null, openRead,
                    FileUtils.SanitizeFilenameAndPreserveUniqueness(exportedFileName));
                disambiguationContext.m_filesBySourceIdentity[sourceIdentity] = fileRef;
                return fileRef;
            }

            return GetOrCreateSafeLocal(
                disambiguationContext, sourceUri, material.UriBase, suggestedName);
        }

        // If true, m_uri looks like a relative path, and the file or stream source needs to be
        // copied into the export output.
        // If false, m_uri looks like a http:// or https://, must be properly encoded,
        // source fields will be null, and no copying needs to take place.
        public readonly bool m_local;
        // Stable identity used to disambiguate filesystem and stream-backed sources.
        public readonly string m_sourceIdentity;
        // A full path to the source file, or null if there is none (eg, it was http:)
        public readonly string m_originalLocation;
        // The uri that the export should use to refer to the file.
        // This is often relative to the export directory, but may be an absolute http.
        public readonly string m_uri;

        // Callers enforce these conditions:
        // If local is true, exactly one of originalLocation or openRead supplies readable content.
        // Otherwise, both are null and uri must be http:
        private readonly Func<Stream> m_openRead;

        private ExportFileReference(
            bool local, string sourceIdentity, string originalLocation,
            Func<Stream> openRead, string uri)
        {
            m_local = local;
            m_sourceIdentity = sourceIdentity;
            m_originalLocation = originalLocation;
            m_openRead = openRead;
            m_uri = uri;
        }

        public void CopyTo(string destination)
        {
            if (!m_local) { throw new InvalidOperationException("HTTP references are not copied"); }
            if (m_openRead == null)
            {
                File.Copy(m_originalLocation, destination);
                return;
            }

            // This is intentionally a direct source-to-destination copy. Introducing an
            // intermediate local file here would re-materialize SAF content for no reason.
            using (Stream source = m_openRead())
            using (var output = new FileStream(
                destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(output);
            }
        }

        public string AsJson()
        {
            return new SimpleJSON.JSONData(m_uri).ToString();
        }

        public bool IsHttp()
        {
            return ExportFileReference.IsHttp(m_uri);
        }
    }

} // namespace TiltBrush
