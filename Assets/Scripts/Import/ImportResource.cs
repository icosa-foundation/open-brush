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
        private readonly Func<Stream> m_Open;
        private readonly Func<string, ImportResource> m_Related;

        internal ImportResource(string name, string origin, string localPath, Func<Stream> open,
            Func<string, ImportResource> related)
        {
            Name = name;
            Origin = origin;
            LocalPath = localPath;
            m_Open = open;
            m_Related = related;
        }

        public Stream OpenRead() => m_Open();
        public ImportResource ResolveRelative(string path) => m_Related(path);
        public string ReadText()
        {
            using (var stream = OpenRead())
            using (var reader = new StreamReader(stream)) { return reader.ReadToEnd(); }
        }
    }
}
