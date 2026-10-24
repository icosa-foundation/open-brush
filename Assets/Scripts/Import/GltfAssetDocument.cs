// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    /// A persistent glTF source plus shared resources. Variants add node transforms and optional
    /// appearance overrides without making another copy of the vertex/index/texture data.
    public sealed class GltfAssetDocument
    {
        public string Path { get; }
        private readonly JObject m_Data;

        public GltfAssetDocument(string path)
        {
            Path = System.IO.Path.GetFullPath(path);
            m_Data = ReadJson(File.ReadAllText(Path));
            if ((string)m_Data["asset"]?["version"] != "2.0") { throw new NotSupportedException("Native model caching requires glTF 2.0"); }
        }

        public static GltfAssetDocument CopySource(ImportResource resource, string localPath, string directory)
        {
            Directory.CreateDirectory(directory);
            JObject data;
            string binaryPath = System.IO.Path.Combine(directory, "source.bin");
            if (System.IO.Path.GetExtension(localPath).Equals(".glb", StringComparison.OrdinalIgnoreCase))
            {
                data = UnpackGlb(localPath, binaryPath);
            }
            else { data = ReadJson(File.ReadAllText(localPath)); }
            var copied = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string collection in new[] { "buffers", "images" })
            {
                if (!(data[collection] is JArray entries)) { continue; }
                foreach (JObject entry in entries)
                {
                    string uri = (string)entry["uri"];
                    bool internalBinary = collection == "buffers" && ReferenceEquals(entry, data["buffers"]?[0]) &&
                                          uri == "source.bin" && File.Exists(binaryPath);
                    if (uri == null || IsRemoteOrInline(uri) || internalBinary) { continue; }
                    var dependency = resource.ResolveRelative(Uri.UnescapeDataString(uri))
                                     ?? throw new FileNotFoundException($"Missing glTF dependency: {uri}");
                    if (!copied.TryGetValue(dependency.Origin, out string name))
                    {
                        string extension = System.IO.Path.GetExtension(dependency.Name);
                        if (extension.IndexOfAny(new[] { '/', '\\', ':', '?', '#' }) >= 0) { throw new InvalidDataException("Invalid glTF dependency extension"); }
                        name = $"asset-{copied.Count}{extension}";
                        using (var input = dependency.OpenRead())
                        using (var output = new FileStream(System.IO.Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write))
                        {
                            input.CopyTo(output);
                        }
                        copied.Add(dependency.Origin, name);
                    }
                    entry["uri"] = name;
                }
            }
            string target = System.IO.Path.Combine(directory, "source.gltf");
            WriteJson(target, data);
            return new GltfAssetDocument(target);
        }

        public void WriteVariant(string path, IEnumerable<GeometryStepData> transforms, float[] centerOffset, float[] color = null,
            IEnumerable<int> excludedNodes = null, float opacity = 1)
        {
            if (float.IsNaN(opacity) || opacity < 0 || opacity > 1) { throw new ArgumentOutOfRangeException(nameof(opacity)); }
            var data = (JObject)m_Data.DeepClone();
            RebaseResources(data, System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
            var nodes = data["nodes"] as JArray ?? throw new InvalidDataException("glTF model has no nodes");
            var scenes = data["scenes"] as JArray ?? throw new InvalidDataException("glTF model has no scenes");
            int index = (int?)data["scene"] ?? 0;
            if (index < 0 || index >= scenes.Count) { throw new InvalidDataException("Invalid glTF default scene"); }
            var scene = scenes[index] as JObject ?? throw new InvalidDataException("Invalid glTF scene");
            var roots = scene["nodes"] as JArray ?? throw new InvalidDataException("glTF scene has no root nodes");
            roots = (JArray)roots.DeepClone();
            if (excludedNodes != null)
            {
                var excluded = new HashSet<int>(excludedNodes);
                if (excluded.Any(node => node < 0 || node >= nodes.Count)) { throw new ArgumentException("Excluded glTF node is out of range", nameof(excludedNodes)); }
                foreach (JObject node in nodes)
                {
                    if (node["children"] is JArray children)
                    {
                        node["children"] = new JArray(children.Where(child => !excluded.Contains((int)child)));
                    }
                }
                roots = new JArray(roots.Where(root => !excluded.Contains((int)root)));
            }
            if (centerOffset != null)
            {
                if (centerOffset.Length != 3) { throw new ArgumentException("Expected three model center components", nameof(centerOffset)); }
                // UnityGLTF changes handedness by reflecting X, in both Maquette and Open Brush.
                roots = AddNode(nodes, roots, new JObject { ["translation"] = new JArray(-centerOffset[0], centerOffset[1], centerOffset[2]) });
            }
            foreach (var transform in transforms)
            {
                if (transform.Type != "transform") { throw new ArgumentException("Expected only intrinsic transform steps", nameof(transforms)); }
                var scale = (float[])transform.Parameters["scale"];
                var rotation = (float[])transform.Parameters["rotation"];
                roots = AddNode(nodes, roots, new JObject
                {
                    ["scale"] = new JArray(scale),
                    ["rotation"] = new JArray(rotation[0], -rotation[1], -rotation[2], rotation[3])
                });
            }
            scene["nodes"] = roots;
            if (color != null) { OverrideColor(data, color); }
            if (opacity < 1) { ApplyOpacity(data, opacity); }
            WriteJson(path, data);
        }

        private static void ApplyOpacity(JObject data, float opacity)
        {
            var materials = data["materials"] as JArray;
            if (materials == null) { materials = new JArray(); data["materials"] = materials; }
            // A primitive without a material uses glTF's implicit opaque white material.
            // Give those primitives a shared explicit material so they fade as well.
            int defaultMaterial = -1;
            if (data["meshes"] is JArray meshes)
            {
                foreach (JObject mesh in meshes)
                {
                    if (!(mesh["primitives"] is JArray primitives)) { continue; }
                    foreach (JObject primitive in primitives)
                    {
                        if (primitive["material"] != null) { continue; }
                        if (defaultMaterial < 0)
                        {
                            defaultMaterial = materials.Count;
                            materials.Add(new JObject());
                        }
                        primitive["material"] = defaultMaterial;
                    }
                }
            }
            foreach (JObject material in materials)
            {
                var pbr = material["pbrMetallicRoughness"] as JObject;
                if (pbr == null) { pbr = new JObject(); material["pbrMetallicRoughness"] = pbr; }
                var color = pbr["baseColorFactor"] as JArray;
                if (color == null) { color = new JArray(1f, 1f, 1f, 1f); pbr["baseColorFactor"] = color; }
                if (color.Count != 4) { throw new InvalidDataException("Invalid glTF base color factor"); }
                // Opaque source materials ignore their saved alpha. A source fade switches
                // to blending with object opacity; masked/blended materials retain their alpha.
                float alpha = ((string)material["alphaMode"] ?? "OPAQUE") == "OPAQUE" ? 1 : (float)color[3];
                color[3] = alpha * opacity;
                material["alphaMode"] = "BLEND";
            }
        }

        private static JArray AddNode(JArray nodes, JArray children, JObject node)
        {
            node["children"] = children;
            int index = nodes.Count;
            nodes.Add(node);
            return new JArray(index);
        }

        private static void OverrideColor(JObject data, float[] color)
        {
            if (color.Length != 4) { throw new ArgumentException("Expected four color components", nameof(color)); }
            var materials = data["materials"] as JArray;
            if (materials == null) { materials = new JArray(); data["materials"] = materials; }
            int material = materials.Count;
            materials.Add(new JObject
            {
                ["name"] = "Imported color", ["alphaMode"] = color[3] < 1 ? "BLEND" : "OPAQUE",
                ["pbrMetallicRoughness"] = new JObject
                {
                    ["baseColorFactor"] = new JArray(color), ["metallicFactor"] = 0f, ["roughnessFactor"] = 1f
                }
            });
            if (!(data["meshes"] is JArray meshes)) { return; }
            foreach (JObject mesh in meshes)
            {
                if (!(mesh["primitives"] is JArray primitives)) { continue; }
                foreach (JObject primitive in primitives) { primitive["material"] = material; }
            }
        }

        private static void RebaseResources(JObject data, string originalDirectory, string targetDirectory)
        {
            foreach (string collection in new[] { "buffers", "images" })
            {
                if (!(data[collection] is JArray entries)) { continue; }
                foreach (JObject entry in entries)
                {
                    string uri = (string)entry["uri"];
                    if (uri == null || IsRemoteOrInline(uri)) { continue; }
                    string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(originalDirectory, Uri.UnescapeDataString(uri)));
                    string relative = System.IO.Path.GetRelativePath(targetDirectory, full).Replace('\\', '/');
                    var components = relative.Split('/');
                    for (int i = 0; i < components.Length; ++i)
                    {
                        if (components[i] != ".." && components[i] != ".") { components[i] = Uri.EscapeDataString(components[i]); }
                    }
                    entry["uri"] = string.Join("/", components);
                }
            }
        }

        private static bool IsRemoteOrInline(string uri) => uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

        private static JObject ReadJson(string text)
        {
            using (var input = new StringReader(text))
            using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None })
            {
                var result = GeometryRecipeData.LoadObject(reader);
                if (reader.Read()) { throw new InvalidDataException("Trailing glTF JSON data"); }
                return result;
            }
        }

        private static void WriteJson(string path, JObject data)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(file, new UTF8Encoding(false))) { writer.Write(data.ToString(Formatting.None)); }
        }

        private static JObject UnpackGlb(string path, string binaryPath)
        {
            using (var file = File.OpenRead(path))
            using (var reader = new BinaryReader(file))
            {
                if (reader.ReadUInt32() != 0x46546C67 || reader.ReadUInt32() != 2 || reader.ReadUInt32() != file.Length)
                {
                    throw new InvalidDataException("Invalid GLB 2.0 header");
                }
                JObject data = null;
                bool binary = false;
                while (file.Position < file.Length)
                {
                    uint length = reader.ReadUInt32(), type = reader.ReadUInt32();
                    if (length > file.Length - file.Position || length % 4 != 0) { throw new InvalidDataException("Invalid GLB chunk length"); }
                    if (type == 0x4E4F534A)
                    {
                        if (data != null || binary || length > int.MaxValue) { throw new InvalidDataException("Invalid GLB JSON chunk"); }
                        data = ReadJson(Encoding.UTF8.GetString(reader.ReadBytes((int)length)).TrimEnd('\0', ' '));
                    }
                    else if (type == 0x004E4942)
                    {
                        if (data == null || binary) { throw new InvalidDataException("Invalid GLB binary chunk"); }
                        using (var output = new FileStream(binaryPath, FileMode.CreateNew, FileAccess.Write))
                        {
                            var block = new byte[65536];
                            long remaining = length;
                            while (remaining > 0)
                            {
                                int read = file.Read(block, 0, (int)Math.Min(block.Length, remaining));
                                if (read == 0) { throw new EndOfStreamException("Truncated GLB binary data"); }
                                output.Write(block, 0, read);
                                remaining -= read;
                            }
                        }
                        binary = true;
                        if (!(data["buffers"] is JArray buffers) || buffers.Count == 0 || buffers[0]["uri"] != null ||
                            (long?)buffers[0]["byteLength"] > length)
                        {
                            throw new InvalidDataException("Invalid GLB binary buffer");
                        }
                        buffers[0]["uri"] = "source.bin";
                    }
                    else
                    {
                        if (data == null) { throw new InvalidDataException("GLB must begin with JSON"); }
                        file.Seek(length, SeekOrigin.Current);
                    }
                }
                return data ?? throw new InvalidDataException("GLB has no JSON chunk");
            }
        }
    }
}
