// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    /// Import-time decomposition into rigid widget placement and intrinsic geometry transforms.
    /// This is not a second serialization format: intrinsic transforms use ordinary recipe steps.
    public sealed class MaquettePlacement
    {
        public float[] Position { get; }
        public float[] Rotation { get; }
        private readonly List<(Vector3 Scale, Quaternion Rotation)> m_LinearSteps = new List<(Vector3, Quaternion)>();
        private readonly Quaternion m_InverseRotation;

        public MaquettePlacement(MaquetteState state)
        {
            if (state == null) { throw new ArgumentNullException(nameof(state)); }
            Matrix4x4 world = Matrix4x4.Identity;
            Quaternion rotation = Quaternion.Identity;
            for (var current = state; current != null; current = current.Parent)
            {
                var transform = current.Data["cachedTransform"];
                Vector3 position = Vector(transform?["position"], Vector3.Zero);
                Vector3 scale = Vector(transform?["scale"], Vector3.One);
                var r = transform?["rotation"];
                var q = new Quaternion(Component(r, "x", 0), Component(r, "y", 0), Component(r, "z", 0), Component(r, "w", 1));
                if (q.LengthSquared() <= 0 || float.IsInfinity(q.LengthSquared())) { throw new InvalidDataException("Invalid Maquette rotation quaternion"); }
                q = Quaternion.Normalize(q);
                world *= Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(position);
                rotation = q * rotation;
                m_LinearSteps.Add((scale, q));
            }
            rotation = Quaternion.Normalize(rotation);
            m_InverseRotation = Quaternion.Conjugate(rotation);
            Position = new[] { world.M41, world.M42, world.M43 };
            foreach (float value in Position)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) { throw new InvalidDataException("Maquette hierarchy overflows its position"); }
            }
            Rotation = new[] { rotation.X, rotation.Y, rotation.Z, rotation.W };
        }

        /// Keep widget scale uniform. In particular, rotated children under nonuniformly scaled
        /// parents retain their shear through ordered scale/rotation steps, without decomposition.
        public GeometryRecipeData BakeLinearTransform(GeometryRecipeData recipe)
        {
            recipe.GetSteps(out _, out _);
            var data = recipe.ToJson();
            var operations = data["Operations"] as JArray;
            if (operations == null) { operations = new JArray(); data["Operations"] = operations; }
            foreach (var step in m_LinearSteps)
            {
                if (step.Scale != Vector3.One || step.Rotation != Quaternion.Identity)
                {
                    operations.Add(Transform(step.Scale, step.Rotation));
                }
            }
            if (m_InverseRotation != Quaternion.Identity) { operations.Add(Transform(Vector3.One, m_InverseRotation)); }
            return new GeometryRecipeData(data);
        }

        /// Column-major affine transform for intrinsic content such as text glyphs. Placement
        /// translation stays on the widget; the content offset, shear and unit conversion stay here.
        public float[] GetIntrinsicTransform(float[] localScale, float[] localOffset, float unitsPerMetre)
        {
            if (!(unitsPerMetre > 0) || float.IsInfinity(unitsPerMetre)) { throw new ArgumentException("Expected a finite positive unit conversion", nameof(unitsPerMetre)); }
            if (localScale == null || localScale.Length != 3 || localOffset == null || localOffset.Length != 3)
            {
                throw new ArgumentException("Expected three intrinsic scale and offset components");
            }
            var matrix = Matrix4x4.CreateScale(localScale[0], localScale[1], localScale[2])
                         * Matrix4x4.CreateTranslation(localOffset[0], localOffset[1], localOffset[2]);
            foreach (var step in m_LinearSteps)
            {
                matrix *= Matrix4x4.CreateScale(step.Scale) * Matrix4x4.CreateFromQuaternion(step.Rotation);
            }
            matrix *= Matrix4x4.CreateFromQuaternion(m_InverseRotation) * Matrix4x4.CreateScale(unitsPerMetre);
            var result = new[]
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            };
            foreach (float value in result)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) { throw new InvalidDataException("Invalid intrinsic content transform"); }
            }
            return result;
        }

        private static JObject Transform(Vector3 scale, Quaternion rotation) => new JObject
        {
            ["Type"] = "transform", ["Parameters"] = new JObject
            {
                ["scale"] = new JArray(scale.X, scale.Y, scale.Z),
                ["rotation"] = new JArray(rotation.X, rotation.Y, rotation.Z, rotation.W)
            }
        };

        private static Vector3 Vector(JToken value, Vector3 fallback) => new Vector3(
            Component(value, "x", fallback.X), Component(value, "y", fallback.Y), Component(value, "z", fallback.Z));

        private static float Component(JToken parent, string name, float fallback)
        {
            var value = parent?[name];
            if (value == null) { return fallback; }
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) { throw new InvalidDataException("Maquette transform component must be numeric"); }
            float result = (float)value;
            if (float.IsNaN(result) || float.IsInfinity(result)) { throw new InvalidDataException("Maquette transform component must be finite"); }
            return result;
        }
    }
}
