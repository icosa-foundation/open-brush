// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    /// Paired ribbon edges in import-canvas coordinates, consumed by an ordinary stroke brush.
    /// This transient import data is not an additional sketch serialization format.
    public sealed class NativeRibbonData
    {
        public float[][] FirstEdge { get; internal set; }
        public float[][] SecondEdge { get; internal set; }
    }

    public static class MaquetteStrokeAdapter
    {
        public static NativeRibbonData Read(MaquetteDocument document, MaquetteState state, float unitsPerMetre)
        {
            if (state?.Entity?.Kind != MaquetteEntityKind.Stroke) { throw new ArgumentException("Expected a Maquette stroke", nameof(state)); }
            if (!(unitsPerMetre > 0) || float.IsInfinity(unitsPerMetre)) { throw new ArgumentException("Invalid stroke units", nameof(unitsPerMetre)); }
            string mode = (string)state.Entity.Data["drawingMode"];
            bool straight = mode == "Straight";
            if (!straight && mode != "Curved") { throw new NotSupportedException($"Unsupported Maquette drawing mode: {mode}"); }
            int version = (int?)document.Sections["brushes.json"]["version"] ?? document.Version;
            bool legacyStraightFrame = straight && version >= 2 && version < 4;
            if (!(state.Entity.Data["controlPoints"] is JArray controls) || controls.Count < 2)
            {
                throw new InvalidDataException("A Maquette ribbon needs at least two paired control points");
            }
            Vector3[] first = controls.Select(control => Vector(control["point1"])).ToArray();
            Vector3[] second = controls.Select(control => Vector(control["point2"])).ToArray();
            var a = new List<Vector3>();
            var b = new List<Vector3>();
            Quaternion constructionRotation = Quaternion.Identity;
            if (straight)
            {
                // Straight drawing keeps the first width and center but aligns its first
                // edge with the final edge. Intermediate controls do not change the quad.
                Vector3 width = second[second.Length - 1] - first[first.Length - 1];
                Vector3 center = (first[0] + second[0]) * .5f;
                Vector3 half = Normalize(width, "straight ribbon width") * Vector3.Distance(first[0], second[0]) * .5f;
                a.Add(center - half); b.Add(center + half);
                a.Add(first[first.Length - 1]); b.Add(second[second.Length - 1]);
                Vector3 forward = Normalize(b[0] - a[0], "straight ribbon width");
                Vector3 up = Normalize(Vector3.Cross(forward, a[1] - a[0]), "straight ribbon normal");
                Vector3 right = Vector3.Cross(up, forward);
                constructionRotation = Quaternion.CreateFromRotationMatrix(new Matrix4x4(
                    right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0,
                    forward.X, forward.Y, forward.Z, 0, 0, 0, 0, 1));
            }
            else
            {
                // The native ribbon samples each edge's cubic independently, five times
                // per span. Shared endpoints are emitted once; no centerline fitting occurs.
                for (int span = 0; span < first.Length - 1; ++span)
                {
                    for (int step = span == 0 ? 0 : 1; step <= 5; ++step)
                    {
                        float t = step / 5f;
                        a.Add(Hermite(first, span, t)); b.Add(Hermite(second, span, t));
                    }
                }
            }
            Vector3 low = a.Concat(b).Aggregate(Vector3.Min), high = a.Concat(b).Aggregate(Vector3.Max);
            Vector3 boundsCenter = (low + high) * .5f;
            // v2/v3 states were saved before the straight ribbon's construction-frame
            // correction. Native migration composes that correction into every state and
            // its inverse into the mesh. They cancel here: retain the original centered
            // points and saved state rather than applying only half of the migration.
            Quaternion inverseConstruction = legacyStraightFrame ? Quaternion.Identity : Quaternion.Conjugate(constructionRotation);
            var placement = new MaquettePlacement(state);
            float[] matrix = placement.GetIntrinsicTransform(new[] { 1f, 1f, 1f }, new[] { 0f, 0f, 0f }, unitsPerMetre);
            var linear = new Matrix4x4(matrix[0], matrix[1], matrix[2], matrix[3], matrix[4], matrix[5], matrix[6], matrix[7],
                matrix[8], matrix[9], matrix[10], matrix[11], matrix[12], matrix[13], matrix[14], matrix[15]);
            var q = placement.Rotation;
            linear *= Matrix4x4.CreateFromQuaternion(new Quaternion(q[0], q[1], q[2], q[3]));
            Vector3 position = new Vector3(placement.Position[0], placement.Position[1], placement.Position[2]) * unitsPerMetre;
            float[] ConvertPoint(Vector3 point)
            {
                point = Vector3.Transform(Vector3.Transform(point - boundsCenter, inverseConstruction), linear) + position;
                if (!Finite(point.X) || !Finite(point.Y) || !Finite(point.Z)) { throw new InvalidDataException("Stroke transformation exceeds supported range"); }
                return new[] { point.X, point.Y, point.Z };
            }
            return new NativeRibbonData { FirstEdge = a.Select(ConvertPoint).ToArray(), SecondEdge = b.Select(ConvertPoint).ToArray() };
        }

        private static Vector3 Hermite(Vector3[] points, int span, float t)
        {
            Vector3 Tangent(int i) => i == 0 ? points[1] - points[0] : i == points.Length - 1
                ? points[i] - points[i - 1] : (points[i + 1] - points[i - 1]) * .5f;
            float t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * points[span] + (t3 - 2 * t2 + t) * Tangent(span) +
                (-2 * t3 + 3 * t2) * points[span + 1] + (t3 - t2) * Tangent(span + 1);
        }

        private static Vector3 Normalize(Vector3 value, string name)
        {
            float length = value.Length();
            if (!(length > 0) || !Finite(length)) { throw new InvalidDataException($"Invalid Maquette {name}"); }
            return value / length;
        }

        private static Vector3 Vector(JToken value)
        {
            if (!(value is JObject)) { throw new InvalidDataException("Missing Maquette ribbon edge point"); }
            float Number(string key)
            {
                var token = value[key];
                if (token == null || token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                {
                    throw new InvalidDataException("Expected a numeric Maquette ribbon coordinate");
                }
                float number = (float)token;
                if (!Finite(number)) { throw new InvalidDataException("Maquette ribbon coordinates must be finite"); }
                return number;
            }
            return new Vector3(Number("x"), Number("y"), Number("z"));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
