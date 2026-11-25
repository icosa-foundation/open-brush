// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;

namespace TiltBrush
{
    // Common editing values. Importers and documents retain their own material formats.
    public class PbrMaterialValues
    {
        public bool Unlit { get; set; }
        public bool Transparent { get; set; }
        public float Metallic { get; set; }
        public float Smoothness { get; set; } = .5f;
        public float Opacity { get; set; } = 1f;

        public static float SmoothnessFromRoughness(float roughness) => 1f - roughness;
        public static float RoughnessFromSmoothness(float smoothness) => 1f - smoothness;

        public virtual void Validate()
        {
            foreach (float value in new[] { Metallic, Smoothness, Opacity })
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Material values must be between zero and one.");
            }
        }
    }
}
