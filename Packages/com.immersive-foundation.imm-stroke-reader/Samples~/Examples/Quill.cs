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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using SQ = SharpQuill;
using ImmSQ = ImmStrokeReader.SharpQuill;

namespace TiltBrush
{
    public static class Quill
    {
        // Quill-specific brush GUIDs
        private const string BRUSH_QUILL_CYLINDER = "f1c4e3e7-2a9f-4b5d-8c3e-7d9a1f8e6b4c";
        private const string BRUSH_QUILL_ELLIPSE = "a2d5f6b8-9c1e-4f3a-7b8d-2e6c9f4a1d5b";
        private const string BRUSH_QUILL_CUBE = "b3e7f8c2-4d5a-1e9b-6c8f-3a7d2f1e9c4b";
        private const string BRUSH_QUILL_RIBBON = "c4f8b3e2-9d1a-5e7f-4c3b-8a6d2f9e1c7b";

        public static void Load(string path, int maxStrokes = 0, bool loadAnimations = false, string layerName = null)
        {
            SQ.Sequence sequence = TryLoadQuillFolder(path);
            if (sequence == null)
            {
                sequence = TryLoadImmFile(path);
            }

            if (sequence == null)
            {
                Debug.LogError($"Quill load failed: {path}");
                return;
            }

            LoadSequence(sequence, path, maxStrokes, loadAnimations, layerName);
        }

        private static SQ.Sequence TryLoadQuillFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return null;
            }

            return SQ.QuillSequenceReader.Read(path);
        }

        private static SQ.Sequence TryLoadImmFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            return ImmSQ.QuillSequenceReader.ReadImm(path);
        }

        private static void LoadSequence(SQ.Sequence sequence, string path, int maxStrokes, bool loadAnimations, string layerName)
        {
            int strokeCount = 0;
            List<Stroke> allCollectedStrokes = new List<Stroke>();
            List<CanvasScript> createdLayers = new List<CanvasScript>();
            List<GrabWidget> createdWidgets = new List<GrabWidget>();
            Dictionary<string, ReferenceImage> imageCache = new Dictionary<string, ReferenceImage>(StringComparer.OrdinalIgnoreCase);
            string quillImageDirectory = GetQuillImageDirectory(path);

            Matrix4x4 globalCorrection = Matrix4x4.Scale(Vector3.one * 10f);

            Matrix4x4 rootWorldNoFlip = globalCorrection * ConvertSQTransformMatrix(sequence.RootLayer.Transform, includeFlip: false);
            Matrix4x4 rootWorldWithFlip = globalCorrection * ConvertSQTransformMatrix(sequence.RootLayer.Transform, includeFlip: true);

            foreach (var topLevelLayer in sequence.RootLayer.Children)
            {
                if (!string.IsNullOrEmpty(layerName) && !LayerContainsName(topLevelLayer, layerName))
                {
                    continue;
                }

                CanvasScript obLayer = App.Scene.AddLayerNow();
                App.Scene.RenameLayer(obLayer, topLevelLayer.Name);

                Matrix4x4 topLevelWorldNoFlip = rootWorldNoFlip * ConvertSQTransformMatrix(topLevelLayer.Transform, includeFlip: false);
                Matrix4x4 topLevelWorldWithFlip = rootWorldWithFlip * ConvertSQTransformMatrix(topLevelLayer.Transform, includeFlip: true);
                obLayer.Pose = TrTransform.FromMatrix4x4(topLevelWorldNoFlip);
                createdLayers.Add(obLayer);

                TraverseAndFlattenLayers(
                    topLevelLayer,
                    topLevelWorldWithFlip,
                    obLayer,
                    ref strokeCount,
                    maxStrokes,
                    loadAnimations,
                    allCollectedStrokes,
                    createdWidgets,
                    imageCache,
                    path,
                    quillImageDirectory,
                    layerName,
                    includeAllDescendants: false);

                if (maxStrokes > 0 && strokeCount >= maxStrokes) break;
            }

            if (allCollectedStrokes.Count > 0)
            {
                foreach (var stroke in allCollectedStrokes)
                {
                    SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                }

                SketchMemoryScript.m_Instance.RenderStrokesDirectly(allCollectedStrokes);

                foreach (var layer in createdLayers)
                {
                    layer.BatchManager.FlushMeshUpdates();
                }
            }

            if (allCollectedStrokes.Count > 0 || createdWidgets.Count > 0)
            {
                var cmd = new LoadQuillCommand(allCollectedStrokes, createdLayers, createdWidgets);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);

                if (maxStrokes > 0 && strokeCount >= maxStrokes)
                {
                    Debug.LogWarning($"Reached maxStrokes limit ({maxStrokes}). Partial load complete.");
                }
            }
        }

        private static void TraverseAndFlattenLayers(
            SQ.Layer layer,
            Matrix4x4 worldXf,
            CanvasScript targetLayer,
            ref int strokeCount,
            int maxStrokes,
            bool loadAnimations,
            List<Stroke> collectedStrokes,
            List<GrabWidget> createdWidgets,
            Dictionary<string, ReferenceImage> imageCache,
            string quillProjectPath,
            string quillImageDirectory,
            string layerName,
            bool includeAllDescendants)
        {
            if (maxStrokes > 0 && strokeCount >= maxStrokes) return;

            bool hasFilter = !string.IsNullOrEmpty(layerName);
            bool isMatch = hasFilter && string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase);
            bool allowAll = includeAllDescendants || isMatch;

            if (hasFilter && !allowAll && !LayerContainsName(layer, layerName))
            {
                return;
            }

            if ((!hasFilter || allowAll) && layer is SQ.LayerPaint paint)
            {
                IEnumerable<SQ.Drawing> drawingsToLoad;
                if (loadAnimations)
                {
                    drawingsToLoad = paint.Drawings;
                }
                else
                {
                    if (paint.Frames.Count > 0)
                    {
                        int drawingIndex = paint.Frames[0];
                        if (drawingIndex >= 0 && drawingIndex < paint.Drawings.Count)
                        {
                            drawingsToLoad = new[] { paint.Drawings[drawingIndex] };
                        }
                        else
                        {
                            drawingsToLoad = paint.Drawings.Take(1);
                        }
                    }
                    else
                    {
                        drawingsToLoad = paint.Drawings.Take(1);
                    }
                }

                foreach (var drawing in drawingsToLoad)
                {
                    foreach (var sqStroke in drawing.Data.Strokes)
                    {
                        Matrix4x4 toLayerSpace = targetLayer.Pose.ToMatrix4x4().inverse * worldXf;

                        var tbStroke = ConvertStroke(sqStroke, targetLayer, toLayerSpace);
                        if (tbStroke != null)
                        {
                            collectedStrokes.Add(tbStroke);
                            strokeCount++;
                        }
                        if (maxStrokes > 0 && strokeCount >= maxStrokes) return;
                    }
                }
            }

            if ((!hasFilter || allowAll) && layer is SQ.LayerPicture picture)
            {
                var widget = CreateImageWidgetFromQuillLayer(
                    picture,
                    worldXf,
                    targetLayer,
                    imageCache,
                    quillProjectPath,
                    quillImageDirectory);
                if (widget != null)
                {
                    createdWidgets.Add(widget);
                }
            }

            if (layer is SQ.LayerGroup group)
            {
                foreach (var child in group.Children)
                {
                    Matrix4x4 childLocalXf = ConvertSQTransformMatrix(child.Transform, includeFlip: true);
                    Matrix4x4 childWorldXf = worldXf * childLocalXf;
                    TraverseAndFlattenLayers(
                        child,
                        childWorldXf,
                        targetLayer,
                        ref strokeCount,
                        maxStrokes,
                        loadAnimations,
                        collectedStrokes,
                        createdWidgets,
                        imageCache,
                        quillProjectPath,
                        quillImageDirectory,
                        layerName,
                        includeAllDescendants: allowAll);
                    if (maxStrokes > 0 && strokeCount >= maxStrokes) return;
                }
            }
        }

        private static bool LayerContainsName(SQ.Layer layer, string layerName)
        {
            if (layer == null || string.IsNullOrEmpty(layerName))
            {
                return false;
            }

            if (string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (layer is SQ.LayerGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (LayerContainsName(child, layerName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Matrix4x4 ConvertSQTransformMatrix(SQ.Transform sqXf, bool includeFlip)
        {
            Vector3 pos = new Vector3(sqXf.Translation.X, sqXf.Translation.Y, sqXf.Translation.Z);
            Quaternion rot = new Quaternion(sqXf.Rotation.X, sqXf.Rotation.Y, sqXf.Rotation.Z, sqXf.Rotation.W);
            Vector3 scale = Vector3.one * sqXf.Scale;
            Matrix4x4 rh = Matrix4x4.TRS(pos, rot, scale);

            if (includeFlip && !string.IsNullOrEmpty(sqXf.Flip) && sqXf.Flip != "N")
            {
                Vector3 flipAxis = Vector3.one;
                switch (sqXf.Flip)
                {
                    case "X":
                        flipAxis = new Vector3(-1, 1, 1);
                        break;
                    case "Y":
                        flipAxis = new Vector3(1, -1, 1);
                        break;
                    case "Z":
                        flipAxis = new Vector3(1, 1, -1);
                        break;
                }
                rh = rh * Matrix4x4.Scale(flipAxis);
            }

            Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(1, 1, -1));
            return mirror * rh * mirror;
        }

        private static Stroke ConvertStroke(SQ.Stroke sqStroke, CanvasScript targetLayer, Matrix4x4 toLayerSpace)
        {
            if (sqStroke.Vertices.Count < 2) return null;

            float maxWidth = 0f;
            foreach (var v in sqStroke.Vertices)
            {
                if (v.Width > maxWidth) maxWidth = v.Width;
            }

            string brushGuid;

            switch (sqStroke.BrushType)
            {
                case SQ.BrushType.Cylinder:
                    brushGuid = BRUSH_QUILL_CYLINDER;
                    break;
                case SQ.BrushType.Ellipse:
                    brushGuid = BRUSH_QUILL_ELLIPSE;
                    break;
                case SQ.BrushType.Cube:
                    brushGuid = BRUSH_QUILL_CUBE;
                    break;
                case SQ.BrushType.Ribbon:
                    brushGuid = BRUSH_QUILL_RIBBON;
                    break;
                default:
                    Debug.LogWarning($"Unknown Quill brush type: {sqStroke.BrushType}, falling back to Ribbon");
                    brushGuid = BRUSH_QUILL_RIBBON;
                    break;
            }

            var brush = BrushCatalog.m_Instance.GetBrush(new Guid(brushGuid)) ??
                        BrushCatalog.m_Instance.GetBrush(new Guid(BRUSH_QUILL_RIBBON));
            if (brush == null) return null;

            var color = sqStroke.Vertices[0].Color;
            var unityColor = new Color(color.R, color.G, color.B);

            var controlPoints = new List<PointerManager.ControlPoint>(sqStroke.Vertices.Count);
            List<Color32?> perPointColors = new List<Color32?>();
            uint time = 0;
            Quaternion prevOrientation = Quaternion.identity;

            for (int i = 0; i < sqStroke.Vertices.Count; i++)
            {
                var v = sqStroke.Vertices[i];

                Vector3 localPos = new Vector3(v.Position.X, v.Position.Y, -v.Position.Z);
                Vector3 localForward = ComputeTangentFromPositions(sqStroke.Vertices, i);
                Vector3 localUp = new Vector3(v.Normal.X, v.Normal.Y, -v.Normal.Z);

                Vector3 obPos = toLayerSpace.MultiplyPoint(localPos);
                Vector3 obForward = toLayerSpace.MultiplyVector(localForward);
                Vector3 obUp = toLayerSpace.MultiplyVector(localUp);

                Quaternion orient = BuildSafeOrientation(obForward, obUp, prevOrientation);
                prevOrientation = orient;

                float pressure = MapPressure(v.Width, maxWidth);

                controlPoints.Add(new PointerManager.ControlPoint
                {
                    m_Pos = obPos,
                    m_Orient = orient,
                    m_Pressure = pressure,
                    m_TimestampMs = time++
                });

                perPointColors.Add(new Color32(
                    (byte)(v.Color.R * 255f),
                    (byte)(v.Color.G * 255f),
                    (byte)(v.Color.B * 255f),
                    (byte)(v.Opacity * 255f)
                ));
            }

            float layerScale = GetUniformScale(toLayerSpace);
            var stroke = new Stroke
            {
                m_Type = Stroke.Type.NotCreated,
                m_IntendedCanvas = targetLayer,
                m_BrushGuid = brush.m_Guid,
                m_BrushScale = 1f,
                m_BrushSize = maxWidth * layerScale * 2f,
                m_Color = unityColor,
                m_Seed = 0,
                m_ControlPoints = controlPoints.ToArray(),
                m_OverrideColors = perPointColors,
                m_ColorOverrideMode = ColorOverrideMode.Replace
            };

            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Group = SketchGroupTag.None;

            return stroke;
        }

        private static Vector3 ComputeTangentFromPositions(List<SQ.Vertex> vertices, int index)
        {
            const float kEpsilon = 1e-7f;
            Vector3 currentPos = new Vector3(vertices[index].Position.X, vertices[index].Position.Y, -vertices[index].Position.Z);

            Vector3 forward = Vector3.zero;
            for (int j = index + 1; j < vertices.Count; j++)
            {
                Vector3 nextPos = new Vector3(vertices[j].Position.X, vertices[j].Position.Y, -vertices[j].Position.Z);
                Vector3 delta = nextPos - currentPos;
                if (delta.sqrMagnitude >= kEpsilon * kEpsilon)
                {
                    forward = delta.normalized;
                    break;
                }
            }

            Vector3 backward = Vector3.zero;
            for (int j = index - 1; j >= 0; j--)
            {
                Vector3 prevPos = new Vector3(vertices[j].Position.X, vertices[j].Position.Y, -vertices[j].Position.Z);
                Vector3 delta = currentPos - prevPos;
                if (delta.sqrMagnitude >= kEpsilon * kEpsilon)
                {
                    backward = delta.normalized;
                    break;
                }
            }

            Vector3 tangent = forward + backward;
            if (tangent.sqrMagnitude >= kEpsilon * kEpsilon)
            {
                return tangent.normalized;
            }

            Vector3 first = new Vector3(vertices[0].Position.X, vertices[0].Position.Y, -vertices[0].Position.Z);
            Vector3 last = new Vector3(vertices[^1].Position.X, vertices[^1].Position.Y, -vertices[^1].Position.Z);
            tangent = last - first + new Vector3(0.000001f, 0.000002f, 0.000003f);
            return tangent.normalized;
        }

        private static Quaternion BuildSafeOrientation(Vector3 fwd, Vector3 up, Quaternion prevOrientation)
        {
            const float kMinSqrMagnitude = 1e-6f;

            if (fwd.sqrMagnitude < kMinSqrMagnitude)
            {
                return prevOrientation;
            }
            fwd.Normalize();

            if (up.sqrMagnitude < kMinSqrMagnitude)
            {
                up = Vector3.Cross(fwd, Vector3.right);
                if (up.sqrMagnitude < kMinSqrMagnitude)
                {
                    up = Vector3.Cross(fwd, Vector3.up);
                }
                if (up.sqrMagnitude < kMinSqrMagnitude)
                {
                    return prevOrientation;
                }
            }

            up = up - Vector3.Dot(up, fwd) * fwd;
            if (up.sqrMagnitude < kMinSqrMagnitude)
            {
                return prevOrientation;
            }
            up.Normalize();

            return Quaternion.LookRotation(fwd, up);
        }

        private static float MapPressure(float width, float maxWidth)
        {
            if (maxWidth <= 0f) return 1f;
            float pressure = width / maxWidth;
            return Mathf.Clamp01(pressure);
        }

        private static float GetUniformScale(Matrix4x4 m)
        {
            Vector3 x = new Vector3(m.m00, m.m10, m.m20);
            float scale = x.magnitude;
            if (scale > 0)
            {
                return scale;
            }
            Vector3 y = new Vector3(m.m01, m.m11, m.m21);
            scale = y.magnitude;
            if (scale > 0)
            {
                return scale;
            }
            Vector3 z = new Vector3(m.m02, m.m12, m.m22);
            return z.magnitude;
        }
    }
}
