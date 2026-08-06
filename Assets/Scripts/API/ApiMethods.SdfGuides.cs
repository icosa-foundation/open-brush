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
using IsoMesh;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint(
            "guide.sdf.create",
            "Creates an editable SDF guide from a JSON transform and ordered primitive list",
            "{\"primitives\":[{\"type\":\"sphere\",\"geometry\":[1,0,0,0],\"operation\":\"union\",\"blend\":0}]}")]
        public static string CreateSdfGuide(string json)
        {
            JObject request = ParseSdfRequest(json);
            var definitions = new List<SdfStencil.PrimitiveDefinition>();
            if (request["primitives"] is JArray primitives)
            {
                for (int i = 0; i < primitives.Count; ++i)
                {
                    if (!(primitives[i] is JObject primitiveObject))
                    {
                        throw new ArgumentException($"primitives[{i}] must be a JSON object.");
                    }
                    definitions.Add(ParsePrimitiveDefinition(primitiveObject));
                }
            }
            else if (request["primitives"] != null)
            {
                throw new ArgumentException("primitives must be a JSON array.");
            }

            SdfStencil.ValidatePrimitiveDefinitions(definitions);

            TrTransform transform = request["transform"] == null
                ? _CurrentBrushTransform()
                : ParseTransform(request["transform"], "transform");
            var command = new CreateWidgetCommand(
                WidgetManager.m_Instance.SdfStencilPrefab, transform,
                forceTransform: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
            var stencil = command.Widget as SdfStencil;
            if (stencil == null)
            {
                throw new InvalidOperationException("The configured SDF guide prefab did not create an SDF guide.");
            }

            stencil.ReplacePrimitives(definitions);

            return new JObject
            {
                ["guide"] = WidgetManager.m_Instance.GetActiveWidgetIndex(stencil)
            }.ToString(Formatting.None);
        }

        [ApiEndpoint(
            "guide.sdf.addprimitive",
            "Adds an SDF primitive using a JSON object containing guide, type, geometry, transform, operation, and blend",
            "{\"guide\":0,\"type\":\"sphere\",\"geometry\":[1,0,0,0],\"operation\":\"union\",\"blend\":0}")]
        public static string AddSdfPrimitive(string json)
        {
            JObject request = ParseSdfRequest(json);
            int guideIndex = RequiredInt(request, "guide");
            SdfStencil stencil = GetSdfStencil(guideIndex);
            SdfStencil.PrimitiveDefinition definition = ParsePrimitiveDefinition(request);
            SDFPrimitive primitive = stencil.AddPrimitive(
                definition.Type, definition.Geometry, definition.Transform,
                definition.Operation, definition.Blend);

            return new JObject
            {
                ["guide"] = guideIndex,
                ["primitive"] = PrimitiveIndex(stencil, primitive)
            }.ToString(Formatting.None);
        }

        [ApiEndpoint(
            "guide.sdf.updateprimitive",
            "Updates specified fields on an SDF primitive using JSON",
            "{\"guide\":0,\"primitive\":1,\"operation\":\"subtract\",\"blend\":0.1}")]
        public static void UpdateSdfPrimitive(string json)
        {
            JObject request = ParseSdfRequest(json);
            SdfStencil stencil = GetSdfStencil(RequiredInt(request, "guide"));
            SDFPrimitive primitive = stencil.GetPrimitive(RequiredInt(request, "primitive"));

            SDFPrimitiveType? type = request["type"] == null
                ? (SDFPrimitiveType?)null
                : SdfStencil.ParsePrimitiveType(request.Value<string>("type"));
            Vector4? geometry = request["geometry"] == null
                ? (Vector4?)null
                : ParseVector4(request["geometry"], "geometry");
            TrTransform? transform = request["transform"] == null
                ? (TrTransform?)null
                : ParseTransform(request["transform"], "transform");
            SDFCombineType? operation = request["operation"] == null
                ? (SDFCombineType?)null
                : SdfStencil.ParseOperation(request.Value<string>("operation"));
            float? blend = request["blend"] == null
                ? (float?)null
                : request.Value<float>("blend");

            stencil.UpdatePrimitive(primitive, type, geometry, transform, operation, blend);
        }

        [ApiEndpoint(
            "guide.sdf.removeprimitive",
            "Removes a primitive from an SDF guide",
            "0,1")]
        public static void RemoveSdfPrimitive(int guideIndex, int primitiveIndex)
        {
            SdfStencil stencil = GetSdfStencil(guideIndex);
            stencil.RemovePrimitive(stencil.GetPrimitive(primitiveIndex));
        }

        [ApiEndpoint(
            "guide.sdf.clear",
            "Removes every primitive from an SDF guide",
            "0")]
        public static void ClearSdfGuide(int guideIndex)
        {
            GetSdfStencil(guideIndex).ClearPrimitives();
        }

        [ApiEndpoint(
            "guide.sdf.get",
            "Returns the ordered primitives in an SDF guide as JSON",
            "0")]
        public static string GetSdfGuide(int guideIndex)
        {
            SdfStencil stencil = GetSdfStencil(guideIndex);
            var primitives = new JArray();
            foreach (SDFPrimitive primitive in stencil.GetPrimitives())
            {
                primitives.Add(new JObject
                {
                    ["type"] = SdfStencil.PrimitiveTypeName(primitive.Type),
                    ["geometry"] = Vector4ToJson(primitive.Data),
                    ["transform"] = TransformToJson(TrTransform.FromLocalTransform(primitive.transform)),
                    ["operation"] = SdfStencil.OperationName(primitive.Operation),
                    ["blend"] = primitive.Smoothing
                });
            }
            return new JObject { ["primitives"] = primitives }.ToString(Formatting.None);
        }

        private static JObject ParseSdfRequest(string json)
        {
            try
            {
                return JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException("Expected a JSON object.", nameof(json), exception);
            }
        }

        private static SdfStencil.PrimitiveDefinition ParsePrimitiveDefinition(JObject value)
        {
            string type = value.Value<string>("type");
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("An SDF primitive requires a type.");
            }
            if (value["geometry"] == null)
            {
                throw new ArgumentException("An SDF primitive requires geometry.");
            }

            float blend = value["blend"] == null ? 0f : value.Value<float>("blend");
            if (blend < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), blend, "SDF blend cannot be negative.");
            }

            return new SdfStencil.PrimitiveDefinition(
                SdfStencil.ParsePrimitiveType(type),
                ParseVector4(value["geometry"], "geometry"),
                value["transform"] == null
                    ? TrTransform.identity
                    : ParseTransform(value["transform"], "transform"),
                value["operation"] == null
                    ? SDFCombineType.SmoothUnion
                    : SdfStencil.ParseOperation(value.Value<string>("operation")),
                blend,
                false);
        }

        private static SdfStencil GetSdfStencil(int index)
        {
            if (!(_GetActiveStencil(index) is SdfStencil stencil))
            {
                throw new ArgumentException($"Guide {index} is not an SDF guide.", nameof(index));
            }
            return stencil;
        }

        private static int RequiredInt(JObject value, string property)
        {
            if (value[property] == null)
            {
                throw new ArgumentException($"The JSON object requires an integer '{property}' property.");
            }
            return value.Value<int>(property);
        }

        private static int PrimitiveIndex(SdfStencil stencil, SDFPrimitive primitive)
        {
            IReadOnlyList<SDFPrimitive> primitives = stencil.GetPrimitives();
            for (int i = 0; i < primitives.Count; ++i)
            {
                if (primitives[i] == primitive)
                {
                    return i;
                }
            }
            throw new InvalidOperationException("The new SDF primitive was not registered with its guide.");
        }

        private static Vector4 ParseVector4(JToken token, string property)
        {
            if (token is JArray array && array.Count >= 1 && array.Count <= 4)
            {
                return new Vector4(
                    array.Count > 0 ? array[0].Value<float>() : 0f,
                    array.Count > 1 ? array[1].Value<float>() : 0f,
                    array.Count > 2 ? array[2].Value<float>() : 0f,
                    array.Count > 3 ? array[3].Value<float>() : 0f);
            }
            if (token is JObject value)
            {
                return new Vector4(
                    value.Value<float?>("x") ?? 0f,
                    value.Value<float?>("y") ?? 0f,
                    value.Value<float?>("z") ?? 0f,
                    value.Value<float?>("w") ?? 0f);
            }
            throw new ArgumentException($"'{property}' must be an array of one to four numbers or an x/y/z/w object.");
        }

        private static TrTransform ParseTransform(JToken token, string property)
        {
            if (!(token is JObject value))
            {
                throw new ArgumentException($"'{property}' must be a JSON object.");
            }

            JToken positionToken = value["position"] ?? value["translation"];
            Vector3 position = positionToken == null
                ? Vector3.zero
                : ParseVector3(positionToken, $"{property}.position");
            Quaternion rotation = value["rotation"] == null
                ? Quaternion.identity
                : ParseQuaternion(value["rotation"], $"{property}.rotation");
            float scale = value.Value<float?>("scale") ?? 1f;
            if (scale <= 0f)
            {
                throw new ArgumentOutOfRangeException(property, scale, "An SDF transform must have a positive scale.");
            }
            return TrTransform.TRS(position, rotation, scale);
        }

        private static Vector3 ParseVector3(JToken token, string property)
        {
            if (token is JArray array && array.Count == 3)
            {
                return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
            }
            if (token is JObject value)
            {
                return new Vector3(
                    value.Value<float?>("x") ?? 0f,
                    value.Value<float?>("y") ?? 0f,
                    value.Value<float?>("z") ?? 0f);
            }
            throw new ArgumentException($"'{property}' must be an array of three numbers or an x/y/z object.");
        }

        private static Quaternion ParseQuaternion(JToken token, string property)
        {
            if (token is JArray array && array.Count == 4)
            {
                return new Quaternion(
                    array[0].Value<float>(), array[1].Value<float>(),
                    array[2].Value<float>(), array[3].Value<float>());
            }
            if (token is JObject value)
            {
                return new Quaternion(
                    value.Value<float?>("x") ?? 0f,
                    value.Value<float?>("y") ?? 0f,
                    value.Value<float?>("z") ?? 0f,
                    value.Value<float?>("w") ?? 1f);
            }
            throw new ArgumentException($"'{property}' must be an array of four numbers or an x/y/z/w object.");
        }

        private static JArray Vector4ToJson(Vector4 value)
        {
            return new JArray(value.x, value.y, value.z, value.w);
        }

        private static JObject TransformToJson(TrTransform value)
        {
            return new JObject
            {
                ["position"] = Vector3ToJson(value.translation),
                ["rotation"] = new JArray(
                    value.rotation.x, value.rotation.y, value.rotation.z, value.rotation.w),
                ["scale"] = value.scale
            };
        }
    }
}
