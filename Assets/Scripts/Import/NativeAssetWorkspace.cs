// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;

namespace TiltBrush.DocumentImport
{
    /// Provides filenames for native loaders which cannot consume archive streams. Existing local
    /// files stay in place. An archive is extracted once into an owned temporary directory, keeping
    /// all relative dependencies (including FBX/OBJ sidecars) intact. Final imported assets must be
    /// made persistent before this workspace is disposed.
    public sealed class NativeAssetWorkspace : IDisposable
    {
        private readonly NativeProjectSource m_Source;
        private readonly string m_CacheDirectory;
        private string m_ExtractedRoot;
        private bool m_ExtractionComplete;
        private bool m_Disposed;

        public NativeAssetWorkspace(NativeProjectSource source, string cacheDirectory)
        {
            m_Source = source ?? throw new ArgumentNullException(nameof(source));
            m_CacheDirectory = Path.GetFullPath(cacheDirectory);
        }

        public string LocalPath(ImportResource resource)
        {
            if (m_Disposed) { throw new ObjectDisposedException(nameof(NativeAssetWorkspace)); }
            if (resource == null) { throw new FileNotFoundException("The native asset could not be resolved"); }
            if (resource.LocalPath != null) { return resource.LocalPath; }
            if (resource.ProjectRelativePath == null ||
                resource.Origin != $"{m_Source.Path}#{resource.ProjectRelativePath}")
            {
                throw new ArgumentException("Resource does not belong to this native project", nameof(resource));
            }
            if (m_ExtractedRoot == null) { Extract(); }
            if (!m_ExtractionComplete) { throw new InvalidDataException("Native archive extraction did not complete"); }
            return WithinRoot(m_ExtractedRoot, resource.ProjectRelativePath);
        }

        private void Extract()
        {
            m_ExtractedRoot = Path.Combine(m_CacheDirectory, $"native-import-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_ExtractedRoot);
            foreach (string relative in m_Source.Files)
            {
                string destination = WithinRoot(m_ExtractedRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using (var input = m_Source.OpenRead(relative))
                using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                {
                    input.CopyTo(output);
                }
            }
            m_ExtractionComplete = true;
        }

        private static string WithinRoot(string root, string relative)
        {
            string path = Path.GetFullPath(Path.Combine(root, relative));
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("Asset escapes the import workspace"); }
            return path;
        }

        public void Dispose()
        {
            if (m_Disposed) { return; }
            m_Disposed = true;
            if (m_ExtractedRoot != null && Directory.Exists(m_ExtractedRoot))
            {
                // The only recursive deletion is our own freshly allocated child directory.
                string prefix = m_CacheDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(m_ExtractedRoot);
                if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("Invalid import cleanup target"); }
                Directory.Delete(target, true);
            }
        }
    }
}
