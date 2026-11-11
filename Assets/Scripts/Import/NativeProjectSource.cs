// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    /// A project directory or root-layout ZIP. Resources are read without extracting archives.
    public sealed class NativeProjectSource : IDisposable
    {
        private readonly string m_Directory;
        private readonly ZipArchive m_Archive;
        private readonly Dictionary<string, ZipArchiveEntry> m_Entries;

        public string Path { get; }
        public IEnumerable<string> Files => m_Entries != null ? m_Entries.Keys :
            Directory.EnumerateFiles(m_Directory, "*", SearchOption.AllDirectories)
                .Select(file => Normalize(System.IO.Path.GetRelativePath(m_Directory, file)));

        public NativeProjectSource(string path)
        {
            Path = System.IO.Path.GetFullPath(path);
            if (Directory.Exists(Path))
            {
                m_Directory = Path;
                return;
            }
            m_Archive = ZipFile.OpenRead(Path);
            try
            {
                m_Entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in m_Archive.Entries)
                {
                    if (entry.FullName.EndsWith("/")) { continue; }
                    string name = Normalize(entry.FullName);
                    if (m_Entries.ContainsKey(name))
                    {
                        throw new InvalidDataException($"Ambiguous project resource: {name}");
                    }
                    m_Entries.Add(name, entry);
                }
            }
            catch
            {
                m_Archive.Dispose();
                throw;
            }
        }

        public bool Contains(string relativePath)
        {
            string name = Normalize(relativePath);
            return m_Entries != null ? m_Entries.ContainsKey(name) : File.Exists(DirectoryPath(name));
        }

        public Stream OpenRead(string relativePath)
        {
            string name = Normalize(relativePath);
            if (m_Entries == null) { return File.OpenRead(DirectoryPath(name)); }
            if (!m_Entries.TryGetValue(name, out var entry))
            {
                throw new FileNotFoundException($"Project resource not found: {name}", name);
            }
            return entry.Open();
        }

        public JObject ReadJson(string relativePath)
        {
            using (var stream = OpenRead(relativePath))
            using (var text = new StreamReader(stream))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 256 })
            {
                var result = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (reader.Read()) { throw new InvalidDataException($"Trailing JSON in {relativePath}"); }
                return result;
            }
        }

        public string ReadText(string relativePath)
        {
            using (var stream = OpenRead(relativePath))
            using (var reader = new StreamReader(stream)) { return reader.ReadToEnd(); }
        }

        private string DirectoryPath(string name)
        {
            string resolved = System.IO.Path.GetFullPath(System.IO.Path.Combine(m_Directory, name));
            if (!resolved.StartsWith(m_Directory.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Resource is outside the project: {name}");
            }
            return resolved;
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) { throw new InvalidDataException("Empty resource path"); }
            string name = path.Replace('\\', '/');
            if (name.StartsWith("/") || name.Contains(":") || name.Split('/').Any(part => part == ".."))
            {
                throw new InvalidDataException($"Invalid project resource path: {path}");
            }
            return string.Join("/", name.Split('/').Where(part => part != "." && part.Length != 0));
        }

        public void Dispose() { m_Archive?.Dispose(); }
    }
}
