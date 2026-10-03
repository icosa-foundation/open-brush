// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TiltBrush
{
    internal sealed class ExposedParameterDefinition
    {
        public string Path { get; }
        public string Label { get; }
        public float Min { get; }
        public float Max { get; }
        public float InitialValue { get; }
        public bool IsInteger { get; }

        public ExposedParameterDefinition(
            string path, string label, float min, float max, float initialValue, bool isInteger)
        {
            Path = path;
            Label = label;
            Min = min;
            Max = max;
            InitialValue = initialValue;
            IsInteger = isInteger;
        }
    }

    /// <summary>
    /// A PolyRecipe plus optional preset metadata describing values that a UI may expose.
    /// This class deliberately has no dependency on a particular panel or control type.
    /// </summary>
    internal sealed class PolyRecipePreset
    {
        private readonly JArray m_ExposedParameterJson;

        public PolyRecipe Recipe { get; }
        public bool HasExposedParameters => m_ExposedParameterJson != null;
        public IReadOnlyList<ExposedParameterDefinition> ExposedParameters { get; }

        private PolyRecipePreset(
            PolyRecipe recipe,
            JArray exposedParameterJson,
            IReadOnlyList<ExposedParameterDefinition> exposedParameters)
        {
            Recipe = recipe;
            m_ExposedParameterJson = exposedParameterJson;
            ExposedParameters = exposedParameters;
        }

        public static PolyRecipePreset FromJson(
            string presetText, Color[] defaultColors, Action<string> warningLogger = null)
        {
            return FromJsonObject(JObject.Parse(presetText), defaultColors, warningLogger);
        }

        public static PolyRecipePreset FromJsonObject(
            JObject presetJson, Color[] defaultColors, Action<string> warningLogger = null)
        {
            PolyRecipe recipe = RecipeFromJsonObject(presetJson, defaultColors);
            if (presetJson["ExposedParameters"] is not JArray definitions)
            {
                return new PolyRecipePreset(
                    recipe, exposedParameterJson: null,
                    exposedParameters: Array.Empty<ExposedParameterDefinition>());
            }

            var exposedParameters = new List<ExposedParameterDefinition>();
            foreach (JToken definitionToken in definitions)
            {
                if (definitionToken is not JObject definition)
                {
                    warningLogger?.Invoke("Exposed parameter definition is not an object.");
                    continue;
                }

                string path = definition.Value<string>("path");
                string label = definition.Value<string>("label") ?? path;
                float? min = definition.Value<float?>("min");
                float? max = definition.Value<float?>("max");
                if (string.IsNullOrWhiteSpace(path) || !min.HasValue || !max.HasValue ||
                    max.Value <= min.Value)
                {
                    warningLogger?.Invoke($"Invalid exposed parameter definition for '{label}'.");
                    continue;
                }

                JToken valueToken;
                try
                {
                    valueToken = SelectNumericToken(presetJson, path);
                }
                catch (JsonException e)
                {
                    warningLogger?.Invoke($"Invalid exposed parameter path '{path}': {e.Message}");
                    continue;
                }

                exposedParameters.Add(new ExposedParameterDefinition(
                    path,
                    label,
                    min.Value,
                    max.Value,
                    valueToken.Value<float>(),
                    valueToken.Type == JTokenType.Integer));
            }

            return new PolyRecipePreset(
                recipe,
                (JArray)definitions.DeepClone(),
                exposedParameters);
        }

        public PolyRecipe SetExposedParameterValue(
            PolyRecipe recipe, int index, float value, Color[] defaultColors)
        {
            if (index < 0 || index >= ExposedParameters.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            JObject json = RecipeToJsonObject(recipe);
            JToken token = SelectNumericToken(json, ExposedParameters[index].Path);
            token.Replace(token.Type == JTokenType.Integer
                ? new JValue(Mathf.RoundToInt(value))
                : new JValue(value));
            return RecipeFromJsonObject(json, defaultColors);
        }

        public JObject ToJsonObject(PolyRecipe recipe)
        {
            JObject json = RecipeToJsonObject(recipe);
            if (m_ExposedParameterJson != null)
            {
                json["ExposedParameters"] = m_ExposedParameterJson.DeepClone();
            }
            return json;
        }

        internal static JToken SelectNumericToken(JObject json, string path)
        {
            JToken token = json.SelectToken(path, errorWhenNoMatch: true);
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            {
                throw new JsonSerializationException(
                    $"JSONPath '{path}' resolved to {token.Type}, not a number.");
            }
            return token;
        }

        private static JObject RecipeToJsonObject(PolyRecipe recipe)
        {
            JsonSerializer serializer = CreateRecipeJsonSerializer();
            using var textWriter = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            using var jsonWriter = new CustomJsonWriter(textWriter);
            serializer.Serialize(jsonWriter, new EditableModelDefinition(recipe));
            jsonWriter.Flush();
            return JObject.Parse(textWriter.ToString());
        }

        private static PolyRecipe RecipeFromJsonObject(JObject json, Color[] defaultColors)
        {
            EditableModelDefinition definition = json.ToObject<EditableModelDefinition>(
                CreateRecipeJsonSerializer());
            if (definition == null)
            {
                throw new JsonSerializationException("Shape preset did not contain a definition.");
            }
            return PolyRecipe.FromDef(definition, defaultColors);
        }

        private static JsonSerializer CreateRecipeJsonSerializer()
        {
            return new JsonSerializer { ContractResolver = new CustomJsonContractResolver() };
        }
    }
}
