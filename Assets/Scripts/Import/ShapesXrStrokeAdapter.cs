// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public static class ShapesXrStrokeAdapter
    {
        private static readonly Guid kTape = new Guid("3a0fc5ad-c934-4306-8063-60f5940cae06");
        // Protocol identities classify native objects; no preset meshes, curves or textures
        // are included. Unsupported profiles must not silently become constant-width tape.
        private static readonly Dictionary<Guid, string> kPresets = new Dictionary<Guid, string>
        {
            [kTape] = "Tape",
            [new Guid("e2172677-1a62-426c-8c78-7768207508b8")] = "Leaf",
            [new Guid("2411fc78-4742-4586-b8a3-7b5266ba3731")] = "Marker",
            [new Guid("ffc21f71-b084-4b71-bae7-9c0234a0f5a5")] = "Oil",
            [new Guid("2f630332-2f1f-47a2-88d7-8c5504bf2c5c")] = "Square",
            [new Guid("75e6d4f0-40e3-4563-b919-69e25bdd6221")] = "Tube",
            [new Guid("52e6a9ed-5910-41e2-a640-b079b046d68c")] = "Hull Hard",
            [new Guid("bfefc705-731b-48eb-8476-a0fa4a5fdde2")] = "Hull Smooth"
        };

        public static bool IsStroke(ShapesXrObjectState state)
        {
            if (state == null) { return false; }
            var properties = state.Entity.Properties;
            if (properties["a"] is JValue value && value.Value is byte[] bytes && bytes.Length == 16 &&
                kPresets.ContainsKey(new Guid(bytes))) { return true; }
            return properties["p"] is JArray && (properties["r"] is JArray || properties["w"] is JArray);
        }

        public static NativeRibbonData ReadRibbon(ShapesXrObjectState state, float unitsPerMetre)
        {
            if (state == null) { throw new ArgumentNullException(nameof(state)); }
            // Geometry is static object data in the native initializer. Snapshots select
            // placement/visibility/material state, not a replacement p/r/w sample stream.
            return ReadRibbon(state.Entity.Properties, state.Placement(), unitsPerMetre);
        }

        public static NativeRibbonData ReadRibbon(JObject properties, NativePlacement placement, float unitsPerMetre)
        {
            if (properties == null) { throw new ArgumentNullException(nameof(properties)); }
            if (placement == null) { throw new ArgumentNullException(nameof(placement)); }
            Guid preset = ShapesXrDocument.ReadGuid(properties["a"]);
            if (preset != kTape)
            {
                string name = kPresets.TryGetValue(preset, out string known) ? known : preset.ToString();
                throw new NotSupportedException($"ShapesXR {name} stroke profile");
            }
            if (!(properties["p"] is JArray points) || !(properties["r"] is JArray rotations) ||
                !(properties["w"] is JArray widths) || points.Count < 2 || rotations.Count != points.Count || widths.Count != points.Count)
            {
                throw new InvalidDataException("ShapesXR ribbon point, rotation and width arrays must have matching lengths of at least two");
            }
            var first = new float[points.Count][];
            var second = new float[points.Count][];
            float[] Array(Vector3 point) => new[] { point.X, point.Y, point.Z };
            for (int i = 0; i < points.Count; ++i)
            {
                if (!(points[i] is JArray p) || p.Count != 3 || !(rotations[i] is JArray r) || r.Count != 4)
                {
                    throw new InvalidDataException("Invalid ShapesXR ribbon position or rotation");
                }
                var center = new Vector3(Number(p[0]), Number(p[1]), Number(p[2]));
                var rotation = new Quaternion(Number(r[0]), Number(r[1]), Number(r[2]), Number(r[3]));
                if (!(rotation.LengthSquared() > 0) || float.IsInfinity(rotation.LengthSquared()))
                {
                    throw new InvalidDataException("Invalid ShapesXR ribbon quaternion");
                }
                // Saved width is the Tape profile's half-width in centimetres. Positions
                // remain in metres; each saved rotation orients its cross-section explicitly.
                Vector3 halfWidth = Vector3.Transform(Vector3.UnitX * (.01f * Number(widths[i])), Quaternion.Normalize(rotation));
                first[i] = Array(placement.TransformPoint(center - halfWidth, unitsPerMetre));
                second[i] = Array(placement.TransformPoint(center + halfWidth, unitsPerMetre));
            }
            return new NativeRibbonData { FirstEdge = first, SecondEdge = second };
        }

        private static float Number(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            {
                throw new InvalidDataException("Expected a numeric ShapesXR ribbon value");
            }
            float value = (float)token;
            if (float.IsNaN(value) || float.IsInfinity(value)) { throw new InvalidDataException("ShapesXR ribbon values must be finite"); }
            return value;
        }
    }
}
