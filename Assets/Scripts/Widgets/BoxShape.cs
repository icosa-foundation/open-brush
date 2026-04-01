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

using UnityEngine;

namespace TiltBrush
{
    internal class BoxShape : IWidgetShape
    {
        public static readonly BoxShape Instance = new BoxShape();

        public float GetActivationScore(Transform transform, float size, float maxSize, Vector3 controllerPos)
        {
            float halfExtent = size * 0.5f * Coords.CanvasPose.scale;
            Vector3 localPos = transform.InverseTransformPoint(controllerPos);
            float xDiff = halfExtent - Mathf.Abs(localPos.x);
            float yDiff = halfExtent - Mathf.Abs(localPos.y);
            float zDiff = halfExtent - Mathf.Abs(localPos.z);
            if (xDiff <= 0f || yDiff <= 0f || zDiff <= 0f) return -1f;
            float baseScore = (xDiff / halfExtent + yDiff / halfExtent + zDiff / halfExtent) * 0.333f;
            return baseScore * Mathf.Pow(1 - size / maxSize, 2);
        }

        public Bounds GetSelectionCanvasBounds(Collider collider, Bounds fallback)
        {
            BoxCollider box = collider as BoxCollider;
            if (box == null) return fallback;

            TrTransform toCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
                TrTransform.FromTransform(box.transform);
            Bounds bounds = new Bounds(toCanvasXf * box.center, Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                bounds.Encapsulate(toCanvasXf * (box.center + Vector3.Scale(
                    box.size,
                    new Vector3((i & 1) == 0 ? -0.5f : 0.5f,
                        (i & 2) == 0 ? -0.5f : 0.5f,
                        (i & 4) == 0 ? -0.5f : 0.5f))));
            }
            return bounds;
        }
    }
} // namespace TiltBrush
