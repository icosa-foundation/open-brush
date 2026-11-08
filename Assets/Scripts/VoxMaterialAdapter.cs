// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using UnityEngine;

namespace TiltBrush
{
    internal static class VoxMaterialAdapter
    {
        public static PbrMaterialValues Read(RuntimeVoxDocument.RuntimeMaterial material)
        {
            float weight = Mathf.Clamp01(material.Weight ?? 1f);
            bool glass = material.Type == RuntimeVoxDocument.MaterialType.Glass;
            return new PbrMaterialValues
            {
                Transparent = glass,
                Metallic = material.Type == RuntimeVoxDocument.MaterialType.Metal ? weight : 0f,
                Smoothness = PbrMaterialValues.SmoothnessFromRoughness(Mathf.Clamp01(material.Roughness ?? .1f)),
                // Preserve the existing approximation; native attenuation and IOR stay in the VOX document.
                Opacity = glass ? Mathf.Clamp(1f - weight * (1f - Mathf.Clamp01(material.Attenuation ?? .15f)), .05f, 1f) : 1f
            };
        }
    }
}
