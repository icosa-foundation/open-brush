// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public static class ShapesXrImageAdapter
    {
        public static NativeImageRectangle Read(ShapesXrObjectState state, float unitsPerMetre)
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
            return NativeImageRectangle.FromPlacement(state.Placement(), width, localHeight, unitsPerMetre);
        }
    }
}
