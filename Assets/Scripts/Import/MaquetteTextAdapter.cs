// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class MaquetteTextData
    {
        public string Text { get; internal set; }
        public float FontSize { get; internal set; }
        public int FontStyle { get; internal set; }
        public int Alignment { get; internal set; }
        public float[] BoxSize { get; internal set; }
        public float[] GlyphTransform { get; internal set; }
        public MaquettePlacement Placement { get; internal set; }
    }

    public static class MaquetteTextAdapter
    {
        public const float FontSizeMin = 1f;
        public const float FontSizeMax = 10000f;
        /// Source font size is an autosizing seed, not a fixed physical glyph height. The shipped
        /// prefab uses a centered TMP rectangle inside an upper-left-anchored one-unit text field.
        /// Read only layout facts; vendor font atlases and other prefab assets are not imported.
        public static MaquetteTextData Read(MaquetteState state, Func<string, string> textFile, float unitsPerMetre)
        {
            if (state?.Entity?.Kind != MaquetteEntityKind.Text) { throw new ArgumentException("Expected a Maquette text state", nameof(state)); }
            var rawState = state.Data["textEntityState"];
            if (rawState != null && rawState.Type != JTokenType.Null && !(rawState is JObject))
            {
                throw new InvalidDataException("Invalid Maquette text state");
            }
            var textState = rawState as JObject;
            var format = textState ?? state.Entity.Data;
            float fontSize = Number(format["FontSize"], 0);
            if (fontSize < 0) { throw new InvalidDataException("Negative Maquette text font size"); }
            float ratio = 10000f;
            if (fontSize > 1000) { ratio *= .1f; }
            else if (fontSize > 0 && fontSize < 10) { ratio *= 10f; }
            var scale = state.GetBuilderScale();
            if (scale[0] <= 1e-6f || scale[1] <= 1e-6f) { throw new NotSupportedException("Collapsed or reflected Maquette text box"); }
            var size = new[] { scale[0] * ratio, scale[1] * ratio };
            if (float.IsInfinity(size[0]) || float.IsInfinity(size[1])) { throw new InvalidDataException("Maquette text box dimensions overflow"); }
            var placement = new MaquettePlacement(state);
            return new MaquetteTextData
            {
                Text = textState != null ? (string)textState["Text"] ?? "" : textFile?.Invoke(state.EntityId) ?? "",
                FontSize = fontSize,
                FontStyle = Integer(format["FontStyle"], 0), Alignment = Integer(format["TextAlignmentOption"], 257),
                BoxSize = size, Placement = placement,
                GlyphTransform = placement.GetIntrinsicTransform(new[] { 1 / size[0], 1 / size[1], 1f },
                    new[] { .5f, -.5f, -.001f }, unitsPerMetre)
            };
        }

        private static float Number(JToken value, float fallback)
        {
            if (value == null) { return fallback; }
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) { throw new InvalidDataException("Expected numeric Maquette text parameter"); }
            float result = (float)value;
            if (float.IsNaN(result) || float.IsInfinity(result)) { throw new InvalidDataException("Nonfinite Maquette text parameter"); }
            return result;
        }

        private static int Integer(JToken value, int fallback)
        {
            if (value == null) { return fallback; }
            if (value.Type != JTokenType.Integer) { throw new InvalidDataException("Expected integer Maquette text parameter"); }
            return (int)value;
        }
    }
}
