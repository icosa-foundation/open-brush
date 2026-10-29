// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class MaquettePlacement : NativePlacement
    {
        public MaquettePlacement(MaquetteState state) : base(Transforms(state)) { }

        private static IEnumerable<(Vector3 Position, Quaternion Rotation, Vector3 Scale)> Transforms(MaquetteState state)
        {
            if (state == null) { throw new ArgumentNullException(nameof(state)); }
            for (var current = state; current != null; current = current.Parent)
            {
                var transform = current.Data["cachedTransform"];
                var rotation = transform?["rotation"];
                yield return (Vector(transform?["position"], Vector3.Zero),
                    new Quaternion(Component(rotation, "x", 0), Component(rotation, "y", 0), Component(rotation, "z", 0), Component(rotation, "w", 1)),
                    Vector(transform?["scale"], Vector3.One));
            }
        }

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
