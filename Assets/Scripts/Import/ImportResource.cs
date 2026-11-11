// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;

namespace TiltBrush.DocumentImport
{
    /// A resource plus its dependency resolver. The owner keeps any project archive alive
    /// until its streams have been consumed. No files are extracted or downloaded here.
    public sealed class ImportResource
    {
        public string Name { get; }
        public string Origin { get; }
        public string LocalPath { get; }
        public string ProjectRelativePath { get; }
        private readonly Func<Stream> m_Open;
        private readonly Func<string, ImportResource> m_Related;

        internal ImportResource(string name, string origin, string localPath, Func<Stream> open,
            Func<string, ImportResource> related, string projectRelativePath = null)
        {
            Name = name;
            Origin = origin;
            LocalPath = localPath;
            ProjectRelativePath = projectRelativePath;
            m_Open = open;
            m_Related = related;
        }

        public Stream OpenRead() => m_Open();
        public ImportResource ResolveRelative(string path) => m_Related(path);

        public static ImportResource FromFile(string path, string resourceRoot = null)
        {
            path = Path.GetFullPath(path);
            string root = Path.GetFullPath(resourceRoot ?? Path.GetDirectoryName(path));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("Asset escapes its resource root"); }
            if (!File.Exists(path)) { return null; }
            return new ImportResource(Path.GetFileName(path), path, path, () => File.OpenRead(path), relative =>
            {
                if (Path.IsPathRooted(relative) || relative.Contains(":")) { throw new InvalidDataException("Expected a relative asset dependency"); }
                return FromFile(Path.Combine(Path.GetDirectoryName(path), relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)), root);
            });
        }

        public string ReadText()
        {
            using (var stream = OpenRead())
            using (var reader = new StreamReader(stream)) { return reader.ReadToEnd(); }
        }
    }
}
