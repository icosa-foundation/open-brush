// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Text layout in TMP points and widget-local canvas units. GlyphTransform is an optional
    /// column-major affine matrix, applied before the widget's ordinary uniform placement.
    public sealed class TextWidgetLayout
    {
        public float FontSize { get; set; }
        public bool AutoSize { get; set; }
        public float FontSizeMin { get; set; }
        public float FontSizeMax { get; set; }
        public int FontStyle { get; set; }
        public int Alignment { get; set; }
        public int Wrapping { get; set; }
        public bool RichText { get; set; }
        public Vector2 Size { get; set; }
        public Vector2 Pivot { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float[] GlyphTransform { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }

        public void Validate()
        {
            foreach (float value in new[] { FontSize, FontSizeMin, FontSizeMax, Size.x, Size.y, Pivot.x, Pivot.y })
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) { throw new ArgumentException("Text layout values must be finite"); }
            }
            if (FontSize < 0 || FontSizeMin < 0 || FontSizeMax < FontSizeMin || Size.x < 0 || Size.y < 0)
            {
                throw new ArgumentException("Invalid text layout dimensions or font size range");
            }
            if (GlyphTransform == null) { return; }
            if (GlyphTransform.Length != 16) { throw new ArgumentException("Expected sixteen text glyph transform components"); }
            foreach (float value in GlyphTransform)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) { throw new ArgumentException("Text glyph transform must be finite"); }
            }
            if (GlyphTransform[3] != 0 || GlyphTransform[7] != 0 || GlyphTransform[11] != 0 || GlyphTransform[15] != 1)
            {
                throw new ArgumentException("Text glyph transform must be affine");
            }
        }
    }
}
