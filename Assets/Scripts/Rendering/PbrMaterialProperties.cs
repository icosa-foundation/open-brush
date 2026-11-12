// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using UnityEngine;

namespace TiltBrush
{
    public static class PbrMaterialProperties
    {
        // Shader selection, material ownership and mesh updates belong to each renderer.
        public static void Apply(Material material, PbrMaterialValues values, Color color)
        {
            values.Validate();
            color.a = values.Transparent ? values.Opacity : 1f;
            SetFloat(material, "_Metallic", values.Metallic);
            SetFloat(material, "_MetallicFactor", values.Metallic);
            SetFloat(material, "_Glossiness", values.Smoothness);
            SetFloat(material, "_Smoothness", values.Smoothness);
            SetFloat(material, "_RoughnessFactor", PbrMaterialValues.RoughnessFromSmoothness(values.Smoothness));
            ApplyColor(material, color);
        }

        public static void ApplyColor(Material material, Color color)
        {
            foreach (string property in new[] { "_Color", "_BaseColor", "_BaseColorFactor" })
                if (material.HasProperty(property)) material.SetColor(property, color);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
