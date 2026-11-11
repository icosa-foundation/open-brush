// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public static class MaquetteMaterialAdapter
    {
        public static bool IsOpaqueModelMaterial(string id) => string.IsNullOrEmpty(id) ||
            id == "Lit" || id == "Unlit" || id == "Matte" || id == "Metallic" || id == "Plastic";

        public static bool IsSupportedModelMaterial(string id) => IsOpaqueModelMaterial(id) ||
            id == "TranslucentLit" || id == "TranslucentUnlit";

        /// Maps color, opacity and basic shading to standard glTF properties. Color is linear.
        /// Translucent depth writing and vertex lighting require separate fidelity reporting.
        /// An absent ID selects Maquette's first/default preset, Lit.
        public static JObject ModelMaterial(string id, float[] color)
        {
            if (color == null || color.Length != 4) { throw new ArgumentException("Expected four color components", nameof(color)); }
            foreach (float component in color)
            {
                if (float.IsNaN(component) || float.IsInfinity(component)) { throw new ArgumentException("Expected a finite model color", nameof(color)); }
            }
            if (!IsSupportedModelMaterial(id)) { return null; }
            bool translucent = id == "TranslucentLit" || id == "TranslucentUnlit";
            float smoothness = id == "Matte" || id == "TranslucentLit" ? 0 : id == "Metallic" ? .544f : id == "Plastic" ? .8f : .3f;
            var material = new JObject
            {
                ["name"] = $"Imported {(string.IsNullOrEmpty(id) ? "Lit" : id)}", ["alphaMode"] = translucent ? "BLEND" : "OPAQUE",
                ["pbrMetallicRoughness"] = new JObject
                {
                    ["baseColorFactor"] = new JArray(color[0], color[1], color[2], translucent ? color[3] : 1f),
                    ["metallicFactor"] = id == "Metallic" ? 1f : 0f,
                    ["roughnessFactor"] = 1 - smoothness
                }
            };
            if (id == "Unlit" || id == "TranslucentUnlit")
            { material["extensions"] = new JObject { ["KHR_materials_unlit"] = new JObject() }; }
            return material;
        }
    }
}
