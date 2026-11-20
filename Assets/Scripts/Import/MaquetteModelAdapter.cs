// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public static class MaquetteModelAdapter
    {
        /// Maquette's mesh primitive adopts the source renderer's transform, which is then
        /// replaced by the saved instance transform. Keep the mesh coordinates and unit
        /// conversion here; retaining the source node transforms would apply them twice.
        /// Sources with multiple renderers or deformation need a separate mapping.
        public static bool TryCreateMeshLocalSource(JObject source, float meshUnitScale,
            out JObject result, out float[] center)
        {
            result = null;
            center = null;
            if (!(meshUnitScale > 0) || float.IsInfinity(meshUnitScale))
            {
                throw new ArgumentOutOfRangeException(nameof(meshUnitScale));
            }
            var nodes = source["nodes"] as JArray;
            var scenes = source["scenes"] as JArray;
            int sceneIndex = (int?)source["scene"] ?? 0;
            if (nodes == null || scenes == null || sceneIndex < 0 || sceneIndex >= scenes.Count ||
                !(scenes[sceneIndex]?["nodes"] is JArray roots)) { return false; }

            JObject meshNode = null;
            var pending = new Stack<int>();
            var visited = new HashSet<int>();
            foreach (int root in roots) { pending.Push(root); }
            while (pending.Count > 0)
            {
                int index = pending.Pop();
                if (index < 0 || index >= nodes.Count || !visited.Add(index)) { return false; }
                if (!(nodes[index] is JObject node)) { return false; }
                if (node["mesh"] != null)
                {
                    if (meshNode != null || node["skin"] != null || node["weights"] != null) { return false; }
                    meshNode = node;
                }
                if (node["children"] is JArray children)
                {
                    foreach (int child in children) { pending.Push(child); }
                }
            }
            if (meshNode == null) { return false; }
            int meshIndex = (int)meshNode["mesh"];
            var meshes = source["meshes"] as JArray;
            var accessors = source["accessors"] as JArray;
            if (meshes == null || meshIndex < 0 || meshIndex >= meshes.Count || accessors == null ||
                meshes[meshIndex]?["weights"] != null ||
                !(meshes[meshIndex]?["primitives"] is JArray primitives) || primitives.Count == 0) { return false; }
            var min = new[] { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity };
            var max = new[] { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };
            foreach (JObject primitive in primitives)
            {
                if (primitive["targets"] != null) { return false; }
                int position = (int?)primitive["attributes"]?["POSITION"] ?? -1;
                if (position < 0 || position >= accessors.Count ||
                    !(accessors[position]?["min"] is JArray lo) || lo.Count != 3 ||
                    !(accessors[position]?["max"] is JArray hi) || hi.Count != 3) { return false; }
                for (int axis = 0; axis < 3; ++axis)
                {
                    float low = (float)lo[axis], high = (float)hi[axis];
                    if (float.IsNaN(low) || float.IsInfinity(low) || float.IsNaN(high) || float.IsInfinity(high) || low > high) { return false; }
                    min[axis] = Math.Min(min[axis], low);
                    max[axis] = Math.Max(max[axis], high);
                }
            }

            center = new float[3];
            for (int axis = 0; axis < 3; ++axis) { center[axis] = (min[axis] + max[axis]) * .5f * meshUnitScale; }
            // Work on a copy and keep the original source file and shared buffers intact.
            result = (JObject)source.DeepClone();
            var localNode = (JObject)meshNode.DeepClone();
            foreach (string key in new[] { "matrix", "translation", "rotation", "scale", "children" }) { localNode.Remove(key); }
            localNode["scale"] = new JArray(meshUnitScale, meshUnitScale, meshUnitScale);
            var resultNodes = (JArray)result["nodes"];
            int localIndex = resultNodes.Count;
            resultNodes.Add(localNode);
            result["scenes"][sceneIndex]["nodes"] = new JArray(localIndex);
            // Maquette mesh primitives are snapshots; animation of detached source nodes is not
            // part of the restored primitive. The unmodified source retains those channels.
            result.Remove("animations");
            return true;
        }
    }
}
