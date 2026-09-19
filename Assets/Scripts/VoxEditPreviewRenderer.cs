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
    internal static class VoxEditPreviewRenderer
    {
        private const int kGridDiameter = 5;
        private const int kVerticesPerGridPoint = 36;

        private static readonly int kUseGridOrigin = Shader.PropertyToID("_UseGridOrigin");
        private static readonly int kGridOrigin = Shader.PropertyToID("_GridOrigin");

        private static Material s_Material;
        private static int s_Layer;
        private static MaterialPropertyBlock s_MaterialProperties;

        public static void Draw(
            RuntimeVoxDocument.RuntimeModel model,
            TrTransform modelToCanvas,
            Vector3 canvasPosition,
            Color color)
        {
            if (model == null || !TryGetMaterial())
            {
                return;
            }

            Vector3Int gridCount = new Vector3Int(
                Mathf.Min(kGridDiameter, model.Size.x),
                Mathf.Min(kGridDiameter, model.Size.y),
                Mathf.Min(kGridDiameter, model.Size.z));
            Vector3Int cell = Vector3Int.RoundToInt(modelToCanvas.inverse * canvasPosition);
            Vector3Int gridOrigin = new Vector3Int(
                Mathf.Clamp(cell.x - gridCount.x / 2, 0, model.Size.x - gridCount.x),
                Mathf.Clamp(cell.y - gridCount.y / 2, 0, model.Size.y - gridCount.y),
                Mathf.Clamp(cell.z - gridCount.z / 2, 0, model.Size.z - gridCount.z));

            TrTransform canvasToWorld = App.Scene.ActiveCanvas.Pose;
            TrTransform modelToWorld = canvasToWorld * modelToCanvas;
            Vector3 pointerWorld = canvasToWorld * canvasPosition;

            s_MaterialProperties ??= new MaterialPropertyBlock();
            s_MaterialProperties.Clear();
            s_MaterialProperties.SetMatrix(
                SnapGrid3D.ShaderParam.CanvasToWorldMatrix,
                modelToWorld.ToMatrix4x4());
            s_MaterialProperties.SetMatrix(
                SnapGrid3D.ShaderParam.WorldToCanvasMatrix,
                modelToWorld.inverse.ToMatrix4x4());
            s_MaterialProperties.SetVector(SnapGrid3D.ShaderParam.Pointer, pointerWorld);
            s_MaterialProperties.SetVector(
                SnapGrid3D.ShaderParam.CanvasOrigin,
                modelToWorld.translation);
            s_MaterialProperties.SetColor(
                SnapGrid3D.ShaderParam.Color,
                new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 0.5f));
            s_MaterialProperties.SetVector(
                SnapGrid3D.ShaderParam.GridCount,
                (Vector3)gridCount);
            s_MaterialProperties.SetFloat(SnapGrid3D.ShaderParam.GridInterval, 1f);
            s_MaterialProperties.SetFloat(SnapGrid3D.ShaderParam.LineWidth, 0.03f);
            s_MaterialProperties.SetFloat(SnapGrid3D.ShaderParam.LineLength, 0.32f);
            s_MaterialProperties.SetFloat(
                SnapGrid3D.ShaderParam.CanvasScale,
                Mathf.Abs(modelToWorld.scale));
            s_MaterialProperties.SetFloat(kUseGridOrigin, 1f);
            s_MaterialProperties.SetVector(kGridOrigin, (Vector3)gridOrigin);

            Vector3 localCenter = new Vector3(
                (model.Size.x - 1) * 0.5f,
                (model.Size.y - 1) * 0.5f,
                (model.Size.z - 1) * 0.5f);
            float worldDiameter = Mathf.Max(model.Size.x, Mathf.Max(model.Size.y, model.Size.z)) *
                Mathf.Abs(modelToWorld.scale) * 2f;
            var renderParams = new RenderParams(s_Material)
            {
                layer = s_Layer,
                matProps = s_MaterialProperties,
                worldBounds = new Bounds(
                    modelToWorld * localCenter,
                    Vector3.one * Mathf.Max(worldDiameter, 0.1f)),
            };
            int vertexCount = gridCount.x * gridCount.y * gridCount.z * kVerticesPerGridPoint;
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, vertexCount);
        }

        private static bool TryGetMaterial()
        {
            if (s_Material != null)
            {
                return true;
            }

            if (App.Scene?.MainCanvas == null)
            {
                return false;
            }

            SnapGrid3D snapGrid = App.Scene.MainCanvas.GetComponentInChildren<SnapGrid3D>(true);
            if (snapGrid == null || snapGrid.material == null)
            {
                return false;
            }

            s_Material = snapGrid.material;
            s_Layer = snapGrid.gameObject.layer;
            return true;
        }
    }
}
