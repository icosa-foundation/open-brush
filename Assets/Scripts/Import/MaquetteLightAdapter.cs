// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class MaquetteLightData
    {
        public bool IsSpot { get; internal set; }
        public float Range { get; internal set; }
        public float Intensity { get; internal set; }
        public float SpotAngle { get; internal set; }
        public bool Enabled { get; internal set; }
        public bool Shadows { get; internal set; }
        public MaquettePlacement Placement { get; internal set; }
    }

    public static class MaquetteLightAdapter
    {
        public static MaquetteLightData Read(MaquetteState state, float unitsPerMetre)
        {
            if (state?.Entity?.Kind != MaquetteEntityKind.Light) { throw new ArgumentException("Expected a Maquette light state", nameof(state)); }
            string type = (string)state.Entity.Data["LightType"];
            if (type != "Point" && type != "Spotlight") { throw new NotSupportedException($"Unsupported Maquette light type: {type}"); }
            var raw = state.Data["lightEntityState"];
            if (raw != null && raw.Type != JTokenType.Null && !(raw is JObject)) { throw new InvalidDataException("Invalid Maquette light state"); }
            var data = raw as JObject;
            float range = Number(data?["Radius"], 10), intensity = Number(data?["Intensity"], 1), angle = Number(data?["SpotAngle"], 60);
            if (range < 0 || intensity < 0 || (type == "Spotlight" && (angle <= 0 || angle >= 180)))
            {
                throw new InvalidDataException("Invalid Maquette light range, intensity or spot angle");
            }
            if (!(unitsPerMetre > 0) || float.IsInfinity(unitsPerMetre) || float.IsInfinity(range * unitsPerMetre))
            {
                throw new ArgumentException("Invalid light unit conversion", nameof(unitsPerMetre));
            }
            // Radius is already the source light's world-space range. Its state restore assigns
            // this value directly; multiplying ancestor builder scale here would apply it twice.
            return new MaquetteLightData
            {
                IsSpot = type == "Spotlight", Range = range * unitsPerMetre, Intensity = intensity, SpotAngle = angle,
                Enabled = Boolean(data?["TurnedOn"], true), Shadows = Boolean(data?["IsShadowEnabled"], true),
                Placement = new MaquettePlacement(state)
            };
        }

        private static float Number(JToken token, float fallback)
        {
            if (token == null) { return fallback; }
            if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer) { throw new InvalidDataException("Expected a numeric Maquette light value"); }
            float value = (float)token;
            if (float.IsNaN(value) || float.IsInfinity(value)) { throw new InvalidDataException("Maquette light values must be finite"); }
            return value;
        }

        private static bool Boolean(JToken token, bool fallback)
        {
            if (token == null) { return fallback; }
            if (token.Type != JTokenType.Boolean) { throw new InvalidDataException("Expected a Maquette light boolean"); }
            return (bool)token;
        }
    }
}
