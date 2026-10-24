// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public enum ShapesXrMaterialUsage { Panel, Image, Text }

    public sealed class ShapesXrMaterialData
    {
        public float[] Color { get; internal set; }
        public string Blend { get; internal set; }
        public bool Lit { get; internal set; }
        public bool Passthrough { get; internal set; }
        public string[] UnmappedProperties { get; internal set; }
    }

    public static class ShapesXrMaterialAdapter
    {
        public static ShapesXrMaterialData Read(ShapesXrDocument document, ShapesXrObjectState state,
            ShapesXrMaterialUsage usage)
        {
            var properties = new JObject();
            if (state.Properties["mid"] != null)
            {
                Guid id = ShapesXrDocument.ReadGuid(state.Properties["mid"]);
                if (id != Guid.Empty)
                {
                    if (!document.Entities.TryGetValue(id, out var material) || !material.Exists || material.Kind != 10)
                    {
                        throw new InvalidDataException($"Missing ShapesXR material {id}");
                    }
                    properties = document.ResolveProperties(id, state.StateId);
                }
            }
            return Read(properties, UnitNumber(state.Properties["obj-vis"], 1), usage);
        }

        public static ShapesXrMaterialData Read(JObject properties, float visibility, ShapesXrMaterialUsage usage)
        {
            if (properties == null) { throw new ArgumentNullException(nameof(properties)); }
            if (float.IsNaN(visibility) || visibility < 0 || visibility > 1) { throw new InvalidDataException("Invalid ShapesXR visibility"); }
            float gray = usage == ShapesXrMaterialUsage.Text ? 155f / 255 : 1;
            var color = new[] { gray, gray, gray, 1f };
            if (properties["m-c"] != null)
            {
                if (!(properties["m-c"] is JArray rgba) || rgba.Count != 4) { throw new InvalidDataException("Invalid ShapesXR material color"); }
                for (int i = 0; i < color.Length; ++i)
                {
                    if (rgba[i].Type != JTokenType.Integer || (int)rgba[i] < 0 || (int)rgba[i] > 255)
                    {
                        throw new InvalidDataException("Invalid ShapesXR material color component");
                    }
                    color[i] = (int)rgba[i] / 255f;
                }
            }
            float opacity = UnitNumber(properties["m-o"], 1) * visibility;
            int alpha = EnumValue(properties["m-tam"], usage == ShapesXrMaterialUsage.Image ? 2 : 0, 2);
            int blend = EnumValue(properties["m-bm"], 0, 2);
            // Fading either a material or an object requires blending even when the saved
            // texture-alpha mode is opaque or masked. Text uses its separate opacity directly.
            if (opacity < 1) { alpha = 2; }
            color[3] = usage == ShapesXrMaterialUsage.Text ? opacity : alpha == 0 ? 1 : color[3] * opacity;
            bool passthrough = false;
            if (properties["m-pt"] != null)
            {
                if (properties["m-pt"].Type != JTokenType.Boolean) { throw new InvalidDataException("Invalid ShapesXR passthrough flag"); }
                passthrough = (bool)properties["m-pt"];
            }
            return new ShapesXrMaterialData
            {
                Color = color, Passthrough = passthrough,
                Lit = EnumValue(properties["m-st"], usage == ShapesXrMaterialUsage.Image ? 1 : 0, 1) == 0,
                Blend = alpha == 0 ? "opaque" : alpha == 1 ? "cutout" : blend == 1 ? "additive" : blend == 2 ? "multiply" : "alphaBlend",
                UnmappedProperties = properties.Properties().Where(property => property.Name.StartsWith("m-", StringComparison.Ordinal) &&
                    !new[] { "m-c", "m-o", "m-st", "m-bm", "m-tam", "m-pt" }.Contains(property.Name)).Select(property => property.Name).ToArray()
            };
        }

        private static float UnitNumber(JToken token, float fallback)
        {
            if (token == null) { return fallback; }
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) { throw new InvalidDataException("Expected a numeric ShapesXR opacity"); }
            float value = (float)token;
            if (float.IsNaN(value) || value < 0 || value > 1) { throw new InvalidDataException("Invalid ShapesXR opacity"); }
            return value;
        }

        private static int EnumValue(JToken token, int fallback, int maximum)
        {
            if (token == null) { return fallback; }
            if (token.Type != JTokenType.Integer || (int)token < 0 || (int)token > maximum) { throw new InvalidDataException("Invalid ShapesXR material mode"); }
            return (int)token;
        }
    }
}
