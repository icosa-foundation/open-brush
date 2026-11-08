// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TiltBrush
{
    public sealed class EditableModelMaterialData
    {
        public bool Unlit { get; set; }
        public bool Transparent { get; set; }
        public float Metallic { get; set; }
        public float Smoothness { get; set; } = .5f;
        public float Opacity { get; set; } = 1f;
        [JsonExtensionData] public IDictionary<string, JToken> ExtraData { get; set; }

        public EditableModelMaterialData Clone() => new EditableModelMaterialData
        {
            Unlit = Unlit, Transparent = Transparent, Metallic = Metallic,
            Smoothness = Smoothness, Opacity = Opacity,
            ExtraData = ExtraData?.ToDictionary(p => p.Key, p => p.Value.DeepClone())
        };

        public void Validate()
        {
            foreach (float value in new[] { Metallic, Smoothness, Opacity })
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Material values must be between zero and one.");
            }
            if (ExtraData?.Count > 0) throw new NotSupportedException("Unfamiliar editable material settings");
        }

        public static EditableModelMaterialData FromGltf(JObject material) => new EditableModelMaterialData
        {
            Unlit = material["extensions"]?["KHR_materials_unlit"] != null,
            Transparent = (string)material["alphaMode"] == "BLEND",
            Metallic = (float?)material["pbrMetallicRoughness"]?["metallicFactor"] ?? 0f,
            Smoothness = 1f - ((float?)material["pbrMetallicRoughness"]?["roughnessFactor"] ?? 1f),
            Opacity = (float?)material["pbrMetallicRoughness"]?["baseColorFactor"]?[3] ?? 1f
        };
    }
}
