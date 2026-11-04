// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TiltBrush.DocumentImport;
using UnityEngine;

namespace TiltBrush.MeshEditing
{
    // The authoring controls use the same recipes as native document import. Editing a field
    // preserves the other generator parameters and the existing order of operations.
    public static class ProceduralShapes
    {
        public static readonly string[] PresetNames =
        {
            "Box", "Panel", "Frame",
            "Cylinder", "Capsule", "Sphere"
        };

        public sealed class Parameter
        {
            public string Name;
            public string Label;
            public float Min;
            public float Max;
            public float Default;
            public bool Integer;
            public string[] Choices;
            internal string Operation;
            internal string Axis;
            internal bool Boolean;
            internal bool Taper;
            internal string RelativeTo;
            internal float ReferenceDefault;

            public float Read(GeometryRecipeData recipe)
            {
                JObject data = recipe.ToJson();
                JObject step = FindStep(data, this);
                var parameters = step?["Parameters"];
                if (Taper)
                {
                    float bottom = (float?)parameters?["bottomRadius"] ?? .5f;
                    float top = (float?)parameters?["topRadius"] ?? .5f;
                    float radius = Mathf.Max(bottom, top);
                    return radius > 0 ? (top - bottom) / radius : 0;
                }
                JToken value = parameters?[Name];
                if (Choices != null) { return value != null ? Array.IndexOf(Choices, (string)value) : Default; }
                if (Boolean) { return value != null ? ((bool)value ? 1 : 0) : Default; }
                float number = value != null ? (float)value : Default;
                return RelativeTo != null ? number / ((float?)parameters?[RelativeTo] ?? ReferenceDefault) : number;
            }

            public string Format(float value)
            {
                if (Choices != null)
                {
                    return Choices[Mathf.Clamp(Mathf.RoundToInt(value), 0, Choices.Length - 1)];
                }
                if (Boolean) { return value >= .5f ? "On" : "Off"; }
                return Integer ? Mathf.RoundToInt(value).ToString() : $"{value:0.###}";
            }
        }

        public static GeometryRecipeData CreateRecipe(int preset)
        {
            string type;
            JObject p;
            switch (preset)
            {
                case 0: type = "panel"; p = new JObject { ["width"] = .3f, ["height"] = .3f, ["depth"] = .3f }; break;
                case 1: type = "panel"; p = new JObject { ["width"] = .4f, ["height"] = .3f, ["depth"] = .02f, ["cornerRadius"] = .025f, ["widthSegments"] = 12, ["heightSegments"] = 12 }; break;
                case 2: type = "frame"; p = new JObject { ["width"] = .4f, ["height"] = .3f, ["borderWidth"] = .03f, ["radius"] = .025f }; break;
                case 3: type = "cylinder"; p = new JObject { ["height"] = .3f, ["bottomRadius"] = .15f, ["topRadius"] = .15f, ["smoothSides"] = true }; break;
                case 4: type = "capsule"; p = new JObject { ["cylinderHeight"] = .2f, ["radius"] = .1f, ["smooth"] = true }; break;
                case 5: type = "conic"; p = new JObject { ["radius"] = .15f, ["majorAxis"] = .15f, ["minorAxis"] = .15f, ["centerOffset"] = 0f, ["smooth"] = true }; break;
                default: throw new ArgumentOutOfRangeException(nameof(preset));
            }
            return new GeometryRecipeData(new JObject
            {
                ["Version"] = 1,
                ["Generator"] = Step(type, p),
                ["Operations"] = new JArray()
            });
        }

        public static List<Parameter> GetParameters(GeometryRecipeData recipe)
        {
            recipe.GetSteps(out var generator, out _);
            var result = new List<Parameter>();
            switch (generator.Type)
            {
                case "rectangle":
                case "frame":
                case "panel":
                    if (generator.Type == "panel")
                    {
                        result.Add(Number("cornerRadius", "Corner radius", 0, .5f));
                        result.Add(Number("edgeRadius", "Edge radius", 0, .25f));
                        result.Add(Number("widthSegments", "Width segments", 1, 32, 1, true));
                        result.Add(Number("heightSegments", "Height segments", 1, 32, 1, true));
                        result.Add(Number("edgeSegments", "Edge segments", 1, 16, 8, true));
                    }
                    else
                    {
                        result.Add(Number("radius", "Corner radius", 0, .5f));
                        if (generator.Type == "frame")
                        {
                            result.Add(Number("borderWidth", "Border width", .001f, .3f, .1f));
                        }
                        result.Add(Operation("shell", "thickness", "Thickness", 0, .3f));
                    }
                    result.Add(Number("cornerSegments", "Corner segments", 1, 16, generator.Type == "panel" ? 8 : 4, true));
                    break;
                case "cylinder":
                    result.Add(new Parameter { Name = "taper", Label = "Taper", Min = -1, Max = 1, Taper = true });
                    result.Add(Ratio("topAspect", "bottomAspect", "Top ovalness", .05f, 4, 1, 1));
                    AddRevolution(result);
                    result.Add(Toggle("smoothSides", "Smooth sides"));
                    break;
                case "capsule":
                    result.Add(Ratio("cylinderHeight", "radius", "Body length", 0, 8, 2, 1));
                    AddRevolution(result);
                    result.Add(Number("capSegments", "Cap segments", 2, 32, 8, true));
                    result.Add(Toggle("smooth", "Smooth surface"));
                    break;
                case "conic":
                    result.Add(Choice("curve", "Profile", "circle", "ellipse", "hyperbola", "parabola"));
                    result.Add(Number("centerOffset", "Profile offset", 0, 1, .2f));
                    result.Add(Number("bottomClip", "Bottom clip", 0, .99f));
                    result.Add(Number("topClip", "Top clip", .01f, 1, 1));
                    AddRevolution(result);
                    result.Add(Number("verticalSegments", "Vertical segments", 2, 48, 16, true));
                    result.Add(Toggle("smooth", "Smooth surface"));
                    break;
            }

            // Panels have controllable sampling for curved bends. Existing imported bends on
            // other generators remain editable without adding a new operation stack UI.
            JObject data = recipe.ToJson();
            foreach (string axis in new[] { "x", "y", "z" })
            {
                var bend = Operation("bend", "angle", $"Bend {axis.ToUpperInvariant()} (degrees)", -180, 180);
                bend.Axis = axis;
                if (generator.Type == "panel" || FindStep(data, bend) != null) { result.Add(bend); }
            }
            return result;
        }

        public static GeometryRecipeData WithValue(GeometryRecipeData recipe, Parameter parameter, float value)
        {
            JObject data = recipe.ToJson();
            var operations = data["Operations"] as JArray;
            if (operations == null)
            {
                operations = new JArray();
                data["Operations"] = operations;
            }
            JObject step = FindStep(data, parameter);
            if (step == null)
            {
                if (parameter.Operation == null) { throw new InvalidOperationException("Missing shape generator"); }
                step = Step(parameter.Operation, new JObject());
                // Work in generator coordinates, before any imported placement transform.
                int insert = operations.TakeWhile(op => (string)op["Type"] != "transform").Count();
                operations.Insert(insert, step);
                if (parameter.Operation == "bend")
                {
                    step["Parameters"]["axis"] = parameter.Axis;
                    SetBendReference(data, step, insert);
                }
            }
            var p = step["Parameters"] as JObject;
            if (p == null)
            {
                p = new JObject();
                step["Parameters"] = p;
            }
            if (parameter.Taper)
            {
                float radius = Mathf.Max((float?)p["bottomRadius"] ?? .5f, (float?)p["topRadius"] ?? .5f);
                value = Mathf.Clamp(value, -1, 1);
                p["bottomRadius"] = radius * (value > 0 ? 1 - value : 1);
                p["topRadius"] = radius * (value < 0 ? 1 + value : 1);
            }
            else if (parameter.RelativeTo != null)
            {
                p[parameter.Name] = value * ((float?)p[parameter.RelativeTo] ?? parameter.ReferenceDefault);
            }
            else if (parameter.Choices != null)
            {
                p[parameter.Name] = parameter.Choices[Mathf.Clamp(Mathf.RoundToInt(value), 0, parameter.Choices.Length - 1)];
            }
            else if (parameter.Boolean) { p[parameter.Name] = value >= .5f; }
            else if (parameter.Integer) { p[parameter.Name] = Mathf.RoundToInt(value); }
            else { p[parameter.Name] = value; }

            if (parameter.Operation == "shell" && value == 0) { step.Remove(); }
            if (parameter.Operation == null)
            {
                for (int i = 0; i < operations.Count; ++i)
                {
                    if ((string)operations[i]["Type"] == "bend") { SetBendReference(data, (JObject)operations[i], i); }
                }
            }
            return new GeometryRecipeData(data);
        }

        private static void SetBendReference(JObject data, JObject bend, int index)
        {
            var prefix = (JObject)data.DeepClone();
            prefix["Operations"] = new JArray(((JArray)data["Operations"]).Take(index)
                .Where(op => (string)op["Type"] != "bend").Select(op => op.DeepClone()));
            var poly = EditableModelManager.Evaluate(new GeometryRecipeData(prefix));
            var bounds = new Bounds(poly.Vertices[0].Position, Vector3.zero);
            foreach (var vertex in poly.Vertices) { bounds.Encapsulate(vertex.Position); }
            bend["Parameters"]["referenceCenter"] = new JArray(bounds.center.x, bounds.center.y, bounds.center.z);
            bend["Parameters"]["referenceSize"] = new JArray(bounds.size.x, bounds.size.y, bounds.size.z);
        }

        private static JObject FindStep(JObject data, Parameter p)
        {
            if (p.Operation == null) { return data["Generator"] as JObject; }
            return (data["Operations"] as JArray)?.OfType<JObject>().LastOrDefault(op =>
                (string)op["Type"] == p.Operation && (p.Axis == null || ((string)op["Parameters"]?["axis"] ?? "x") == p.Axis));
        }

        private static JObject Step(string type, JObject parameters) => new JObject { ["Type"] = type, ["Parameters"] = parameters };

        private static Parameter Number(string name, string label, float min, float max, float fallback = 0, bool integer = false)
            => new Parameter { Name = name, Label = label, Min = min, Max = max, Default = fallback, Integer = integer };

        private static Parameter Ratio(string name, string reference, string label, float min, float max, float fallback, float referenceFallback)
        {
            var parameter = Number(name, label, min, max, fallback);
            parameter.RelativeTo = reference;
            parameter.ReferenceDefault = referenceFallback;
            return parameter;
        }

        private static Parameter Operation(string type, string name, string label, float min, float max)
        {
            var p = Number(name, label, min, max);
            p.Operation = type;
            return p;
        }

        private static Parameter Toggle(string name, string label)
        {
            var p = Number(name, label, 0, 1, 0, true);
            p.Boolean = true;
            return p;
        }

        private static Parameter Choice(string name, string label, params string[] choices)
            => new Parameter { Name = name, Label = label, Min = 0, Max = choices.Length - 1, Integer = true, Choices = choices };

        private static void AddRevolution(List<Parameter> result)
        {
            result.Add(Number("sweepAngle", "Sweep (degrees)", 1, 360, 360));
            result.Add(Number("sides", "Sides", 3, 64, 24, true));
        }
    }
}
