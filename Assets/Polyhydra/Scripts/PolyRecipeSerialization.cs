using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Polyhydra.Core;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    // Application-level compatibility lives here, not in the geometry library.
    internal static partial class PolyRecipeSerialization
    {
        public const int CurrentVersion = 1;

        public static List<Dictionary<string, object>> WriteModifiers(IEnumerable<PreviewPolyhedron.OpDefinition> operators)
            => operators?.Select(op => WriteModifier(op.ToModifierSettings())).ToList()
                ?? new List<Dictionary<string, object>>();

        private static Dictionary<string, object> WriteModifier(ModifierSettings modifier)
        {
            var step = GeometryStep.FromModifier(modifier);
            return new Dictionary<string, object>
            {
                { "Type", step.Type },
                { "Parameters", step.Parameters }
            };
        }

        public static ModifierSettings ReadModifier(Dictionary<string, object> data)
        {
            if (data == null || !data.TryGetValue("Type", out var type) || (string)type != "operator")
                throw new NotSupportedException("The 3D editor requires an operator modifier.");
            if (!data.TryGetValue("Parameters", out var values) || values == null)
                throw new InvalidDataException("Modifier parameters are missing.");
            var json = values as JObject ?? JObject.FromObject(values);
            // Derive the supported primitive fields from Core rather than maintaining
            // another independently named modifier schema in this editor.
            var template = GeometryStep.FromModifier(new ModifierSettings
                { StringParameter = "", UseFilterParamInt = true }).Parameters;
            var parameters = new Dictionary<string, object>();
            foreach (var property in json.Properties())
            {
                if (!template.TryGetValue(property.Name, out var sample))
                    throw new NotSupportedException($"Unknown modifier parameter '{property.Name}'.");
                var token = property.Value;
                object value;
                if (sample is bool && token.Type == JTokenType.Boolean) value = token.Value<bool>();
                else if (sample is string && token.Type == JTokenType.String) value = token.Value<string>();
                else if (sample is float && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float))
                {
                    float number = token.Value<float>();
                    if (float.IsNaN(number) || float.IsInfinity(number))
                        throw new InvalidDataException($"Modifier parameter '{property.Name}' must be finite.");
                    value = number;
                }
                else if (sample is int && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float))
                {
                    double number = token.Value<double>();
                    if (number < int.MinValue || number > int.MaxValue || number != Math.Truncate(number))
                        throw new InvalidDataException($"Modifier parameter '{property.Name}' must be an integer.");
                    value = checked((int)number);
                }
                else throw new InvalidDataException($"Invalid modifier parameter '{property.Name}'.");
                parameters.Add(property.Name, value);
            }
            return new GeometryStep("operator", parameters).ToModifierSettings();
        }

        public static List<Dictionary<string, object>> ReadLegacyModifiers(List<Dictionary<string, object>> operations)
        {
            var modifiers = new List<Dictionary<string, object>>();
            if (operations == null) return modifiers;
            foreach (var data in operations)
            {
                var operation = (PolyMesh.Operation)Convert.ToInt32(data["operation"]);
                var filter = (FilterTypes)Convert.ToInt32(data.GetValueOrDefault("filterType"));
                if (!Enum.IsDefined(typeof(PolyMesh.Operation), operation) || !Enum.IsDefined(typeof(FilterTypes), filter))
                    throw new NotSupportedException("Unknown legacy modifier or filter identifier.");
                Color color = Color.white;
                if (data.TryGetValue("paramColor", out var colorValue))
                {
                    if (colorValue is Color nativeColor) color = nativeColor;
                    else if (colorValue is JArray array && array.Count >= 3)
                        color = new Color(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
                    else throw new InvalidDataException("Invalid legacy modifier color.");
                }
                modifiers.Add(WriteModifier(new ModifierSettings
                {
                    OpType = operation,
                    Active = !Convert.ToBoolean(data.GetValueOrDefault("disabled")),
                    Parameter1 = Convert.ToSingle(data.GetValueOrDefault("param1")),
                    Parameter2 = Convert.ToSingle(data.GetValueOrDefault("param2")),
                    Parameter1Randomize = Convert.ToBoolean(data.GetValueOrDefault("param1Randomize")),
                    Parameter2Randomize = Convert.ToBoolean(data.GetValueOrDefault("param2Randomize")),
                    StringParameter = $"#{ColorUtility.ToHtmlStringRGB(color)}",
                    FilterType = filter,
                    FilterParam = Convert.ToSingle(data.GetValueOrDefault("filterParamFloat")),
                    UseFilterParamInt = true,
                    FilterParamInt = Convert.ToInt32(data.GetValueOrDefault("filterParamInt")),
                    FilterFlip = Convert.ToBoolean(data.GetValueOrDefault("filterNot"))
                }));
            }
            return modifiers;
        }

        public static Dictionary<string, object> ReadLegacyGenerator(GeneratorTypes type, Dictionary<string, object> parameters)
        {
            if (parameters == null) return null;
            var result = new Dictionary<string, object>(parameters);
            // In the unversioned build, ShapeTypes 10 was the filled Sector. Current
            // versioned definitions use the explicit enum, where 10 means Rectangle.
            if (type == GeneratorTypes.Shapes && Convert.ToInt32(result.GetValueOrDefault("type")) == 10)
            {
                result["type"] = (int)ShapeTypes.Arc;
                result["b"] = 0f;
            }
            return result;
        }

        public static string UpgradeLegacyPath(string path)
        {
            if (path == "$.AutoSmoothAngle") return "$.PostProcessing.AutoSmoothAngle";
            var names = new Dictionary<string, string>
            {
                { "param1", "amount" }, { "param2", "amount2" },
                { "param1Randomize", "randomizeAmount" }, { "param2Randomize", "randomizeAmount2" },
                { "filterParamFloat", "filterParameter" }, { "filterParamInt", "filterParameterInt" },
                { "filterNot", "invertFilter" }, { "disabled", "active" }
            };
            return Regex.Replace(path, @"^\$\.Operations\[(\d+)\]\.(\w+)", match =>
            {
                if (!names.TryGetValue(match.Groups[2].Value, out var name))
                    throw new NotSupportedException($"Unsupported legacy exposed-parameter path '{path}'.");
                return $"$.Modifiers[{match.Groups[1].Value}].Parameters.{name}";
            });
        }
    }
}
