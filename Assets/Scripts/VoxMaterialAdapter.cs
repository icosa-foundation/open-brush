// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    internal static class VoxMaterialAdapter
    {
        internal static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> properties) =>
            properties?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, string>();
        internal static RuntimeVoxDocument.RuntimeMaterial Describe(IReadOnlyDictionary<string, string> properties) =>
            new RuntimeVoxDocument.RuntimeMaterial(1, properties ?? new Dictionary<string, string>());
        public static PbrMaterialValues Read(RuntimeVoxDocument.RuntimeMaterial material)
        {
            float weight = Mathf.Clamp01(material.Weight ?? 1f);
            bool glass = material.Type == RuntimeVoxDocument.MaterialType.Glass;
            bool blend = material.Type == RuntimeVoxDocument.MaterialType.Blend;
            // Our surface shader approximates transmission with alpha blending.
            // Keep MATL _alpha separate: it is not the palette's RGBA alpha.
            float opacity = blend ? Mathf.Clamp(1f - Mathf.Clamp01(material.Transmission ?? 0f), .05f, 1f) :
                glass ? Mathf.Clamp(1f - weight * (1f - Mathf.Clamp01(material.Attenuation ?? .15f)), .05f, 1f) : 1f;
            return new PbrMaterialValues
            {
                Transparent = glass || blend && opacity < 1f,
                Metallic = blend ? Mathf.Clamp01(material.Metallic ?? 0f) :
                    material.Type == RuntimeVoxDocument.MaterialType.Metal ? weight : 0f,
                Smoothness = PbrMaterialValues.SmoothnessFromRoughness(Mathf.Clamp01(material.Roughness ?? .1f)),
                Opacity = opacity
            };
        }
        internal static float Emission(RuntimeVoxDocument.RuntimeMaterial material)
        {
            if (material.Type == RuntimeVoxDocument.MaterialType.Emit)
                return Mathf.Clamp01(Mathf.Clamp01(material.Weight ?? 1f) * Mathf.Max(0, material.Flux ?? 1f));
            return material.Type == RuntimeVoxDocument.MaterialType.Blend
                ? Mathf.Clamp01(Mathf.Max(0, material.Emission ?? 0f) * Mathf.Max(0, material.Flux ?? 1f)) : 0f;
        }
        private static Dictionary<string, string> AsBlend(RuntimeVoxDocument.RuntimeMaterial material)
        {
            var next = Copy(material.Properties);
            if (material.Type == RuntimeVoxDocument.MaterialType.Blend) return next;
            var values = Read(material);
            next["_type"] = "_blend";
            next["_metal"] = Number(values.Metallic);
            next["_trans"] = Number(1f - values.Opacity);
            float flux = Mathf.Max(0, material.Flux ?? 1f);
            if (flux == 0) { flux = 1; next["_flux"] = "1"; }
            // Preserve the visible emission, including zero when another source type
            // retains inactive emission settings from an earlier material edit.
            next["_emit"] = Number(Emission(material) / flux);
            return next;
        }
        internal static Dictionary<string, string> WithValue(IReadOnlyDictionary<string, string> properties,
            PbrMaterialParameter.Property key, float value)
        {
            var material = Describe(properties);
            var values = Read(material);
            var next = Copy(properties);
            value = Mathf.Clamp01(value);
            switch (key)
            {
                case PbrMaterialParameter.Property.Glossiness:
                    if (Mathf.Approximately(value, values.Smoothness)) return next;
                    next["_rough"] = Number(1f - value); break;
                case PbrMaterialParameter.Property.Metallic:
                    if (Mathf.Approximately(value, values.Metallic)) return next;
                    if (material.Type == RuntimeVoxDocument.MaterialType.Metal) next["_weight"] = Number(value);
                    else { next = AsBlend(material); next["_metal"] = Number(value); }
                    break;
                case PbrMaterialParameter.Property.Opacity:
                    value = Mathf.Max(.05f, value);
                    if (Mathf.Approximately(value, values.Opacity)) return next;
                    if (material.Type == RuntimeVoxDocument.MaterialType.Glass)
                    {
                        next["_weight"] = "1"; next["_att"] = Number(value);
                    }
                    else { next = AsBlend(material); next["_trans"] = Number(1f - value); }
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
            return next;
        }
        internal static Dictionary<string, string> WithEmission(IReadOnlyDictionary<string, string> properties, float value)
        {
            var material = Describe(properties);
            var next = Copy(properties);
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, Emission(material))) return next;
            if (material.Type == RuntimeVoxDocument.MaterialType.Emit)
            {
                float weight = Mathf.Clamp01(material.Weight ?? 1f);
                if (weight == 0) { weight = 1; next["_weight"] = "1"; }
                next["_flux"] = Number(value / weight);
            }
            else
            {
                next = AsBlend(material);
                float flux = Mathf.Max(0, material.Flux ?? 1f);
                if (flux == 0) { flux = 1; next["_flux"] = "1"; }
                next["_emit"] = Number(value / flux);
            }
            return next;
        }
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
