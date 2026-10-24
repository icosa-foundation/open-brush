// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public static class ShapesXrModelAdapter
    {
        public static IReadOnlyDictionary<int, JObject> ReadMaterialProperties(ShapesXrDocument document, ShapesXrObjectState state)
        {
            var result = new Dictionary<int, JObject>();
            foreach (var property in state.Properties.Properties())
            {
                int index;
                if (property.Name == "mid") { index = 0; }
                else if (!property.Name.StartsWith("mid-", StringComparison.Ordinal) ||
                    !int.TryParse(property.Name.Substring(4), NumberStyles.None, CultureInfo.InvariantCulture, out index) || index < 1)
                { continue; }
                Guid id = ShapesXrDocument.ReadGuid(property.Value);
                if (id == Guid.Empty) { continue; }
                if (!document.Entities.TryGetValue(id, out var material) || !material.Exists || material.Kind != 10)
                { throw new InvalidDataException($"Missing ShapesXR model material {id}"); }
                if (result.ContainsKey(index)) { throw new InvalidDataException("Duplicate ShapesXR model material slot"); }
                result.Add(index, document.ResolveProperties(id, state.StateId));
            }
            return result;
        }

        public static IReadOnlyList<GltfMaterialOverride> MaterialOverrides(JObject gltf,
            IReadOnlyDictionary<int, JObject> properties, out string[] unmapped)
        {
            var result = new List<GltfMaterialOverride>();
            var issues = new HashSet<string>(StringComparer.Ordinal);
            if (properties.Count == 0) { unmapped = Array.Empty<string>(); return result.AsReadOnly(); }
            var nodes = gltf["nodes"] as JArray ?? throw new InvalidDataException("Missing glTF nodes");
            var meshes = gltf["meshes"] as JArray ?? throw new InvalidDataException("Missing glTF meshes");
            var hidden = new HashSet<int>(HiddenLodNodes(gltf));
            var groups = new List<(int Node, int Primitive, int Original)>();
            var visited = new HashSet<int>();
            void Visit(int index)
            {
                if (index < 0 || index >= nodes.Count) { throw new InvalidDataException("Invalid glTF scene node"); }
                if (hidden.Contains(index) || !visited.Add(index)) { return; }
                var node = nodes[index];
                if (node["mesh"] != null)
                {
                    int mesh = (int)node["mesh"];
                    if (mesh < 0 || mesh >= meshes.Count) { throw new InvalidDataException("Invalid glTF mesh index"); }
                    var primitives = meshes[mesh]["primitives"] as JArray ?? throw new InvalidDataException("Missing glTF primitives");
                    int original = (int?)node["extras"]?["original_index"] ?? 0;
                    for (int p = 0; p < primitives.Count; ++p) { groups.Add((index, p, original)); }
                }
                if (node["children"] is JArray children) { foreach (var child in children) { Visit((int)child); } }
            }
            var scenes = gltf["scenes"] as JArray ?? throw new InvalidDataException("Missing glTF scenes");
            int scene = (int?)gltf["scene"] ?? 0;
            if (scene < 0 || scene >= scenes.Count) { throw new InvalidDataException("Invalid glTF default scene"); }
            if (scenes[scene]["nodes"] is JArray roots) { foreach (var root in roots) { Visit((int)root); } }
            var ordered = groups.OrderBy(group => group.Original).ToArray();
            foreach (var property in properties)
            {
                if (property.Key < 0 || property.Key >= ordered.Length)
                { issues.Add($"material slot {property.Key} has no mesh group"); continue; }
                var group = ordered[property.Key];
                var primitive = meshes[(int)nodes[group.Node]["mesh"]]["primitives"][group.Primitive];
                JObject original = new JObject();
                if (primitive["material"] != null)
                {
                    int material = (int)primitive["material"];
                    if (!(gltf["materials"] is JArray materials) || material < 0 || material >= materials.Count)
                    { throw new InvalidDataException("Invalid glTF material index"); }
                    original = (JObject)materials[material];
                }
                result.Add(new GltfMaterialOverride(group.Node, group.Primitive,
                    OverrideMaterial(original, property.Value, issues)));
            }
            unmapped = issues.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return result.AsReadOnly();
        }

        private static JObject OverrideMaterial(JObject original, JObject properties, HashSet<string> issues)
        {
            // Validate the shared basic fields, but inherit any absent fields from the
            // source glTF rather than the defaults for newly created ShapesXR panels.
            var appearance = ShapesXrMaterialAdapter.Read(properties, 1, ShapesXrMaterialUsage.Panel);
            var material = (JObject)original.DeepClone();
            var pbr = material["pbrMetallicRoughness"] as JObject;
            if (pbr == null) { pbr = new JObject(); material["pbrMetallicRoughness"] = pbr; }
            var color = pbr["baseColorFactor"] as JArray;
            if (color == null) { color = new JArray(1f, 1f, 1f, 1f); pbr["baseColorFactor"] = color; }
            if (color.Count != 4) { throw new InvalidDataException("Invalid glTF base color factor"); }
            string originalMode = (string)material["alphaMode"] ?? "OPAQUE";
            float opacity = properties["m-o"] != null ? (float)properties["m-o"] : originalMode == "OPAQUE" ? 1 : (float)color[3];
            string mode = properties["m-tam"] == null ? originalMode :
                (int)properties["m-tam"] == 0 ? "OPAQUE" : (int)properties["m-tam"] == 1 ? "MASK" : "BLEND";
            if (opacity < 1 && (properties["m-c"] != null || properties["m-o"] != null || properties["m-tam"] != null)) { mode = "BLEND"; }
            if (properties["m-c"] is JArray rgba)
            {
                // Saved Color32 values are gamma colors; glTF factors are linear.
                // UnityGLTF converts those factors back to Unity material color values.
                for (int i = 0; i < 3; ++i)
                {
                    float component = appearance.Color[i];
                    color[i] = component <= .04045f ? component / 12.92f :
                        (float)Math.Pow((component + .055f) / 1.055f, 2.4);
                }
                color[3] = mode == "OPAQUE" ? 1 : (int)rgba[3] / 255f * opacity;
            }
            else if (properties["m-o"] != null || properties["m-tam"] != null) { color[3] = mode == "OPAQUE" ? 1 : opacity; }
            if (properties["m-c"] != null || properties["m-o"] != null || properties["m-tam"] != null) { material["alphaMode"] = mode; }
            if (properties["m-tam"] != null && mode == "MASK") { issues.Add("alpha-mask threshold"); }
            if (properties["m-met"] != null) { pbr["metallicFactor"] = UnitValue(properties["m-met"]); }
            if (properties["m-rou"] != null) { pbr["roughnessFactor"] = UnitValue(properties["m-rou"]); }
            if (properties["m-st"] != null)
            {
                var extensions = material["extensions"] as JObject;
                if (appearance.Lit) { extensions?.Remove("KHR_materials_unlit"); }
                else
                {
                    if (extensions == null) { extensions = new JObject(); material["extensions"] = extensions; }
                    extensions["KHR_materials_unlit"] = new JObject();
                }
            }
            if (properties["m-bm"] != null && (int)properties["m-bm"] != 0 && mode == "BLEND") { issues.Add(appearance.Blend); }
            if (appearance.Passthrough) { issues.Add("passthrough"); }
            foreach (string field in appearance.UnmappedProperties)
            { if (field != "m-met" && field != "m-rou") { issues.Add(field); } }
            if (material["extensions"]?["KHR_materials_pbrSpecularGlossiness"] != null)
            { issues.Add("specular-glossiness material overrides"); }
            return material;
        }

        private static float UnitValue(JToken value)
        {
            if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)
            { throw new InvalidDataException("Expected a numeric ShapesXR material factor"); }
            float result = (float)value;
            if (float.IsNaN(result) || result < 0 || result > 1) { throw new InvalidDataException("Invalid ShapesXR material factor"); }
            return result;
        }

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
