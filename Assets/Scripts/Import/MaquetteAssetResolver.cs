// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;

namespace TiltBrush.DocumentImport
{
    /// Resolves bundled resources before looking in the user's installed libraries.
    /// Microsoft assets remain user-side dependencies; none are distributed with Open Brush.
    public sealed class MaquetteAssetResolver
    {
        private readonly NativeProjectSource m_Source;
        private readonly string m_UserLibrary;
        private readonly string m_Content;

        public MaquetteAssetResolver(NativeProjectSource source, string userLibrary = null, string content = null)
        {
            m_Source = source ?? throw new ArgumentNullException(nameof(source));
            m_UserLibrary = userLibrary;
            m_Content = content;
        }

        public ImportResource Resolve(string nativePath)
        {
            if (string.IsNullOrWhiteSpace(nativePath)) { return null; }
            string path = nativePath.Replace('\\', '/');
            foreach (var token in new[] { (Name: "<MAQUETTEOBJECT>", Folder: "Objects"), (Name: "<MAQUETTEIMAGE>", Folder: "Images") })
            {
                if (!path.StartsWith(token.Name + "/", StringComparison.OrdinalIgnoreCase)) { continue; }
                string relative = Clean(path.Substring(token.Name.Length + 1));
                return Project($"{token.Folder}/{relative}") ?? External(m_UserLibrary, $"{token.Folder}/{relative}");
            }
            const string contentToken = "<MAQUETTECONTENT>/";
            if (path.StartsWith(contentToken, StringComparison.OrdinalIgnoreCase))
            {
                return External(m_Content, Clean(path.Substring(contentToken.Length)));
            }
            if (path.StartsWith("<")) { return null; }
            bool windowsDrive = path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && path[2] == '/';
            if (!windowsDrive && Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile) { return null; }
            if (windowsDrive || Path.IsPathRooted(path))
            {
                if (File.Exists(path))
                {
                    string root = Path.GetPathRoot(Path.GetFullPath(path));
                    return External(root, Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/'));
                }
                // Real legacy documents retain author-machine paths into their own bundled folders.
                foreach (string folder in new[] { "Objects", "Images", "TextContents" })
                {
                    int marker = path.LastIndexOf($"/{folder}/", StringComparison.OrdinalIgnoreCase);
                    if (marker >= 0)
                    {
                        var resource = Project(path.Substring(marker + 1));
                        if (resource != null) { return resource; }
                    }
                }
                return null;
            }
            return Project(Clean(path));
        }

        public ImportResource Text(string entityId)
        {
            if (!Guid.TryParse(entityId, out _)) { throw new InvalidDataException("Invalid Maquette text entity GUID"); }
            return Project($"TextContents/{entityId}.txt");
        }

        private ImportResource Project(string path)
        {
            path = Clean(path);
            if (!m_Source.Contains(path)) { return null; }
            string parent = Parent(path);
            string local = Directory.Exists(m_Source.Path) ? Path.Combine(m_Source.Path, path) : null;
            return new ImportResource(Path.GetFileName(path), $"{m_Source.Path}#{path}", local,
                () => m_Source.OpenRead(path), relative => Project(Join(parent, relative)));
        }

        private static ImportResource External(string root, string relative)
        {
            if (string.IsNullOrEmpty(root)) { return null; }
            string path = Path.GetFullPath(Path.Combine(root, Clean(relative)));
            string resolvedRoot = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            if (!path.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("Asset escapes its resource root"); }
            if (!File.Exists(path)) { return null; }
            return new ImportResource(Path.GetFileName(path), path, path, () => File.OpenRead(path),
                dependency => External(root, Join(Parent(relative.Replace('\\', '/')), dependency)));
        }

        private static string Parent(string path)
        {
            int separator = path.LastIndexOf('/');
            return separator < 0 ? "" : path.Substring(0, separator);
        }

        private static string Join(string parent, string relative)
        {
            if (Path.IsPathRooted(relative) || relative.Contains(":")) { throw new InvalidDataException("Expected a relative asset dependency"); }
            string dependency = relative.Replace('\\', '/');
            return Clean(parent.Length == 0 ? dependency : $"{parent}/{dependency}");
        }

        private static string Clean(string path)
        {
            if (path.StartsWith("/") || path.Contains(":")) { throw new InvalidDataException("Expected a project-relative resource path"); }
            var components = new List<string>();
            foreach (string part in path.Split('/'))
            {
                if (part.Length == 0 || part == ".") { continue; }
                if (part == "..")
                {
                    if (components.Count == 0) { throw new InvalidDataException("Asset dependency escapes its resource root"); }
                    components.RemoveAt(components.Count - 1);
                }
                else { components.Add(part); }
            }
            if (components.Count == 0) { throw new InvalidDataException("Empty asset resource path"); }
            return string.Join("/", components);
        }
    }
}
