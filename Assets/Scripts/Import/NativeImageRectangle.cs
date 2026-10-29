// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Numerics;

namespace TiltBrush.DocumentImport
{
    /// A rectangular image uses ordinary rigid placement, width and height.
    public sealed class NativeImageRectangle
    {
        public NativePlacement Placement { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }

        public static NativeImageRectangle FromPlacement(NativePlacement placement, float width, float height, float unitsPerMetre)
        {
            var values = placement.GetIntrinsicTransform(new[] { width, height, 1f }, new[] { 0f, 0f, 0f }, unitsPerMetre);
            var x = new Vector3(values[0], values[1], values[2]);
            var y = new Vector3(values[4], values[5], values[6]);
            var z = new Vector3(values[8], values[9], values[10]);
            float w = x.Length(), h = y.Length();
            if (!(w > 0) || !(h > 0) || float.IsInfinity(w) || float.IsInfinity(h))
            { throw new ArgumentException("Collapsed or invalid native image dimensions"); }
            x /= w; y /= h;
            // The real Maquette corpus has small accumulated transform roundoff (under
            // 0.006 degrees). Do not mistake that for deliberately sheared geometry.
            if (Math.Abs(Vector3.Dot(x, y)) > .0002f)
            { throw new NotSupportedException("Sheared image geometry"); }
            if (Vector3.Dot(Vector3.Cross(x, y), z) < 0)
            { throw new NotSupportedException("Reflected image geometry"); }
            z = Vector3.Normalize(Vector3.Cross(x, y));
            y = Vector3.Normalize(Vector3.Cross(z, x));
            var basis = new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0,
                z.X, z.Y, z.Z, 0, 0, 0, 0, 1);
            var r = placement.Rotation;
            var rotation = Quaternion.Normalize(new Quaternion(r[0], r[1], r[2], r[3]) * Quaternion.CreateFromRotationMatrix(basis));
            var p = placement.Position;
            return new NativeImageRectangle
            {
                Placement = new NativePlacement(new[] { (new Vector3(p[0], p[1], p[2]), rotation, Vector3.One) }),
                Width = w, Height = h
            };
        }
    }
}
