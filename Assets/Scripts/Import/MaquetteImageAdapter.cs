// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;

namespace TiltBrush.DocumentImport
{
    public sealed class MaquetteImageData
    {
        public NativePlacement Placement { get; internal set; }
        public float Height { get; internal set; }
        public float Width { get; internal set; }
        public string MaterialId { get; internal set; }
    }

    public static class MaquetteImageAdapter
    {
        public static MaquetteImageData Read(MaquetteState state, MaquettePrimitiveData primitive, float unitsPerMetre)
        {
            if (primitive.Type != "ImageFile" && primitive.Type != "ImageFromWeb") { throw new ArgumentException("Expected a Maquette image primitive"); }
            var placement = new MaquettePlacement(state);
            var rectangle = NativeImageRectangle.FromPlacement(placement, 1, 1, unitsPerMetre);
            return new MaquetteImageData
            {
                Placement = rectangle.Placement, Height = rectangle.Height, Width = rectangle.Width,
                MaterialId = primitive.UsesDefaultImageMaterial ? "TranslucentUnlit" : (string)state.Data["materialId"] ?? "TranslucentUnlit"
            };
        }
    }
}
