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
    // preserves the other generator parameters and the existing order of modifiers.
    public static class ProceduralShapes
    {
        public static readonly string[] PresetNames =
        {
            "Box", "Panel", "Frame",
            "Cylinder", "Capsule"
        };

        public static bool IsPresetVisible(int preset) => preset >= 0 && preset < PresetNames.Length;

        // Axis gestures edit dimensions, not the final mesh scale.
        private static string AxisDimension(GeometryRecipeData recipe, GrabWidget.Axis axis)
        {
            if (recipe == null) { return null; }
            recipe.GetSteps(out var generator, out var modifiers);
            if (modifiers.Any(step => step.Type == "transform" || step.Type == "bend")) { return null; }
            if (generator.Type == "cylinder" || generator.Type == "capsule")
            {
                if (axis == GrabWidget.Axis.Y) { return generator.Type == "cylinder" ? "height" : "cylinderHeight"; }
                if (axis == GrabWidget.Axis.X || axis == GrabWidget.Axis.Z)
                {
                    return generator.Type == "cylinder" ? "radial" : "radius";
                }
                return null;
            }
            if (generator.Type != "panel" && generator.Type != "rectangle" && generator.Type != "frame") { return null; }
            switch (axis)
            {
                case GrabWidget.Axis.X: return "width";
                case GrabWidget.Axis.Y: return "height";
                case GrabWidget.Axis.Z: return generator.Type == "panel" ? "depth" : null;
                default: return null;
            }
        }

        public static bool CanResizeAxis(GeometryRecipeData recipe, GrabWidget.Axis axis)
            => AxisDimension(recipe, axis) != null;

        private static float MinimumDimension(JObject generator, string dimension)
        {
            var parameters = generator["Parameters"];
            string type = (string)generator["Type"];
            if (type == "cylinder" || type == "capsule") { return .0001f; }
            float edgeRadius = type == "panel" ? (float?)parameters?["edgeRadius"] ?? 0f : 0f;
            float minimum = Mathf.Max(.0001f, 2f * edgeRadius);
            if (dimension != "depth")
            {
                string radius = type == "panel" ? "cornerRadius" : "radius";
                minimum = Mathf.Max(minimum, 2f * ((float?)parameters?[radius] ?? 0f));
                if (type == "frame")
                {
                    minimum = Mathf.Max(minimum, 2f * ((float?)parameters?["borderWidth"] ?? .1f) + .0001f);
                }
            }
            return minimum;
        }

        public static float MinimumAxisResizeRatio(GeometryRecipeData recipe, GrabWidget.Axis axis)
        {
            string dimension = AxisDimension(recipe, axis);
            if (dimension == null) { return 0f; }
            var generator = (JObject)recipe.ToJson()["Generator"];
            var parameters = generator["Parameters"];
            if (dimension == "cylinderHeight")
            {
                float caps = 2f * ((float?)parameters?["radius"] ?? 1f);
                return caps / (((float?)parameters?["cylinderHeight"] ?? 2f) + caps);
            }
            if (dimension == "radial")
            {
                bool ellipse = EllipseEnabled(recipe.ToJson());
                float bottom = (float?)parameters?["bottomRadius"] ?? .5f;
                float top = (float?)parameters?["topRadius"] ?? .5f;
                if (ellipse && axis == GrabWidget.Axis.Z)
                {
                    bottom *= (float?)parameters?["bottomAspect"] ?? 1f;
                    top *= (float?)parameters?["topAspect"] ?? 1f;
                }
                float radius = Mathf.Max(bottom, top);
                return radius > 0f ? .0001f / radius : 0f;
            }
            float current = (float?)generator["Parameters"]?[dimension] ?? (dimension == "depth" ? .1f : 1f);
            return current > 0f ? MinimumDimension(generator, dimension) / current : 0f;
        }

        public static GeometryRecipeData ResizeAxis(GeometryRecipeData recipe, GrabWidget.Axis axis, float ratio)
        {
            if (!(ratio > 0f) || float.IsInfinity(ratio)) { throw new ArgumentOutOfRangeException(nameof(ratio)); }
            string dimension = AxisDimension(recipe, axis);
            if (dimension == null) { throw new NotSupportedException("This shape has no dimension mapped to that axis"); }
            JObject data = recipe.ToJson();
            var generator = (JObject)data["Generator"];
            var parameters = generator["Parameters"] as JObject;
            if (parameters == null)
            {
                parameters = new JObject();
                generator["Parameters"] = parameters;
            }
            if (dimension == "radial")
            {
                ratio = Mathf.Max(ratio, MinimumAxisResizeRatio(recipe, axis));
                bool ellipse = EllipseEnabled(data);
                foreach (string end in new[] { "bottom", "top" })
                {
                    float radius = (float?)parameters[$"{end}Radius"] ?? .5f;
                    float aspect = (float?)parameters[$"{end}Aspect"] ?? 1f;
                    if (!ellipse || axis == GrabWidget.Axis.X) { parameters[$"{end}Radius"] = radius * ratio; }
                    parameters[$"{end}Aspect"] = !ellipse ? 1f : aspect * (axis == GrabWidget.Axis.X ? 1f / ratio : ratio);
                }
                return new GeometryRecipeData(data);
            }
            if (dimension == "cylinderHeight")
            {
                float caps = 2f * ((float?)parameters["radius"] ?? 1f);
                float total = ((float?)parameters["cylinderHeight"] ?? 2f) + caps;
                float resizedHeight = total * ratio;
                if (float.IsInfinity(resizedHeight)) { throw new ArgumentOutOfRangeException(nameof(ratio)); }
                parameters[dimension] = Mathf.Max(0f, resizedHeight - caps);
                return new GeometryRecipeData(data);
            }
            float current = (float?)parameters[dimension] ?? (dimension == "depth" ? .1f : 1f);
            float resized = current * ratio;
            if (float.IsInfinity(resized)) { throw new ArgumentOutOfRangeException(nameof(ratio)); }
            parameters[dimension] = Mathf.Max(MinimumDimension(generator, dimension), resized);
            return new GeometryRecipeData(data);
        }

        private static bool EllipseEnabled(JObject data)
        {
            var parameters = data["Generator"]?["Parameters"];
            // Imported elliptical cylinders remain editable as ellipses, without
            // requiring authoring metadata in source documents.
            return (bool?)data["Authoring"]?["Ellipse"] ??
                (((float?)parameters?["bottomAspect"] ?? 1f) != 1f ||
                 ((float?)parameters?["topAspect"] ?? 1f) != 1f);
        }

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
            internal string Operator;
            internal bool FinalSmoothing;
            internal string Axis;
            internal bool Boolean;
            internal bool Ellipse;
            internal bool Taper;
            internal string RelativeTo;
            internal float ReferenceDefault;

            public float Read(GeometryRecipeData recipe)
            {
                JObject data = recipe.ToJson();
                if (Ellipse) { return EllipseEnabled(data) ? 1f : 0f; }
                if (FinalSmoothing)
                {
                    // Native imports can retain generator-specific seam smoothing. An authoring
                    // edit replaces those flags with the shared final smoothing setting.
                    return ((float?)data["PostProcessing"]?["AutoSmoothAngle"] > 0 ||
                        (bool?)data["Generator"]?["Parameters"]?["smooth"] == true ||
                        (bool?)data["Generator"]?["Parameters"]?["smoothSides"] == true) ? 1 : 0;
                }
                JObject step = FindStep(data, this);
                var parameters = step?["Parameters"];
                if (Operator != null && (bool?)parameters?["active"] == false) { return Default; }
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
                case 3: type = "cylinder"; p = new JObject { ["height"] = .3f, ["bottomRadius"] = .15f, ["topRadius"] = .15f }; break;
                case 4: type = "capsule"; p = new JObject { ["cylinderHeight"] = .2f, ["radius"] = .1f }; break;
                case 5: type = "conic"; p = new JObject { ["radius"] = .15f, ["majorAxis"] = .15f, ["minorAxis"] = .15f, ["centerOffset"] = 0f }; break;
                default: throw new ArgumentOutOfRangeException(nameof(preset));
            }
            return new GeometryRecipeData(new JObject
            {
                ["Version"] = 1,
                ["Generator"] = Step(type, p),
                ["Modifiers"] = new JArray(),
                ["PostProcessing"] = new JObject { ["AutoSmoothAngle"] = preset >= 3 ? 60f : 0f }
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
                        result.Add(Operation("operator", "amount", "Thickness", 0, .3f, "Shell"));
                    }
                    result.Add(Number("cornerSegments", "Corner segments", 1, 16, generator.Type == "panel" ? 8 : 4, true));
                    break;
                case "cylinder":
                    result.Add(new Parameter { Name = "taper", Label = "Taper", Min = -1, Max = 1, Taper = true });
                    result.Add(new Parameter { Name = "Ellipse", Label = "Ellipse", Min = 0, Max = 1,
                        Integer = true, Boolean = true, Ellipse = true });
                    if (EllipseEnabled(recipe.ToJson()))
                    {
                        result.Add(Ratio("topAspect", "bottomAspect", "Top ovalness", .05f, 4, 1, 1));
                    }
                    AddRevolution(result);
                    break;
                case "capsule":
                    result.Add(Ratio("cylinderHeight", "radius", "Body length", 0, 8, 2, 1));
                    AddRevolution(result);
                    result.Add(Number("capSegments", "Cap segments", 2, 32, 8, true));
                    break;
                case "conic":
                    // Retained for imports and saved recipes, without authoring controls.
                    return result;
            }

            result.Add(new Parameter { Name = "AutoSmoothAngle", Label = "Smooth surface",
                Min = 0, Max = 1, Integer = true, Boolean = true, FinalSmoothing = true });

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
            if (parameter.Ellipse)
            {
                var authoring = data["Authoring"] as JObject ?? new JObject();
                data["Authoring"] = authoring;
                bool enabled = value >= .5f;
                authoring["Ellipse"] = enabled;
                if (!enabled)
                {
                    var generator = (JObject)data["Generator"];
                    var parameters = generator["Parameters"] as JObject ?? new JObject();
                    generator["Parameters"] = parameters;
                    parameters["bottomAspect"] = 1f;
                    parameters["topAspect"] = 1f;
                }
                return new GeometryRecipeData(data);
            }
            if (parameter.FinalSmoothing)
            {
                if (!(data["PostProcessing"] is JObject postProcessing))
                {
                    postProcessing = new JObject();
                    data["PostProcessing"] = postProcessing;
                }
                postProcessing["AutoSmoothAngle"] = value >= .5f ? 60f : 0f;
                var generatorParameters = data["Generator"]?["Parameters"] as JObject;
                generatorParameters?.Remove("smooth");
                generatorParameters?.Remove("smoothSides");
                return new GeometryRecipeData(data);
            }
            var modifiers = data["Modifiers"] as JArray;
            if (modifiers == null)
            {
                modifiers = new JArray();
                data["Modifiers"] = modifiers;
            }
            JObject step = FindStep(data, parameter);
            if (step == null)
            {
                if (parameter.Operation == null) { throw new InvalidOperationException("Missing shape generator"); }
                var createdParameters = new JObject();
                if (parameter.Operator != null) { createdParameters["operation"] = parameter.Operator; }
                step = Step(parameter.Operation, createdParameters);
                // Work in generator coordinates, before any imported placement transform.
                int insert = modifiers.TakeWhile(op => (string)op["Type"] != "transform").Count();
                modifiers.Insert(insert, step);
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
            if (parameter.Operator != null) { p["active"] = true; }
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

            if (parameter.Operator == "Shell" && value == 0) { step.Remove(); }
            if (parameter.Operation == null)
            {
                for (int i = 0; i < modifiers.Count; ++i)
                {
                    if ((string)modifiers[i]["Type"] == "bend") { SetBendReference(data, (JObject)modifiers[i], i); }
                }
            }
            return new GeometryRecipeData(data);
        }

        private static void SetBendReference(JObject data, JObject bend, int index)
        {
            var prefix = (JObject)data.DeepClone();
            // Reference bounds belong to the geometry before final processing.
            prefix["PostProcessing"] = new JObject();
            prefix["Modifiers"] = new JArray(((JArray)data["Modifiers"]).Take(index)
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
            return (data["Modifiers"] as JArray)?.OfType<JObject>().LastOrDefault(op =>
                (string)op["Type"] == p.Operation &&
                (p.Operator == null || (string)op["Parameters"]?["operation"] == p.Operator) && (p.Axis == null || ((string)op["Parameters"]?["axis"] ?? "x") == p.Axis));
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

        private static Parameter Operation(string type, string name, string label, float min, float max, string operatorName = null)
        {
            var p = Number(name, label, min, max);
            p.Operation = type;
            p.Operator = operatorName;
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
