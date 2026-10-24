// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using Newtonsoft.Json;

namespace TiltBrush
{
    public enum ImageBlendMode { Cutout = 0, Opaque = 1, AlphaBlend = 2, Additive = 3 }

    /// Intrinsic image geometry and appearance, before the ordinary widget pose/size.
    /// Transform is column-major affine. Tint contains RGB; alpha uses the existing Opacities index.
    public sealed class ImageWidgetLayout
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float[] Transform { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float[] Tint { get; set; }
        public ImageBlendMode BlendMode { get; set; }

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(ImageBlendMode), BlendMode)) { throw new ArgumentException("Unsupported image blend mode"); }
            if (Tint != null)
            {
                if (Tint.Length != 3) { throw new ArgumentException("Expected three image tint components"); }
                Finite(Tint);
            }
            if (Transform == null) { return; }
            if (Transform.Length != 16) { throw new ArgumentException("Expected sixteen image transform components"); }
            Finite(Transform);
            if (Transform[3] != 0 || Transform[7] != 0 || Transform[11] != 0 || Transform[15] != 1)
            {
                throw new ArgumentException("Image geometry transform must be affine");
            }
        }

        private static void Finite(float[] values)
        {
            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) { throw new ArgumentException("Image layout values must be finite"); }
            }
        }
    }
}
