// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class ShapesXrTextData
    {
        public string Text { get; internal set; }
        public float FontSize { get; internal set; }
        public bool AutoSize { get; internal set; }
        public int FontStyle { get; internal set; }
        public int Alignment { get; internal set; }
        public float[] BoxSize { get; internal set; }
        public float[] GlyphTransform { get; internal set; }
        public float[] Position { get; internal set; }
        public NativePlacement Placement { get; internal set; }
    }

    public static class ShapesXrTextAdapter
    {
        public const string FontResource = "Fonts & Materials/LiberationSans SDF";
        public const float FontSizeMin = .01f;
        public const float FontSizeMax = 72f;

        public static ShapesXrTextData Read(ShapesXrObjectState state, float unitsPerMetre)
        {
            var properties = state?.Properties ?? throw new ArgumentNullException(nameof(state));
            if (properties["obj-tx"]?.Type != JTokenType.String) { throw new InvalidDataException("Missing ShapesXR text"); }
            if (!(properties["obj-s"] is JArray size) || size.Count != 3) { throw new InvalidDataException("Missing ShapesXR text box size"); }
            float width = Number(size[0]), height = Number(size[1]);
            if (width <= 0 || height <= 0) { throw new InvalidDataException("ShapesXR text box dimensions must be positive"); }
            float fontSize = Number(properties["obj-ts"]);
            if (fontSize < 0) { throw new InvalidDataException("Negative ShapesXR text font size"); }
            bool autoSize = fontSize < .005f;
            var placement = state.Placement();
            var position = new Vector3(placement.Position[0], placement.Position[1], placement.Position[2]);
            float depth = .025f * Math.Min(width, height);
            if (autoSize)
            {
                // Native auto-fit boxes are centered at the authored pose. The fixed-size case
                // keeps the upper-left pivot. Its world-space anchor correction excludes scale.
                var rotation = new Quaternion(placement.Rotation[0], placement.Rotation[1], placement.Rotation[2], placement.Rotation[3]);
                position += Vector3.Transform(new Vector3(-width / 2, height / 2, depth), rotation);
            }
            return new ShapesXrTextData
            {
                Text = (string)properties["obj-tx"], FontSize = fontSize, AutoSize = autoSize,
                FontStyle = Integer(properties["obj-ty"], 0),
                Alignment = Integer(properties["obj-ta"], 1) | Integer(properties["obj-tva"], 256),
                BoxSize = new[] { width, height }, Placement = placement,
                Position = new[] { position.X, position.Y, position.Z },
                GlyphTransform = placement.GetIntrinsicTransform(new[] { 1f, 1f, 1f }, new[] { 0f, 0f, -depth }, unitsPerMetre)
            };
        }

        private static float Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
            {
                throw new InvalidDataException("Expected a numeric ShapesXR text value");
            }
            float value = (float)token;
            if (float.IsNaN(value) || float.IsInfinity(value)) { throw new InvalidDataException("ShapesXR text values must be finite"); }
            return value;
        }

        private static int Integer(JToken token, int fallback)
        {
            if (token == null) { return fallback; }
            if (token.Type != JTokenType.Integer) { throw new InvalidDataException("Expected a ShapesXR text enum value"); }
            return (int)token;
        }
    }
}
