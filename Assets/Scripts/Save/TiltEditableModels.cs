// Copyright 2022 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    // Definition/instance separation ported from the 3D Shapes branch.
    public class TiltEditableModels
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string FilePath { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AssetId { get; private set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TrTransform[] RawTransforms { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool[] PinStates { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public uint[] GroupIds { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int[] LayerIds { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Color[] Colors { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }

        [JsonIgnore]
        public Model.Location Location
        {
            get => AssetId != null ? Model.Location.Generated(AssetId) : Model.Location.File(FilePath);
            set
            {
                AssetId = value.IsGenerated() ? value.AssetId : null;
                FilePath = value.GetLocationType() == Model.Location.Type.LocalFile ? value.RelativePath : null;
            }
        }
    }
}
