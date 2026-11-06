// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
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
            return GeometryRecipeEvaluator.Build(new GeometryRecipe(ToStep(source), modifiers.Select(ToStep), settings), generator);
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
