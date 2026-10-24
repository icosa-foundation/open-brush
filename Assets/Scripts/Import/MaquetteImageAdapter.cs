// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;

namespace TiltBrush.DocumentImport
{
    public sealed class MaquetteImageData
    {
        public MaquettePlacement Placement { get; internal set; }
        public float Height { get; internal set; }
        public float[] GeometryTransform { get; internal set; }
        public string MaterialId { get; internal set; }
    }

    public static class MaquetteImageAdapter
    {
        public static MaquetteImageData Read(MaquetteState state, MaquettePrimitiveData primitive, float unitsPerMetre)
        {
            if (primitive.Type != "ImageFile" && primitive.Type != "ImageFromWeb") { throw new ArgumentException("Expected a Maquette image primitive"); }
            var placement = new MaquettePlacement(state);
            // The image's aspect is already in its saved scale. Start with a one-unit square,
            // then factor out its height so ordinary image-widget sizing remains meaningful.
            var matrix = placement.GetIntrinsicTransform(new[] { 1f, 1f, 1f }, new[] { 0f, 0f, 0f }, unitsPerMetre);
            float height = (float)Math.Sqrt((double)matrix[4] * matrix[4] + (double)matrix[5] * matrix[5] + (double)matrix[6] * matrix[6]);
            if (!(height > 0) || float.IsInfinity(height)) { throw new InvalidDataException("Collapsed or invalid Maquette image height"); }
            for (int i = 0; i < 15; ++i) { matrix[i] /= height; }
            return new MaquetteImageData
            {
                Placement = placement, Height = height, GeometryTransform = matrix,
                MaterialId = primitive.UsesDefaultImageMaterial ? "TranslucentUnlit" : (string)state.Data["materialId"] ?? "TranslucentUnlit"
            };
        }
    }
}
