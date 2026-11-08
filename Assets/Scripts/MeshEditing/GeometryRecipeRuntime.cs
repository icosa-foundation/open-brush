// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush.DocumentImport;
using UnityEngine;

namespace TiltBrush.MeshEditing
{
    // Shared application bridge: wire primitives become Core settings in one place.
    public static class GeometryRecipeRuntime
    {
        public static PolyMesh Evaluate(GeometryRecipeData data, Func<GeometryStep, PolyMesh> generator = null)
        {
            data.GetSteps(out var source, out var modifiers, out var post);
            var settings = new GeometryPostProcessingSettings
            {
                FastConicalize = Setting(post, nameof(GeometryPostProcessingSettings.FastConicalize), true),
                CanonicalizeIterations = Setting(post, nameof(GeometryPostProcessingSettings.CanonicalizeIterations), 0),
                PlanarizeIterations = Setting(post, nameof(GeometryPostProcessingSettings.PlanarizeIterations), 0),
                FaceInset = Setting(post, nameof(GeometryPostProcessingSettings.FaceInset), 0f),
                AutoSmoothAngle = Setting(post, nameof(GeometryPostProcessingSettings.AutoSmoothAngle), 0f),
                OutputScale = Setting(post, nameof(GeometryPostProcessingSettings.OutputScale), 1f)
            };
            return GeometryRecipeEvaluator.Build(new GeometryRecipe(ToStep(source), modifiers.Select(ToStep), settings),
                step => GenerateAdditional(step) ?? generator?.Invoke(step));
        }

        // Explicit dimensions distinguish these shared generators from the older
        // 3D-shapes x/y/z controls, which continue through the host's legacy reader.
        private static PolyMesh GenerateAdditional(GeometryStep step)
        {
            var p = step.Parameters;
            if (step.Type == "torus" && p.ContainsKey("ringRadius"))
            {
                float tube = Mathf.Max(.0001f, Setting(p, "tubeRadius", .04f));
                float ring = Mathf.Max(tube + .0001f, Setting(p, "ringRadius", .11f));
                var poly = VariousSolids.PartialTorus(
                    Mathf.Clamp(Setting(p, "pathSegments", 32), 3, 128),
                    Mathf.Clamp(Setting(p, "tubeSegments", 12), 3, 64),
                    100f * tube / ring, Mathf.Clamp(Setting(p, "sweepAngle", 360f), 1f, 360f));
                return poly.Duplicate(Matrix4x4.Scale(Vector3.one * ring));
            }
            if (step.Type == "uvSphere" && p.ContainsKey("radius"))
            {
                int longitude = Mathf.Clamp(Setting(p, "longitudeSegments", 24), 3, 24);
                int latitude = Mathf.Clamp(Setting(p, "latitudeSegments", 12), 3, 24);
                var poly = Setting(p, "hemisphere", false)
                    ? VariousSolids.UvHemisphere(longitude, latitude)
                    : VariousSolids.UvSphere(longitude, latitude).FillHoles();
                float radius = Mathf.Max(.0001f, Setting(p, "radius", .15f));
                var radii = new Vector3(
                    Mathf.Max(.0001f, Setting(p, "radiusX", radius)),
                    Mathf.Max(.0001f, Setting(p, "radiusY", radius)),
                    Mathf.Max(.0001f, Setting(p, "radiusZ", radius)));
                return poly.Duplicate(Matrix4x4.Scale(radii));
            }
            if (step.Type == "uniform" && p.ContainsKey("radius"))
            {
                string solid = Setting(p, "solid", "Dodecahedron");
                if (!Enum.TryParse(solid, out UniformTypes type) ||
                    (type != UniformTypes.Tetrahedron && type != UniformTypes.Cube &&
                     type != UniformTypes.Octahedron && type != UniformTypes.Dodecahedron && type != UniformTypes.Icosahedron))
                {
                    throw new NotSupportedException($"Unsupported regular solid '{solid}'");
                }
                var poly = new WythoffPoly(type).Build();
                float radius = poly.Vertices.Max(vertex => vertex.Position.magnitude);
                return poly.Duplicate(Matrix4x4.Scale(Vector3.one *
                    (Mathf.Max(.0001f, Setting(p, "radius", .15f)) / radius)));
            }
            if (step.Type == "stairs" && p.ContainsKey("width"))
            {
                int steps = Mathf.Clamp(Setting(p, "steps", 6), 1, 64);
                var poly = VariousSolids.Stairs(steps, 1f, 1f / steps);
                var bounds = new Bounds(poly.Vertices[0].Position, Vector3.zero);
                foreach (var vertex in poly.Vertices) { bounds.Encapsulate(vertex.Position); }
                float width = Mathf.Max(.0001f, Setting(p, "width", .3f));
                float height = Mathf.Max(.0001f, Setting(p, "height", .3f));
                float depth = Mathf.Max(.0001f, Setting(p, "depth", .4f));
                // Dimension the generator before modifiers, preserving their physical sizes.
                var scale = new Vector3(width / bounds.size.x, height / bounds.size.y, depth / bounds.size.z);
                return poly.Duplicate(Matrix4x4.TRS(new Vector3(0, height * .5f, 0), Quaternion.identity, scale) *
                    Matrix4x4.Translate(-bounds.center));
            }
            return null;
        }

        private static T Setting<T>(IReadOnlyDictionary<string, object> settings, string name, T fallback)
            => settings.TryGetValue(name, out var value) ? (T)value : fallback;

        private static GeometryStep ToStep(GeometryStepData step)
        {
            var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var entry in step.Parameters)
            {
                object value = entry.Value;
                if (value is float[] components)
                {
                    switch (components.Length)
                    {
                        case 2: value = new Vector2(components[0], components[1]); break;
                        case 3: value = new Vector3(components[0], components[1], components[2]); break;
                        case 4: value = new Quaternion(components[0], components[1], components[2], components[3]); break;
                        default: throw new ArgumentException($"Invalid geometry vector: {entry.Key}");
                    }
                }
                parameters.Add(entry.Key, value);
            }
            return new GeometryStep(step.Type, parameters);
        }
    }
}
