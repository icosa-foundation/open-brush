// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public static class ShapesXrModelAdapter
    {
        /// Converted ShapesXR models group alternate renderers by named LOD nodes.
        /// Keep the lowest available level number in each group; ordinary nodes stay intact.
        public static IReadOnlyCollection<int> HiddenLodNodes(JObject gltf)
        {
            var nodes = gltf["nodes"] as JArray ?? throw new InvalidDataException("Missing glTF nodes");
            var hidden = new HashSet<int>();
            foreach (JObject group in nodes)
            {
                if (!((string)group["name"] ?? "").StartsWith("__LOD_GROUP__", System.StringComparison.Ordinal) || !(group["children"] is JArray children)) { continue; }
                var candidates = new List<(int Node, int Level)>();
                foreach (var child in children)
                {
                    int index = (int)child;
                    if (index < 0 || index >= nodes.Count) { throw new InvalidDataException("Invalid glTF child node"); }
                    string name = (string)nodes[index]["name"] ?? "";
                    if (nodes[index]["mesh"] == null || !name.StartsWith("__LOD_", System.StringComparison.Ordinal)) { continue; }
                    string level = new string(name.Skip(6).TakeWhile(c => c >= '0' && c <= '9').ToArray());
                    if (!int.TryParse(level, out int number)) { throw new InvalidDataException("Invalid ShapesXR LOD node name"); }
                    candidates.Add((index, number));
                }
                if (candidates.Count == 0) { continue; }
                int best = candidates.Min(candidate => candidate.Level);
                foreach (var candidate in candidates) { if (candidate.Level != best) { hidden.Add(candidate.Node); } }
            }
            return hidden;
        }
    }
}
