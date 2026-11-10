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
        internal static float Emission(RuntimeVoxDocument.RuntimeMaterial material) =>
            Mathf.Clamp01(Mathf.Clamp01(material.Weight ?? 1f) * Mathf.Max(0, material.Flux ?? 1f));
        internal static Dictionary<string, string> WithType(IReadOnlyDictionary<string, string> properties, int type)
        {
            var next = Copy(properties);
            next["_type"] = (RuntimeVoxDocument.MaterialType)type switch
            {
                RuntimeVoxDocument.MaterialType.Diffuse => "_diffuse",
                RuntimeVoxDocument.MaterialType.Metal => "_metal",
                RuntimeVoxDocument.MaterialType.Glass => "_glass",
                RuntimeVoxDocument.MaterialType.Emit => "_emit",
                RuntimeVoxDocument.MaterialType.Blend => "_blend",
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
            return next;
        }
        internal static Dictionary<string, string> WithValue(IReadOnlyDictionary<string, string> properties,
            PbrMaterialParameter.Property key, float value)
        {
            var next = Copy(properties);
            bool blend = Describe(properties).Type == RuntimeVoxDocument.MaterialType.Blend;
            switch (key)
            {
                case PbrMaterialParameter.Property.Glossiness: next["_rough"] = Number(1f - value); break;
                case PbrMaterialParameter.Property.Metallic: next[blend ? "_metal" : "_weight"] = Number(value); break;
                case PbrMaterialParameter.Property.Opacity:
                    if (blend) { next["_trans"] = Number(1f - Mathf.Clamp01(value)); break; }
                    next["_weight"] = "1"; next["_att"] = Number(Mathf.Max(.05f, value)); break;
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
            return next;
        }
        internal static Dictionary<string, string> WithEmission(IReadOnlyDictionary<string, string> properties, float value)
        {
            var next = Copy(properties);
            float weight = Mathf.Clamp01(Describe(properties).Weight ?? 1f);
            if (weight == 0) { weight = 1; next["_weight"] = "1"; }
            next["_flux"] = Number(value / weight);
            return next;
        }
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
