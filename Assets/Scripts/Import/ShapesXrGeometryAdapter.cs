// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    /// Translates resolved native panel properties to a shared recipe. Does not generate geometry.
    /// The source preset name is deliberately irrelevant: saved properties are authoritative.
    public static class ShapesXrGeometryAdapter
    {
        public static GeometryRecipeData Panel(JObject properties)
        {
            var dimensions = properties["obj-pmugs"] as JArray;
            if (dimensions == null || dimensions.Count != 3) { throw new InvalidDataException("Missing ShapesXR panel dimensions"); }
            float width = Number(dimensions[0], "width"), height = Number(dimensions[1], "height"), depth = Number(dimensions[2], "depth");
            if (width <= 0 || height <= 0 || depth <= 0) { throw new InvalidDataException("ShapesXR panel dimensions must be positive; zero-depth panels require separate geometry support"); }
            float bendX = Number(properties["obj-pmbh"], "horizontal bend"), bendY = Number(properties["obj-pmbv"], "vertical bend");
            float corner = Number(properties["obj-pmugcor"], "corner radius"), edge = Number(properties["obj-pmugchr"], "edge radius");
            if (corner < 0 || edge < 0) { throw new InvalidDataException("Negative ShapesXR panel radius"); }
            int mask = Integer(properties["obj-pmugcors"], "corner selection");
            if (mask < 0 || mask > 15) { throw new InvalidDataException("Unsupported ShapesXR corner selection"); }
            var corners = new JArray();
            var names = new[] { "topLeft", "bottomLeft", "bottomRight", "topRight" };
            for (int i = 0; i < names.Length; ++i) { if ((mask & (1 << i)) != 0) { corners.Add(names[i]); } }
            var parameters = new JObject
            {
                ["width"] = width, ["height"] = height, ["depth"] = depth,
                ["cornerRadius"] = corner, ["edgeRadius"] = edge, ["roundedCorners"] = corners,
                ["cornerSegments"] = Boolean(properties["obj-pmugcos"], "sharp corners") ? 1 : 8,
                ["edgeSegments"] = Boolean(properties["obj-pmugchs"], "sharp edges") ? 1 : 8,
                ["widthSegments"] = Samples(bendY), ["heightSegments"] = Samples(bendX)
            };
            var operations = new JArray();
            bool generateBack = Boolean(properties["obj-pmuggb"], "generate back");
            if (depth <= .0001f)
            {
                // Native thin sheets omit walls and bevels. Back-plane generation is independent.
                parameters["edgeRadius"] = 0f;
                if (generateBack)
                {
                    foreach (var direction in new[] { new JArray(1, 0, 0), new JArray(-1, 0, 0), new JArray(0, 1, 0), new JArray(0, -1, 0) })
                    {
                        operations.Add(Step("faceRemove", new JObject
                        {
                            ["direction"] = direction, ["angle"] = 89.9f, ["invert"] = false
                        }));
                    }
                }
                else
                {
                    operations.Add(Step("faceRemove", new JObject
                    {
                        ["direction"] = new JArray(0, 0, -1), ["angle"] = .1f, ["invert"] = true
                    }));
                }
            }
            else if (!generateBack)
            {
                operations.Add(Step("faceRemove", new JObject
                {
                    ["direction"] = new JArray(0, 0, 1), ["angle"] = 89.9f, ["invert"] = false
                }));
            }
            foreach (var bend in new[] { (Angle: bendX, Axis: "x"), (Angle: bendY, Axis: "y") })
            {
                if (bend.Angle == 0) { continue; }
                operations.Add(Step("bend", new JObject
                {
                    ["angle"] = bend.Angle, ["axis"] = bend.Axis,
                    ["referenceCenter"] = new JArray(0, 0, 0), ["referenceSize"] = new JArray(width, height, depth)
                }));
            }
            return new GeometryRecipeData(new JObject
            {
                ["Version"] = 1, ["Generator"] = Step("panel", parameters), ["Operations"] = operations
            });
        }

        private static int Samples(float angle) => angle == 0 ? 1 :
            (int)Math.Round(64 * Math.Max(.125, Math.Min(1, Math.Abs((double)angle) / 360)), MidpointRounding.ToEven);

        private static JObject Step(string type, JObject parameters) => new JObject { ["Type"] = type, ["Parameters"] = parameters };

        private static float Number(JToken value, string label)
        {
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer))
            {
                throw new InvalidDataException($"Invalid ShapesXR {label}");
            }
            float number = (float)value;
            if (float.IsNaN(number) || float.IsInfinity(number)) { throw new InvalidDataException($"Nonfinite ShapesXR {label}"); }
            return number;
        }

        private static int Integer(JToken value, string label)
        {
            if (value?.Type != JTokenType.Integer) { throw new InvalidDataException($"Invalid ShapesXR {label}"); }
            return (int)value;
        }

        private static bool Boolean(JToken value, string label)
        {
            if (value?.Type != JTokenType.Boolean) { throw new InvalidDataException($"Invalid ShapesXR {label}"); }
            return (bool)value;
        }
    }
}
