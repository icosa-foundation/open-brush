// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class GeometryStepData
    {
        public string Type { get; }
        public IReadOnlyDictionary<string, object> Parameters { get; }
        internal GeometryStepData(string type, Dictionary<string, object> parameters) { Type = type; Parameters = parameters; }
    }

    /// The shared, vendor-independent geometry wire format. Unknown data survives reading/writing;
    /// evaluation is a separate, strict operation. Vectors are float arrays at this engine-free boundary.
    [JsonConverter(typeof(GeometryRecipeDataConverter))]
    public sealed class GeometryRecipeData
    {
        private readonly JObject m_Data;
        public int Version { get; }
        public JObject ToJson() => (JObject)m_Data.DeepClone();

        public GeometryRecipeData(JObject data)
        {
            m_Data = (JObject)(data ?? throw new ArgumentNullException(nameof(data))).DeepClone();
            Version = ReadInteger(m_Data["Version"], "Version");
            if (Version < 1) { throw new InvalidDataException("Geometry recipe Version must be positive"); }
        }

        public static GeometryRecipeData Parse(string json)
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
            {
                var result = new GeometryRecipeData(LoadObject(reader));
                if (reader.Read()) { throw new InvalidDataException("Trailing geometry recipe JSON"); }
                return result;
            }
        }

        public void GetSteps(out GeometryStepData generator, out IReadOnlyList<GeometryStepData> modifiers)
            => GetSteps(out generator, out modifiers, out _);

        public void GetSteps(out GeometryStepData generator, out IReadOnlyList<GeometryStepData> modifiers,
            out IReadOnlyDictionary<string, object> postProcessing)
        {
            if (Version != 1) { throw new NotSupportedException($"Unsupported geometry recipe version: {Version}"); }
            CheckMembers(m_Data, "Version", "Generator", "Modifiers", "PostProcessing");
            generator = ReadStep(m_Data["Generator"], true);
            var steps = new List<GeometryStepData>();
            if (m_Data.TryGetValue("Modifiers", out var value))
            {
                if (!(value is JArray array)) { throw new InvalidDataException("Geometry Modifiers must be an array"); }
                foreach (var step in array) { steps.Add(ReadStep(step, false)); }
            }
            modifiers = steps.AsReadOnly();
            postProcessing = m_Data.TryGetValue("PostProcessing", out var settings)
                ? ReadParameters(settings, s_PostProcessing, "PostProcessing")
                : new Dictionary<string, object>();
        }

        public static JObject LoadObject(JsonReader reader)
        {
            var previous = reader.DateParseHandling;
            reader.DateParseHandling = DateParseHandling.None;
            try
            {
                return JObject.Load(reader,
                    new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            finally { reader.DateParseHandling = previous; }
        }

        private enum ValueKind { Number, Integer, Boolean, String, Vector2, Vector3, Quaternion, Strings }

        // These are wire types, not construction defaults. Polyhydra owns geometry execution.
        private static readonly Dictionary<string, Dictionary<string, ValueKind>> s_Generators = new Dictionary<string, Dictionary<string, ValueKind>>
        {
            ["rectangle"] = Spec("width height radius", "cornerSegments", strings: "roundedCorners"),
            ["frame"] = Spec("width height borderWidth radius", "cornerSegments"),
            ["panel"] = Spec("width height depth cornerRadius edgeRadius", "cornerSegments edgeSegments widthSegments heightSegments", strings: "roundedCorners"),
            ["cylinder"] = Spec("height bottomRadius bottomAspect topRadius topAspect sweepAngle", "sides", "smoothSides", vector2: "topOffset", quaternion: "topRotation"),
            ["capsule"] = Spec("cylinderHeight radius sweepAngle", "sides capSegments", "smooth", "zeroSweep"),
            ["conic"] = Spec("radius majorAxis minorAxis centerOffset bottomClip topClip sweepAngle", "sides verticalSegments", "smooth", "curve zeroSweep"),
            // Shared wire descriptions for the additional 3D-shapes generators.
            // A host without the corresponding generator retains the definition.
            ["uniform"] = Spec(text: "solid"),
            ["radial"] = Spec("height capheight", "sides", text: "solid"),
            ["grid"] = Spec(integer: "x y", text: "family type shape"),
            ["waterman"] = Spec(integer: "root c"),
            ["mesh"] = Spec(),
            ["polygon"] = Spec("height", "sides"),
            ["star"] = Spec("sharpness height", "sides"),
            ["c_Shape"] = Spec("a b c height"),
            ["l_Shape"] = Spec("a b c height"),
            ["h_Shape"] = Spec("a b c height"),
            ["arc"] = Spec("b c height", "a"),
            ["arch"] = Spec("b c height", "a"),
            ["gothicArch"] = Spec("a b c height"),
            ["ring"] = Spec("b c height", "a"),
            ["triangle"] = Spec("b c height", "a"),
            ["box"] = Spec(integer: "x y z"),
            ["uvSphere"] = Spec("z", "x y"),
            ["uvHemisphere"] = Spec(integer: "x y"),
            ["stairs"] = Spec("y z", "x"),
            ["torus"] = Spec("z", "x y"),
            ["chamferedBox"] = Spec("y z", "x"),
            ["hollowHemisphere"] = Spec("z", "x y"),
            ["chamferedCylinder"] = Spec("z", "x y"),
            ["partialTorus"] = Spec("z", "x y"),
            ["wireframeBox"] = Spec("z")
        };

        private static readonly Dictionary<string, Dictionary<string, ValueKind>> s_Modifiers = new Dictionary<string, Dictionary<string, ValueKind>>
        {
            ["operator"] = Spec("amount amount2 filterParameter", "iterations filterParameterInt",
                "active randomizeAmount randomizeAmount2 invertFilter", "operation filter stringParameter"),
            ["transform"] = Spec(vector3: "translation scale", quaternion: "rotation"),
            ["faceRemove"] = Spec("angle", boolean: "invert", vector3: "direction"),
            ["bend"] = Spec("angle", text: "axis", vector3: "referenceCenter referenceSize"),
            ["flip"] = Spec()
        };

        private static readonly Dictionary<string, ValueKind> s_PostProcessing = Spec(
            "FaceInset AutoSmoothAngle OutputScale", "CanonicalizeIterations PlanarizeIterations", "FastConicalize");

        private static Dictionary<string, ValueKind> Spec(string number = "", string integer = "", string boolean = "",
            string text = "", string vector2 = "", string vector3 = "", string quaternion = "", string strings = "")
        {
            var result = new Dictionary<string, ValueKind>(StringComparer.Ordinal);
            var groups = new[] { number, integer, boolean, text, vector2, vector3, quaternion, strings };
            for (int i = 0; i < groups.Length; ++i)
            {
                foreach (string name in groups[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    result.Add(name, (ValueKind)i);
                }
            }
            return result;
        }

        private static GeometryStepData ReadStep(JToken value, bool generator)
        {
            if (!(value is JObject step)) { throw new InvalidDataException("Geometry step must be an object"); }
            CheckMembers(step, "Type", "Parameters");
            if (step["Type"]?.Type != JTokenType.String) { throw new InvalidDataException("Geometry step requires a string Type"); }
            string type = (string)step["Type"];
            var catalog = generator ? s_Generators : s_Modifiers;
            if (!catalog.TryGetValue(type, out var spec)) { throw new NotSupportedException($"Unsupported geometry step: {type}"); }
            var parameters = step.TryGetValue("Parameters", out var data)
                ? ReadParameters(data, spec, type) : new Dictionary<string, object>();
            return new GeometryStepData(type, parameters);
        }

        private static Dictionary<string, object> ReadParameters(JToken data,
            Dictionary<string, ValueKind> spec, string label)
        {
            if (!(data is JObject bag)) { throw new InvalidDataException($"Geometry {label} settings must be an object"); }
            var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var property in bag.Properties())
            {
                if (!spec.TryGetValue(property.Name, out var kind))
                {
                    throw new NotSupportedException($"Unknown geometry parameter {label}.{property.Name}");
                }
                parameters.Add(property.Name, ReadValue(property.Value, kind, $"{label}.{property.Name}"));
            }
            return parameters;
        }

        private static object ReadValue(JToken value, ValueKind kind, string label)
        {
            switch (kind)
            {
                case ValueKind.Number: return ReadNumber(value, label);
                case ValueKind.Integer: return ReadInteger(value, label);
                case ValueKind.Boolean:
                    if (value.Type == JTokenType.Boolean) { return (bool)value; }
                    break;
                case ValueKind.String:
                    if (value.Type == JTokenType.String) { return (string)value; }
                    break;
                case ValueKind.Strings:
                    if (value is JArray strings && strings.All(item => item.Type == JTokenType.String))
                    {
                        return strings.Select(item => (string)item).ToArray();
                    }
                    break;
                default:
                    int count = kind == ValueKind.Vector2 ? 2 : kind == ValueKind.Vector3 ? 3 : 4;
                    if (value is JArray components && components.Count == count)
                    {
                        var result = components.Select(item => ReadNumber(item, label)).ToArray();
                        if (kind == ValueKind.Quaternion)
                        {
                            double lengthSquared = result.Sum(component => (double)component * component);
                            if (Math.Abs(lengthSquared - 1) > 0.0001) { throw new InvalidDataException($"Geometry {label} must be a unit quaternion"); }
                        }
                        return result;
                    }
                    break;
            }
            throw new InvalidDataException($"Invalid geometry {label}: expected {kind}");
        }

        private static float ReadNumber(JToken value, string label)
        {
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer))
            {
                throw new InvalidDataException($"Geometry {label} must be a number");
            }
            float number = (float)value;
            if (float.IsNaN(number) || float.IsInfinity(number)) { throw new InvalidDataException($"Geometry {label} must be finite"); }
            return number;
        }

        private static int ReadInteger(JToken value, string label)
        {
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer))
            {
                throw new InvalidDataException($"Geometry {label} must be an integer");
            }
            double number = (double)value;
            if (double.IsNaN(number) || number < int.MinValue || number > int.MaxValue || number != Math.Truncate(number))
            {
                throw new InvalidDataException($"Geometry {label} must be an integer in range");
            }
            return (int)number;
        }

        private static void CheckMembers(JObject value, params string[] names)
        {
            foreach (var property in value.Properties())
            {
                if (!names.Contains(property.Name)) { throw new NotSupportedException($"Unknown geometry field: {property.Name}"); }
            }
        }
    }

    public sealed class GeometryRecipeDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(GeometryRecipeData);
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            => new GeometryRecipeData(GeometryRecipeData.LoadObject(reader));
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => ((GeometryRecipeData)value).ToJson().WriteTo(writer);
    }
}
