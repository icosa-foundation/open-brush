// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    /// Maps native construction parameters to the common recipe, without generating vertices.
    /// globalScale is the source builder's effective positive scale. The returned geometry is
    /// source-local: the caller must apply the original object hierarchy exactly once afterward.
    public static class MaquetteGeometryAdapter
    {
        public static GeometryRecipeData Convert(JObject entity, float[] globalScale, int formatVersion = 21)
        {
            if (formatVersion < 1 || formatVersion > 21) { throw new NotSupportedException($"Unsupported Maquette procedural format: {formatVersion}"); }
            if (globalScale == null || globalScale.Length != 3) { throw new ArgumentException("Expected three scale components", nameof(globalScale)); }
            foreach (float component in globalScale)
            {
                if (!(component > 0) || float.IsInfinity(component)) { throw new NotSupportedException("Maquette procedural scale must be finite and positive"); }
            }
            var p = new Parameters(entity["parameters"]);
            string prefab = (string)entity["prefabPath"];
            var operations = new JArray();
            JObject generator;
            switch (prefab)
            {
                case "Procedural/MqProceduralMesh_RoundedCornerQuad":
                case "Procedural/MqProceduralMesh_RoundedCornerRim":
                    generator = Rectangle(p, globalScale, prefab.EndsWith("Rim"), formatVersion, operations);
                    break;
                case "Procedural/MqProceduralMesh_CylinderCone":
                    generator = Cylinder(p);
                    break;
                case "Procedural/MqProceduralMesh_ConicSection":
                    generator = Conic(p);
                    break;
                case "Procedural/MqProceduralMesh_Capsule":
                    generator = Capsule(p, globalScale, operations);
                    break;
                default: throw new NotSupportedException($"Unsupported Maquette procedural prefab: {prefab}");
            }
            if (p.Boolean("invert", false)) { operations.Add(Step("flip", new JObject())); }
            p.RequireAllRead();
            return new GeometryRecipeData(new JObject { ["Version"] = 1, ["Generator"] = generator, ["Operations"] = operations });
        }

        private static JObject Rectangle(Parameters p, float[] scale, bool frame, int version, JArray operations)
        {
            float width = p.Number("width", .5f), height = p.Number("height", .3f), thickness = p.Number("thickness", .1f);
            float w = width * scale[0], h = height * scale[1];
            if (w <= 0 || h <= 0 || thickness <= 0) { throw new NotSupportedException("Maquette rectangle dimensions must be positive"); }
            float radius = Math.Max(0, p.Number("radius", frame ? .04f : .01f));
            int sides = Clamp(p.Integer("sides", 8), 0, 25);
            bool square = sides == 0 || radius <= .0001f;
            var dimensions = new JObject { ["width"] = w, ["height"] = h, ["radius"] = square ? 0 : radius, ["cornerSegments"] = Math.Max(1, sides) };
            if (frame)
            {
                float border = p.Number("border_size", .02f);
                bool scaleBorder = p.Boolean("scale_border", false);
                // Before format 21 the outline described the opening, and the scale-border
                // flag had the opposite meaning. Express the same surface using an inward frame.
                if (version < 21 ? !scaleBorder : scaleBorder) { border *= Math.Max(scale[0], scale[1]); }
                if (version < 21)
                {
                    w += 2 * border;
                    h += 2 * border;
                    dimensions["width"] = w;
                    dimensions["height"] = h;
                }
                border = Math.Min(border, Math.Min(w, h) * .5f);
                if (border < 0 || (!square && border * 2 >= Math.Min(w, h)))
                {
                    throw new NotSupportedException("Collapsed rounded Maquette frame borders need separate compatibility handling");
                }
                dimensions["borderWidth"] = border;
                dimensions["radius"] = square ? 0 : radius + border;
            }
            operations.Add(Step("shell", new JObject { ["thickness"] = thickness * scale[2], ["symmetric"] = true }));
            float halfRoot = (float)Math.Sqrt(.5);
            operations.Add(Transform(rotation: new JArray(-halfRoot, 0, 0, halfRoot)));
            operations.Add(Transform(scale: new JArray(1 / scale[0], 1 / scale[1], 1 / scale[2])));
            float[] pivot = p.Vector("pivot", new[] { .5f, .5f, .5f });
            if (pivot[0] != .5f || pivot[1] != .5f || pivot[2] != .5f)
            {
                operations.Add(Transform(translation: new JArray((.5f - pivot[0]) * w / scale[0], (.5f - pivot[1]) * h / scale[1], (.5f - pivot[2]) * thickness)));
            }
            operations.Add(Step("autoSmooth", new JObject { ["angle"] = 60f }));
            return Step(frame ? "frame" : "rectangle", dimensions);
        }

        private static JObject Cylinder(Parameters p)
        {
            float[] bottom = p.Vector("bottom_scale", new[] { 1f, 1f });
            float[] top = p.Vector("top_scale", new[] { 1f, 1f });
            if (bottom[0] <= 0 || bottom[1] <= 0 || top[0] <= 0 || top[1] <= 0)
            {
                throw new NotSupportedException("Singular or reflected Maquette cylinder ends need separate compatibility handling");
            }
            return Step("cylinder", new JObject
            {
                ["sides"] = Clamp(p.Integer("sides", 32), 3, 100), ["height"] = 1f,
                ["bottomRadius"] = p.Number("radius_bottom", .6f) * bottom[0], ["bottomAspect"] = bottom[1] / bottom[0],
                ["topRadius"] = p.Number("radius_top", .4f) * top[0], ["topAspect"] = top[1] / top[0],
                ["topOffset"] = new JArray(p.Vector("top_offset", new[] { 0f, 0f })),
                ["topRotation"] = Euler(p.Vector("top_rotation", new[] { 0f, 0f, 0f })),
                ["sweepAngle"] = Clamp(p.Number("sweep_angle", 360), -360, 360), ["smoothSides"] = !p.Boolean("hard_edge", false)
            });
        }

        private static JObject Conic(Parameters p)
        {
            int curve = p.Integer("curve_type", 0);
            var names = new[] { "circle", "ellipse", "hyperbola", "parabola" };
            if (curve < 0 || curve >= names.Length) { throw new NotSupportedException($"Unknown Maquette conic curve: {curve}"); }
            float radius = p.Number("radius", .5f);
            float bottom = Clamp(p.Number("bottom_clip", 0), 0, 1), top = Clamp(p.Number("top_clip", 1), 0, 1);
            if (top <= bottom) { throw new NotSupportedException("Equal or reversed Maquette conic clips need separate compatibility handling"); }
            return Step("conic", new JObject
            {
                ["curve"] = names[curve], ["radius"] = curve == 0 ? radius : .5f,
                ["majorAxis"] = p.Number("major_axis", 1), ["minorAxis"] = p.Number("minor_axis", 1),
                ["centerOffset"] = p.Number("center_offset", .2f), ["bottomClip"] = bottom, ["topClip"] = top,
                ["sides"] = Clamp(p.Integer("sides", 32), 3, 100),
                ["verticalSegments"] = Clamp(p.Integer("vertical_segments", 16), 2, 50),
                ["sweepAngle"] = Clamp(p.Number("sweep_angle", 360), -360, 360),
                ["smooth"] = true, ["zeroSweep"] = "doubleSidedProfile"
            });
        }

        private static JObject Capsule(Parameters p, float[] scale, JArray operations)
        {
            float radius = p.Number("radius", .5f) * Math.Max(scale[0], scale[2]) * .5f;
            float height = Math.Max(p.Number("length", 1) * scale[1] - 2 * radius, .001f);
            float sweep = Clamp(p.Number("sweep_angle", 360), -360, 360);
            int sides = Clamp(p.Integer("sides", 12), 3, 100), segments = Clamp(p.Integer("end_segments", 6), 2, 50);
            bool round = p.Boolean("round_profile", false);
            float divisor = Math.Max(scale[0], scale[2]);
            JObject generator;
            if (sweep == 0)
            {
                generator = Step("conic", new JObject
                {
                    ["curve"] = "circle", ["radius"] = radius, ["bottomClip"] = .5f, ["topClip"] = 1f,
                    ["verticalSegments"] = segments, ["sides"] = sides, ["sweepAngle"] = 0f,
                    ["zeroSweep"] = "doubleSidedProfile", ["smooth"] = true
                });
                operations.Add(Transform(translation: new JArray(0, height * .5f, 0)));
            }
            else
            {
                generator = Step("capsule", new JObject
                {
                    ["radius"] = radius, ["cylinderHeight"] = height, ["sides"] = sides,
                    ["capSegments"] = segments, ["sweepAngle"] = sweep, ["smooth"] = true,
                    ["zeroSweep"] = "doubleSidedProfile"
                });
            }
            operations.Add(Transform(scale: new JArray(1 / (round ? scale[0] : divisor), 1 / scale[1], 1 / (round ? scale[2] : divisor))));
            return generator;
        }

        private static JObject Step(string type, JObject parameters) => new JObject { ["Type"] = type, ["Parameters"] = parameters };
        private static JObject Transform(JArray translation = null, JArray rotation = null, JArray scale = null)
        {
            var parameters = new JObject();
            if (translation != null) { parameters["translation"] = translation; }
            if (rotation != null) { parameters["rotation"] = rotation; }
            if (scale != null) { parameters["scale"] = scale; }
            return Step("transform", parameters);
        }

        private static JArray Euler(float[] degrees)
        {
            // Unity Euler convention: rotate Z, then X, then Y (qY * qX * qZ).
            double x = degrees[0] * Math.PI / 360, y = degrees[1] * Math.PI / 360, z = degrees[2] * Math.PI / 360;
            double sx = Math.Sin(x), cx = Math.Cos(x), sy = Math.Sin(y), cy = Math.Cos(y), sz = Math.Sin(z), cz = Math.Cos(z);
            return new JArray((float)(cy * sx * cz + sy * cx * sz), (float)(sy * cx * cz - cy * sx * sz),
                (float)(cy * cx * sz - sy * sx * cz), (float)(cy * cx * cz + sy * sx * sz));
        }

        private static int Clamp(int number, int min, int max) => Math.Max(min, Math.Min(max, number));
        private static float Clamp(float number, float min, float max) => Math.Max(min, Math.Min(max, number));

        private sealed class Parameters
        {
            private readonly Dictionary<string, string> m_Values = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly HashSet<string> m_Read = new HashSet<string>(StringComparer.Ordinal);
            public Parameters(JToken value)
            {
                if (value == null || value.Type == JTokenType.Null) { return; }
                if (!(value is JArray array)) { throw new InvalidDataException("Maquette procedural parameters must be Key/Value entries"); }
                foreach (var entry in array)
                {
                    string key = (string)entry["Key"];
                    if (string.IsNullOrEmpty(key) || entry["Value"]?.Type != JTokenType.String || m_Values.ContainsKey(key))
                    {
                        throw new InvalidDataException("Invalid or duplicate Maquette procedural parameter");
                    }
                    m_Values.Add(key, (string)entry["Value"]);
                }
            }

            private string Read(string name) { m_Read.Add(name); return m_Values.TryGetValue(name, out var value) ? value : null; }
            public float Number(string name, float fallback)
            {
                string text = Read(name);
                if (text == null) { return fallback; }
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                {
                    throw new InvalidDataException($"Invalid Maquette number: {name}");
                }
                return value;
            }

            public int Integer(string name, int fallback)
            {
                string text = Read(name);
                if (text == null) { return fallback; }
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) { throw new InvalidDataException($"Invalid Maquette integer: {name}"); }
                return value;
            }

            public bool Boolean(string name, bool fallback)
            {
                string text = Read(name);
                if (text == null) { return fallback; }
                if (!bool.TryParse(text, out bool value)) { throw new InvalidDataException($"Invalid Maquette boolean: {name}"); }
                return value;
            }

            public float[] Vector(string name, float[] fallback)
            {
                string text = Read(name);
                if (text == null) { return fallback; }
                JObject vector = JObject.Parse(text, new Newtonsoft.Json.Linq.JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                var result = new float[fallback.Length];
                for (int i = 0; i < result.Length; ++i)
                {
                    JToken component = vector["xyz"[i].ToString()];
                    if (component == null || (component.Type != JTokenType.Float && component.Type != JTokenType.Integer)) { throw new InvalidDataException($"Invalid Maquette vector: {name}"); }
                    result[i] = (float)component;
                    if (float.IsNaN(result[i]) || float.IsInfinity(result[i])) { throw new InvalidDataException($"Nonfinite Maquette vector: {name}"); }
                }
                return result;
            }

            public void RequireAllRead()
            {
                foreach (string name in m_Values.Keys)
                {
                    if (!m_Read.Contains(name)) { throw new NotSupportedException($"Unknown Maquette procedural parameter: {name}"); }
                }
            }
        }
    }
}
