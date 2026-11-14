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
using System.Collections.Generic;
using System.IO;

namespace TiltBrush
{
    // A collapsible directory tree flattened into the rows that are currently visible.
    // Expansion state persists across rebuilds so it survives the chooser popup closing.
    public class ReferenceDirectoryTree
    {
        public class Node
        {
            public string Path;
            public string Label;
            public int Depth;
            public bool HasChildren;
            public bool IsExpanded;
            public bool IsCurrent;
        }

        // Guards against runaway recursion through symlink/junction loops.
        private const int kMaxDepth = 32;

        private readonly Func<string, string[]> m_GetChildren;
        private readonly Func<string, string> m_GetLabel;
        private readonly HashSet<string> m_Expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Node> m_Nodes = new List<Node>();

        public IReadOnlyList<Node> Nodes => m_Nodes;
        public string RootPath { get; private set; }
        public string CurrentPath { get; private set; }
        public int CurrentIndex { get; private set; } = -1;

        public ReferenceDirectoryTree(Func<string, string[]> getChildren, Func<string, string> getLabel)
        {
            m_GetChildren = getChildren;
            m_GetLabel = getLabel;
        }

        // revealCurrent expands the root, every ancestor of the current directory, and the
        // current directory itself, so the current location and its subfolders are visible.
        public void Rebuild(string rootPath, string currentPath, bool revealCurrent)
        {
            RootPath = Normalize(rootPath);
            CurrentPath = Normalize(currentPath);

            if (revealCurrent)
            {
                m_Expanded.Add(RootPath);
                foreach (var path in GetAncestorChain(RootPath, CurrentPath))
                {
                    m_Expanded.Add(path);
                }
            }

            m_Nodes.Clear();
            CurrentIndex = -1;
            AddNode(RootPath, 0);
        }

        public void ToggleExpanded(string path)
        {
            path = Normalize(path);
            if (!m_Expanded.Remove(path))
            {
                m_Expanded.Add(path);
            }
        }

        private void AddNode(string path, int depth)
        {
            string[] children = depth < kMaxDepth ? m_GetChildren(path) : Array.Empty<string>();
            var node = new Node
            {
                Path = path,
                Label = m_GetLabel(path),
                Depth = depth,
                HasChildren = children.Length > 0,
                IsCurrent = PathsEqual(path, CurrentPath),
            };
            node.IsExpanded = node.HasChildren && m_Expanded.Contains(path);
            if (node.IsCurrent)
            {
                CurrentIndex = m_Nodes.Count;
            }
            m_Nodes.Add(node);

            if (node.IsExpanded)
            {
                foreach (var child in children)
                {
                    AddNode(Normalize(child), depth + 1);
                }
            }
        }

        // Root first, then each directory down to and including current. If current does not
        // live under root (e.g. a virtual subdirectory stored elsewhere) only the two ends are
        // returned; the caller's child provider is responsible for linking them.
        private static List<string> GetAncestorChain(string root, string current)
        {
            var chain = new List<string>();
            if (string.IsNullOrEmpty(current))
            {
                return chain;
            }
            if (IsPathWithin(root, current))
            {
                for (var dir = current; dir != null && !PathsEqual(dir, root);
                     dir = Path.GetDirectoryName(dir))
                {
                    chain.Add(dir);
                }
                chain.Reverse();
            }
            else
            {
                chain.Add(current);
            }
            return chain;
        }

        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }
            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                full = path;
            }
            var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Keep filesystem roots ("/" or "C:\") intact.
            if (trimmed.Length == 0 || trimmed.EndsWith(":"))
            {
                return full;
            }
            return trimmed;
        }

        public static bool PathsEqual(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // True if child is strictly inside root. Both paths must be normalized.
        public static bool IsPathWithin(string root, string child)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(child) || child.Length <= root.Length)
            {
                return false;
            }
            if (!child.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            char next = child[root.Length];
            bool rootEndsWithSeparator = root[root.Length - 1] == Path.DirectorySeparatorChar ||
                root[root.Length - 1] == Path.AltDirectorySeparatorChar;
            return rootEndsWithSeparator ||
                next == Path.DirectorySeparatorChar || next == Path.AltDirectorySeparatorChar;
        }
    }
}
