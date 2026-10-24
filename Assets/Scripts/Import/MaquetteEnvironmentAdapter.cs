// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class MaquetteFogData
    {
        public float[] Color { get; internal set; }
        public float Density { get; internal set; }
        public bool Enabled { get; internal set; }

        public float ToCanvasDensity(float unitsPerMetre, float relativeCanvasScale)
        {
            if (!(unitsPerMetre > 0) || float.IsInfinity(unitsPerMetre) || !(relativeCanvasScale > 0) || float.IsInfinity(relativeCanvasScale))
            {
                throw new ArgumentException("Invalid fog unit or canvas scale conversion");
            }
            float density = Enabled ? (float)(Density / ((double)unitsPerMetre * relativeCanvasScale)) : 0;
            if (float.IsInfinity(density)) { throw new InvalidDataException("Converted fog density exceeds supported range"); }
            return density;
        }
    }

    public sealed class MaquetteEnvironmentData
    {
        public string SceneId { get; internal set; }
        public float[] SunColor { get; internal set; }
        public float[] SunRotation { get; internal set; }
        public float SunIntensity { get; internal set; }
        public bool Shadows { get; internal set; }
        public JObject Lighting { get; internal set; }
    }

    public static class MaquetteEnvironmentAdapter
    {
        public static MaquetteEnvironmentData Read(MaquetteDocument document, MaquetteScene scene)
        {
            var source = ReadSource(document, scene);
            scene = source.Scene;
            var lighting = source.Lighting;
            if (lighting == null) { return null; }
            float intensity = document.Version < 12 && scene.Collection != "stages" ? 1.25f : Number(lighting["SunIntensity"]);
            if (intensity < 0) { throw new InvalidDataException("Negative Maquette sun intensity"); }
            float[] color = Components(lighting["SunColor"], "rgba");
            float[] rotation = Components(lighting["SunDirection"], "xyzw");
            var quaternion = new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
            if (!(quaternion.LengthSquared() > 0) || float.IsInfinity(quaternion.LengthSquared()))
            {
                throw new InvalidDataException("Invalid Maquette sun rotation");
            }
            quaternion = Quaternion.Normalize(quaternion);
            return new MaquetteEnvironmentData
            {
                SceneId = scene.Id, Lighting = (JObject)lighting.DeepClone(), SunColor = color,
                SunRotation = new[] { quaternion.X, quaternion.Y, quaternion.Z, quaternion.W }, SunIntensity = intensity,
                Shadows = document.Version < 13 && scene.Collection != "stages" || Boolean(lighting["ShadowEnabled"], false)
            };
        }

        public static MaquetteFogData ReadFog(MaquetteDocument document, MaquetteScene scene)
        {
            var source = ReadSource(document, scene);
            if (source.Lighting == null) { return null; }
            var lighting = source.Lighting;
            float density = lighting["FogDensity"] == null ? 0 : Number(lighting["FogDensity"]);
            if (density < 0) { throw new InvalidDataException("Negative Maquette fog density"); }
            return new MaquetteFogData
            {
                Color = lighting["FogColor"] == null ? new float[4] : Components(lighting["FogColor"], "rgba"),
                Density = density,
                Enabled = document.Version < 13 && source.Scene.Collection != "stages" || Boolean(lighting["FogEnabled"], false)
            };
        }

        private static (MaquetteScene Scene, JObject Lighting) ReadSource(MaquetteDocument document, MaquetteScene scene)
        {
            if (document == null) { throw new ArgumentNullException(nameof(document)); }
            if (scene == null || !document.Scenes.Contains(scene)) { throw new ArgumentException("Expected a scene from this document", nameof(scene)); }
            // Master content is always composed into a slide. Environment inheritance has
            // its own switch, introduced in v17, and replaces the entire lighting record.
            if (scene.Collection == "slides" && document.Version >= 17 &&
                Boolean(document.Sections["scenes.json"]["applyMasterStageEnvironment"], false))
            {
                scene = document.Scenes.FirstOrDefault(value => value.Collection == "stages") ?? scene;
            }
            var raw = scene.Data["sceneSettings"]?["Environment"]?["Lighting"];
            if (raw == null || raw.Type == JTokenType.Null) { return (scene, null); }
            if (!(raw is JObject lighting)) { throw new InvalidDataException("Invalid Maquette environment lighting"); }
            return (scene, lighting);
        }

        private static float[] Components(JToken token, string names)
        {
            if (!(token is JObject)) { throw new InvalidDataException("Missing Maquette environment color or rotation"); }
            return names.Select(name => Number(token[name.ToString()])).ToArray();
        }

        private static float Number(JToken token)
        {
            if (token == null || token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
            {
                throw new InvalidDataException("Expected a numeric Maquette environment value");
            }
            float value = (float)token;
            if (float.IsNaN(value) || float.IsInfinity(value)) { throw new InvalidDataException("Maquette environment values must be finite"); }
            return value;
        }

        private static bool Boolean(JToken token, bool fallback)
        {
            if (token == null) { return fallback; }
            if (token.Type != JTokenType.Boolean) { throw new InvalidDataException("Expected a Maquette environment flag"); }
            return (bool)token;
        }
    }
}
