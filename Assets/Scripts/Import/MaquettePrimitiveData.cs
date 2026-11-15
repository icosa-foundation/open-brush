// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    /// Version-normalized asset identity. Geometry and material conversion are separate.
    public sealed class MaquettePrimitiveData
    {
        public string Type { get; }
        public string Path { get; }
        public string LoadOption { get; }
        public bool UsesLegacyMeshOrigin { get; }
        public bool IgnoresImageTint { get; }
        public bool UsesDefaultImageMaterial { get; }

        private MaquettePrimitiveData(string type, string path, string option, int version)
        {
            Type = type;
            Path = path;
            LoadOption = option;
            UsesLegacyMeshOrigin = version < 9;
            IgnoresImageTint = version < 10;
            UsesDefaultImageMaterial = version < 20;
        }

        /// Built-in paths come from the user's installation, never a redistributed asset table.
        public static MaquettePrimitiveData Read(JObject data, int version, Func<string, string> builtInPath)
        {
            if (data == null) { throw new ArgumentNullException(nameof(data)); }
            if (version < 1 || version > 21) { throw new NotSupportedException($"Unsupported Maquette primitive format: {version}"); }
            string type = (string)data["primitiveType"];
            string path = (string)data["primitivePath"] ?? "";
            if (string.IsNullOrEmpty(type)) { throw new InvalidDataException("Maquette primitive has no type"); }
            string option = version < 7 ? (type.Contains("GooglePoly") ? "Original" : "Maquette") : (string)data["assetLoadOption"];
            if (version < 5)
            {
                if (type == "IMAGE") { type = "ImageFile"; }
                else if (type == "OBJECT") { type = "ModelFile"; }
                else
                {
                    string shape = type == "FBX" ? LegacyFbxShape(path) : type;
                    if (shape != null) { path = builtInPath?.Invoke(shape); }
                    type = "ModelFile";
                }
            }
            else if (version == 5 && type == "UnityResource")
            {
                const string prefix = "Primitives/Maq_Primitive_";
                if (path.StartsWith(prefix, StringComparison.Ordinal)) { path = builtInPath?.Invoke(path.Substring(prefix.Length)); }
                type = "ModelFile";
            }
            return new MaquettePrimitiveData(type, path, option, version);
        }

        private static string LegacyFbxShape(string path)
        {
            foreach (string name in new[] { "Torus", "Tube", "Cone" })
            {
                if (path.Contains(name)) { return name; }
            }
            return null;
        }
    }
}
