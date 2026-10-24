// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class ShapesXrImageData
    {
        public NativePlacement Placement { get; internal set; }
        public float Height { get; internal set; }
        public float[] GeometryTransform { get; internal set; }
    }

    public static class ShapesXrImageAdapter
    {
        public static ShapesXrImageData Read(ShapesXrObjectState state, float unitsPerMetre)
        {
            if (!(state.Properties["s"] is JArray bounds) || bounds.Count != 3 ||
                (bounds[0].Type != JTokenType.Float && bounds[0].Type != JTokenType.Integer))
            {
                throw new InvalidDataException("Missing ShapesXR image aspect ratio");
            }
            float aspect = (float)bounds[0];
            if (float.IsNaN(aspect) || float.IsInfinity(aspect)) { throw new InvalidDataException("Invalid ShapesXR image aspect ratio"); }
            // A negative value requests a square; nonnegative values fit the aspect into
            // a one-unit square. The object hierarchy's authored scale remains independent.
            float width = aspect < 0 || aspect > 1 ? 1 : aspect;
            float localHeight = aspect > 1 ? 1 / aspect : 1;
            var placement = state.Placement();
            var matrix = placement.GetIntrinsicTransform(new[] { width, localHeight, 1f }, new[] { 0f, 0f, 0f }, unitsPerMetre);
            float height = (float)Math.Sqrt((double)matrix[4] * matrix[4] + (double)matrix[5] * matrix[5] + (double)matrix[6] * matrix[6]);
            if (!(height > 0) || float.IsInfinity(height)) { throw new InvalidDataException("Collapsed ShapesXR image height"); }
            for (int i = 0; i < 15; ++i) { matrix[i] /= height; }
            return new ShapesXrImageData { Placement = placement, Height = height, GeometryTransform = matrix };
        }
    }
}
