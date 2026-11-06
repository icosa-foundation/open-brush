// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public enum ShapesXrResourceKind { Image, Model, Audio }

    public sealed class ShapesXrResourceFile
    {
        public string Name { get; internal set; }
        public Uri Url { get; internal set; }
        public long Size { get; internal set; }
    }

    public sealed class ShapesXrResourceData
    {
        public Guid Id { get; }
        public string Name { get; }
        public ShapesXrResourceKind Kind { get; }
        public IReadOnlyList<ShapesXrResourceFile> Files { get; }
        public ShapesXrResourceFile MainFile { get; }

        public ShapesXrResourceData(JObject response, Guid expectedId)
        {
            if ((bool?)response?["succeeded"] != true || !(response["data"] is JObject data))
            {
                throw new InvalidDataException("ShapesXR resource request did not succeed");
            }
            if (!Guid.TryParse((string)data["id"], out var id) || id != expectedId) { throw new InvalidDataException("ShapesXR resource ID mismatch"); }
            Id = id;
            Name = (string)data["name"] ?? id.ToString();
            string format = ((string)data["format"] ?? "").TrimStart('.').ToLowerInvariant();
            switch (format)
            {
                case "png": case "jpg": case "jpeg": case "bmp": case "tga":
                    Kind = ShapesXrResourceKind.Image;
                    break;
                case "glb": case "gltf": case "obj": case "fbx": case "zip":
                    Kind = ShapesXrResourceKind.Model;
                    break;
                case "wav": case "mp3":
                    Kind = ShapesXrResourceKind.Audio;
                    break;
                default: throw new NotSupportedException($"Unsupported ShapesXR resource format: {format}");
            }
            var groups = data["files"] as JArray ?? throw new InvalidDataException("Missing ShapesXR resource files");
            // Model artifact 3 contains the service's renderable converted files. Original
            // files remain usable when no conversion is supplied and Open Brush supports them.
            var selected = Kind == ShapesXrResourceKind.Model && groups.Count > 3 && groups[3] is JArray converted && converted.Count > 0
                ? converted : groups.FirstOrDefault() as JArray;
            if (selected == null || selected.Count == 0) { throw new InvalidDataException("ShapesXR resource contains no usable files"); }
            var files = new List<ShapesXrResourceFile>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in selected)
            {
                string filename = (string)token["filename"];
                if (string.IsNullOrWhiteSpace(filename)) { throw new InvalidDataException("Missing ShapesXR resource filename"); }
                filename = filename.Replace('\\', '/');
                if (filename.StartsWith("/") || filename.Contains(":") || filename.Split('/').Any(part => part == ".." || part.Length == 0 || part == "."))
                {
                    throw new InvalidDataException("Invalid ShapesXR resource filename");
                }
                if (!names.Add(filename)) { throw new InvalidDataException("Duplicate ShapesXR resource filename"); }
                if (!Uri.TryCreate((string)token["url"], UriKind.Absolute, out var url) || (url.Scheme != "https" && url.Scheme != "http"))
                {
                    throw new InvalidDataException("Invalid ShapesXR resource URL");
                }
                long size = (long?)token["size"] ?? -1;
                if (size < 0) { throw new InvalidDataException("Invalid ShapesXR resource size"); }
                files.Add(new ShapesXrResourceFile { Name = filename, Url = url, Size = size });
            }
            Files = files;
            MainFile = Kind == ShapesXrResourceKind.Image ? files.FirstOrDefault(file => IsImage(file.Name)) :
                Kind == ShapesXrResourceKind.Audio ? files.FirstOrDefault(file => IsAudio(file.Name)) :
                files.FirstOrDefault(file => file.Name.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) ??
                files.FirstOrDefault(file => IsModel(file.Name));
            if (MainFile == null) { throw new NotSupportedException("ShapesXR resource has no supported renderable file"); }
        }

        private static bool IsImage(string name) => new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga" }.Contains(Path.GetExtension(name).ToLowerInvariant());
        private static bool IsModel(string name) => new[] { ".glb", ".gltf", ".obj", ".fbx" }.Contains(Path.GetExtension(name).ToLowerInvariant());
        private static bool IsAudio(string name) => new[] { ".wav", ".mp3" }.Contains(Path.GetExtension(name).ToLowerInvariant());
    }
}
