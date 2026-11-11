// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TiltBrush
{
    public sealed class EditableModelMaterialData : PbrMaterialValues
    {
        [JsonExtensionData] public IDictionary<string, JToken> ExtraData { get; set; }

        public EditableModelMaterialData Clone() => new EditableModelMaterialData
        {
            Unlit = Unlit, Transparent = Transparent, Metallic = Metallic,
            Smoothness = Smoothness, Opacity = Opacity,
            ExtraData = ExtraData?.ToDictionary(p => p.Key, p => p.Value.DeepClone())
        };

        public override void Validate()
        {
            base.Validate();
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
