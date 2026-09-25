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
        private const int kBoxVertexCount = 12 * 12;

        private static readonly int kUseGridOrigin = Shader.PropertyToID("_UseGridOrigin");
        private static readonly int kGridOrigin = Shader.PropertyToID("_GridOrigin");
        private static readonly int kPreviewMode = Shader.PropertyToID("_PreviewMode");
        private static readonly int kBoundsMin = Shader.PropertyToID("_BoundsMin");
        private static readonly int kBoundsMax = Shader.PropertyToID("_BoundsMax");

        private static Material s_Material;
        private static int s_Layer;
        private static MaterialPropertyBlock s_GridProperties;
        private static MaterialPropertyBlock s_BoundsProperties;
        private static MaterialPropertyBlock s_TargetProperties;

        public static void Draw(
            RuntimeVoxDocument.RuntimeModel model,
            TrTransform modelToCanvas,
            Vector3 canvasPosition,
            Color color)
            => Draw(model, modelToCanvas, canvasPosition, color, color);

        public static void Draw(
            RuntimeVoxDocument.RuntimeModel model,
            TrTransform modelToCanvas,
            Vector3 canvasPosition,
            Color targetColor,
            Color guideColor,
            bool showGrid = true)
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
            bool isInside = model.IsInBounds(cell);
            Vector3Int gridOrigin = new Vector3Int(
                Mathf.Clamp(cell.x - gridCount.x / 2, 0, model.Size.x - gridCount.x),
                Mathf.Clamp(cell.y - gridCount.y / 2, 0, model.Size.y - gridCount.y),
                Mathf.Clamp(cell.z - gridCount.z / 2, 0, model.Size.z - gridCount.z));

            TrTransform canvasToWorld = App.Scene.ActiveCanvas.Pose;
            TrTransform modelToWorld = canvasToWorld * modelToCanvas;
            Vector3 pointerWorld = canvasToWorld * canvasPosition;

            Vector3 localCenter = new Vector3(
                (model.Size.x - 1) * 0.5f,
                (model.Size.y - 1) * 0.5f,
                (model.Size.z - 1) * 0.5f);
            float worldDiameter = Mathf.Max(model.Size.x, Mathf.Max(model.Size.y, model.Size.z)) *
                Mathf.Abs(modelToWorld.scale) * 2f;
            var worldBounds = new Bounds(
                modelToWorld * localCenter,
                Vector3.one * Mathf.Max(worldDiameter, 0.1f));
            if (showGrid)
            {
                s_GridProperties ??= new MaterialPropertyBlock();
                s_GridProperties.Clear();
                s_GridProperties.SetMatrix(
                    SnapGrid3D.ShaderParam.CanvasToWorldMatrix,
                    modelToWorld.ToMatrix4x4());
                s_GridProperties.SetMatrix(
                    SnapGrid3D.ShaderParam.WorldToCanvasMatrix,
                    modelToWorld.inverse.ToMatrix4x4());
                s_GridProperties.SetVector(SnapGrid3D.ShaderParam.Pointer, pointerWorld);
                s_GridProperties.SetVector(
                    SnapGrid3D.ShaderParam.CanvasOrigin,
                    modelToWorld.translation);
                s_GridProperties.SetColor(
                    SnapGrid3D.ShaderParam.Color,
                    new Color(
                        guideColor.r * 0.45f,
                        guideColor.g * 0.45f,
                        guideColor.b * 0.45f,
                        0.5f));
                s_GridProperties.SetVector(
                    SnapGrid3D.ShaderParam.GridCount,
                    (Vector3)gridCount);
                s_GridProperties.SetFloat(SnapGrid3D.ShaderParam.GridInterval, 1f);
                s_GridProperties.SetFloat(SnapGrid3D.ShaderParam.LineWidth, 0.03f);
                s_GridProperties.SetFloat(SnapGrid3D.ShaderParam.LineLength, 0.32f);
                s_GridProperties.SetFloat(
                    SnapGrid3D.ShaderParam.CanvasScale,
                    Mathf.Abs(modelToWorld.scale));
                s_GridProperties.SetFloat(kUseGridOrigin, 1f);
                s_GridProperties.SetVector(kGridOrigin, (Vector3)gridOrigin);
                s_GridProperties.SetFloat(kPreviewMode, 0f);

                var renderParams = new RenderParams(s_Material)
                {
                    layer = s_Layer,
                    matProps = s_GridProperties,
                    worldBounds = worldBounds,
                };
                int vertexCount = gridCount.x * gridCount.y * gridCount.z * kVerticesPerGridPoint;
                Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, vertexCount);
            }

            Color boundsColor = isInside
                ? new Color(guideColor.r, guideColor.g, guideColor.b, 0.35f)
                : new Color(0.65f, 0.08f, 0.08f, 0.5f);
            DrawBox(
                modelToWorld,
                new Vector3(-0.5f, -0.5f, -0.5f),
                (Vector3)model.Size - Vector3.one * 0.5f,
                boundsColor,
                0.035f,
                worldBounds,
                ref s_BoundsProperties);

            if (isInside)
            {
                DrawBox(
                    modelToWorld,
                    (Vector3)cell - Vector3.one * 0.5f,
                    (Vector3)cell + Vector3.one * 0.5f,
                    new Color(targetColor.r, targetColor.g, targetColor.b, 0.9f),
                    0.075f,
                    worldBounds,
                    ref s_TargetProperties);
            }
        }

        private static void DrawBox(
            TrTransform modelToWorld,
            Vector3 boundsMin,
            Vector3 boundsMax,
            Color color,
            float lineWidth,
            Bounds worldBounds,
            ref MaterialPropertyBlock properties)
        {
            properties ??= new MaterialPropertyBlock();
            properties.Clear();
            properties.SetMatrix(
                SnapGrid3D.ShaderParam.CanvasToWorldMatrix,
                modelToWorld.ToMatrix4x4());
            properties.SetColor(SnapGrid3D.ShaderParam.Color, color);
            properties.SetFloat(SnapGrid3D.ShaderParam.LineWidth, lineWidth);
            properties.SetFloat(kPreviewMode, 1f);
            properties.SetVector(kBoundsMin, boundsMin);
            properties.SetVector(kBoundsMax, boundsMax);

            var renderParams = new RenderParams(s_Material)
            {
                layer = s_Layer,
                matProps = properties,
                worldBounds = worldBounds,
            };
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, kBoxVertexCount);
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
