// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using UnityEngine;

namespace TiltBrush
{
    internal class SphereShape : IWidgetShape
    {
        public static readonly SphereShape Instance = new SphereShape();

        float IWidgetShape.GetActivationScore(Transform transform, Collider collider, float size, float maxSize, Vector3 controllerPos)
            => GetActivationScore(transform, size, maxSize, controllerPos);

        Bounds IWidgetShape.GetSelectionCanvasBounds(Collider collider, Bounds fallback)
            => collider != null ? GetSelectionCanvasBounds(collider) : fallback;

        public static Vector3 GetExtents(float size)
        {
            return size * Vector3.one;
        }

        public static float GetSizeFromExtents(Vector3 extents)
        {
            if (extents.x != extents.y || extents.x != extents.z)
            {
                throw new ArgumentException("Sphere does not support non-uniform extents");
            }

            return extents.x;
        }

        public static void FindClosestPointOnSurface(
            Transform transform,
            float signedWidgetSize,
            Vector3 pos,
            out Vector3 surfacePos,
            out Vector3 surfaceNorm)
        {
            Vector3 centerToPos = pos - transform.position;
            float radius = Mathf.Abs(signedWidgetSize) * 0.5f * Coords.CanvasPose.scale;
            surfacePos = transform.position + centerToPos.normalized * radius;
            surfaceNorm = centerToPos;
        }

        public static float GetActivationScore(
            Transform transform,
            float signedWidgetSize,
            float maxSizeCs,
            Vector3 controllerPos)
        {
            float radius = Mathf.Abs(signedWidgetSize) * 0.5f * Coords.CanvasPose.scale;
            float baseScore = 1.0f - (transform.position - controllerPos).magnitude / radius;
            if (baseScore < 0)
            {
                return baseScore;
            }

            return baseScore * Mathf.Pow(1 - signedWidgetSize / maxSizeCs, 2);
        }

        public static Bounds GetSelectionCanvasBounds(Collider collider)
        {
            SphereCollider sphere = collider as SphereCollider;
            if (sphere == null)
            {
                return new Bounds();
            }

            TrTransform colliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
                TrTransform.FromTransform(collider.transform);
            Bounds bounds = new Bounds(colliderToCanvasXf * sphere.center, Vector3.zero);

            colliderToCanvasXf.rotation = Quaternion.identity;
            bounds.Encapsulate(colliderToCanvasXf * (sphere.center + sphere.radius * Vector3.one));
            bounds.Encapsulate(colliderToCanvasXf * (sphere.center - sphere.radius * Vector3.one));
            return bounds;
        }
    }
} // namespace TiltBrush
