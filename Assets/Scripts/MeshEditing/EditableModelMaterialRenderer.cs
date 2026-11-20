// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Linq;
using UnityEngine;

namespace TiltBrush.MeshEditing
{
    public static class EditableModelMaterialRenderer
    {
        public static EditableModelMaterialData Capture(Material material, Color color) => new EditableModelMaterialData
        {
            Unlit = material.shader.name.IndexOf("unlit", StringComparison.OrdinalIgnoreCase) >= 0,
            Transparent = color.a < 1f,
            Metallic = material.HasProperty("_Metallic") ? Mathf.Clamp01(material.GetFloat("_Metallic")) : 0f,
            Smoothness = material.HasProperty("_Glossiness") ? Mathf.Clamp01(material.GetFloat("_Glossiness"))
                : material.HasProperty("_Smoothness") ? Mathf.Clamp01(material.GetFloat("_Smoothness")) : .5f,
            Opacity = Mathf.Clamp01(color.a)
        };

        public static void Apply(Material material, EditableModelMaterialData settings, Color color, Material fallback = null)
        {
            settings?.Validate();
            var template = settings == null ? fallback : Resources.Load<Material>(
                $"ShapeMaterials/{(settings.Unlit ? "Unlit" : "Lit")}{(settings.Transparent ? "Transparent" : "Opaque")}");
            if (template == null) throw new InvalidOperationException("Missing procedural material template");
            string[] keywords = material.shaderKeywords;
            material.shader = template.shader;
            material.CopyPropertiesFromMaterial(template);
            material.shaderKeywords = material.shaderKeywords.Union(keywords).ToArray();
            if (settings != null) PbrMaterialProperties.Apply(material, settings, color);
            else PbrMaterialProperties.ApplyColor(material, color);
        }
    }
}
