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

namespace TiltBrush
{
    public static class Quill
    {
        // Quill-specific brush GUIDs
        private const string BRUSH_QUILL_CYLINDER = "f1c4e3e7-2a9f-4b5d-8c3e-7d9a1f8e6b4c";
        private const string BRUSH_QUILL_ELLIPSE = "a2d5f6b8-9c1e-4f3a-7b8d-2e6c9f4a1d5b";
        private const string BRUSH_QUILL_CUBE = "b3e7f8c2-4d5a-1e9b-6c8f-3a7d2f1e9c4b";
        private const string BRUSH_QUILL_RIBBON = "c4f8b3e2-9d1a-5e7f-4c3b-8a6d2f9e1c7b";

        public static void Load(string path, int maxStrokes = 0, bool loadAnimations = false)
        {
            if (!Directory.Exists(path))
            {
                Debug.LogError($"Quill project path not found: {path}");
                return;
            }

            var sequence = SQ.QuillSequenceReader.Read(path);
            if (sequence == null)
            {
                Debug.LogError("Failed to read Quill sequence");
                return;
            }

            // Recurse layers and collect all strokes
            int strokeCount = 0;
            List<Stroke> allCollectedStrokes = new List<Stroke>();
            List<CanvasScript> createdLayers = new List<CanvasScript>();

            // Apply 10x global scale (Meters to Centimeters/Unity units normalization)
            TrTransform globalCorrection = TrTransform.S(10f);

            // The RootLayer itself can have a transform
            TrTransform rootWorldXf = globalCorrection * ConvertSQTransform(sequence.RootLayer.Transform);

            // Iterate only over top-level layers of the root group
            foreach (var topLevelLayer in sequence.RootLayer.Children)
            {
                // Create exactly one OB layer for each top-level Quill layer
                CanvasScript obLayer = App.Scene.AddLayerNow();
                App.Scene.RenameLayer(obLayer, topLevelLayer.Name);

                // Calculate world transform for this top-level layer
                TrTransform topLevelWorldXf = rootWorldXf * ConvertSQTransform(topLevelLayer.Transform);
                obLayer.LocalPose = topLevelWorldXf;
                createdLayers.Add(obLayer);

                // Recurse into children and flatten ALL descendant strokes into this obLayer
                TraverseAndFlattenLayers(topLevelLayer, topLevelWorldXf, obLayer, ref strokeCount, maxStrokes, loadAnimations, allCollectedStrokes);

                if (maxStrokes > 0 && strokeCount >= maxStrokes) break;
            }

            if (allCollectedStrokes.Count > 0)
            {
                // Register strokes in memory list first
                foreach (var stroke in allCollectedStrokes)
                {
                    SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                }

                // Optimized batch rendering
                SketchMemoryScript.m_Instance.RenderStrokesDirectly(allCollectedStrokes);

                // Finalize batches
                foreach (var layer in createdLayers)
                {
                    layer.BatchManager.FlushMeshUpdates();
                }

                // Single undo step for all strokes and layers
                var cmd = new LoadQuillCommand(allCollectedStrokes, createdLayers);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);

                if (maxStrokes > 0 && strokeCount >= maxStrokes)
                {
                    Debug.LogWarning($"Reached maxStrokes limit ({maxStrokes}). Partial load complete.");
                }
            }
        }

        private static void TraverseAndFlattenLayers(SQ.Layer layer, TrTransform worldXf, CanvasScript targetLayer, ref int strokeCount, int maxStrokes, bool loadAnimations, List<Stroke> collectedStrokes)
        {
            if (maxStrokes > 0 && strokeCount >= maxStrokes) return;

            // 1. Process strokes in this layer if it's a Paint layer
            if (layer is SQ.LayerPaint paint)
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
                        // Calculate transform from Quill world space to the OB targetLayer's local space
                        TrTransform toLayerSpace = targetLayer.LocalPose.inverse * worldXf;

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

            // 2. Recurse into children if it's a Group layer
            if (layer is SQ.LayerGroup group)
            {
                foreach (var child in group.Children)
                {
                    TrTransform childLocalXf = ConvertSQTransform(child.Transform);
                    TrTransform childWorldXf = worldXf * childLocalXf;
                    TraverseAndFlattenLayers(child, childWorldXf, targetLayer, ref strokeCount, maxStrokes, loadAnimations, collectedStrokes);
                    if (maxStrokes > 0 && strokeCount >= maxStrokes) return;
                }
            }
        }

        private static TrTransform ConvertSQTransform(SQ.Transform sqXf)
        {
            // Quill is Right-Handed Y-up. Unity is Left-Handed Y-up.
            // Flip Z for position and rotation.
            Vector3 pos = new Vector3(sqXf.Translation.X, sqXf.Translation.Y, -sqXf.Translation.Z);

            // To flip a quaternion across the XY plane (flipping Z):
            // The X and Y components are negated, while Z and W remain the same.
            Quaternion rot = new Quaternion(-sqXf.Rotation.X, -sqXf.Rotation.Y, sqXf.Rotation.Z, sqXf.Rotation.W);

            float scale = sqXf.Scale;
            return TrTransform.TRS(pos, rot, scale);
        }

        /// <summary>
        /// Computes tangent at a control point by averaging forward and backward differences.
        /// This matches Quill's official importer behavior (Element::ComputeTangent).
        /// Ignores Quill's stored tangent data to achieve smooth interpolation.
        /// </summary>
        private static Vector3 ComputeTangentFromPositions(List<SQ.Vertex> vertices, int index)
        {
            const float kEpsilon = 1e-7f;
            Vector3 currentPos = new Vector3(vertices[index].Position.X, vertices[index].Position.Y, -vertices[index].Position.Z);

            // Find first valid forward difference
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

            // Find first valid backward difference
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

            // Average the two directions
            Vector3 tangent = forward + backward;
            if (tangent.sqrMagnitude >= kEpsilon * kEpsilon)
            {
                return tangent.normalized;
            }

            // Fallback: overall stroke direction with tiny offset to avoid zero vector
            Vector3 first = new Vector3(vertices[0].Position.X, vertices[0].Position.Y, -vertices[0].Position.Z);
            Vector3 last = new Vector3(vertices[^1].Position.X, vertices[^1].Position.Y, -vertices[^1].Position.Z);
            tangent = last - first + new Vector3(0.000001f, 0.000002f, 0.000003f);
            return tangent.normalized;
        }

        /// <summary>
        /// Builds a safe orthonormal orientation from forward and up vectors.
        /// Uses Gram-Schmidt orthogonalization to ensure valid basis.
        /// Falls back to previous orientation if vectors are degenerate.
        /// </summary>
        private static Quaternion BuildSafeOrientation(Vector3 fwd, Vector3 up, Quaternion prevOrientation)
        {
            const float kMinSqrMagnitude = 1e-6f;

            // Check if forward is valid
            if (fwd.sqrMagnitude < kMinSqrMagnitude)
            {
                return prevOrientation;
            }
            fwd.Normalize();

            // Check if up is valid; if not, construct a perpendicular vector
            if (up.sqrMagnitude < kMinSqrMagnitude)
            {
                // Try to find a perpendicular using cross product with cardinal axes
                up = Vector3.Cross(fwd, Vector3.right);
                if (up.sqrMagnitude < kMinSqrMagnitude)
                {
                    up = Vector3.Cross(fwd, Vector3.up);
                }
                if (up.sqrMagnitude < kMinSqrMagnitude)
                {
                    // Degenerate case - fallback to previous orientation
                    return prevOrientation;
                }
            }

            // Gram-Schmidt orthogonalization: make up perpendicular to fwd
            up = up - Vector3.Dot(up, fwd) * fwd;
            if (up.sqrMagnitude < kMinSqrMagnitude)
            {
                // After orthogonalization, up became zero - fallback
                return prevOrientation;
            }
            up.Normalize();

            return Quaternion.LookRotation(fwd, up);
        }

        /// <summary>
        /// Maps Quill width to Open Brush pressure.
        /// Currently uses linear mapping but can be extended for per-brush tuning.
        /// </summary>
        private static float MapPressure(float width, float maxWidth)
        {
            if (maxWidth <= 0f) return 1f;

            // Linear mapping for now
            float pressure = width / maxWidth;

            // Future: Add per-brush pressure curves here if needed
            // switch (brushGuid)
            // {
            //     case BRUSH_QUILL_CYLINDER:
            //         pressure = Mathf.Pow(pressure, 1.2f); // Example adjustment
            //         break;
            // }

            return Mathf.Clamp01(pressure);
        }

        private static Stroke ConvertStroke(SQ.Stroke sqStroke, CanvasScript targetLayer, TrTransform toLayerSpace)
        {
            if (sqStroke.Vertices.Count < 2) return null;

            // Calculate max width for thresholding
            float maxWidth = 0f;
            foreach (var v in sqStroke.Vertices)
            {
                if (v.Width > maxWidth) maxWidth = v.Width;
            }

            // Determine Brush GUID - map to Quill-specific brushes
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

            var brush = BrushCatalog.m_Instance.GetBrush(new Guid(brushGuid));
            if (brush == null)
            {
                brush = BrushCatalog.m_Instance.GetBrush(new Guid(BRUSH_QUILL_RIBBON));
            }
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

                // Convert Quill vertex position (Right-Handed) to Unity (Left-Handed) by flipping Z
                Vector3 localPos = new Vector3(v.Position.X, v.Position.Y, -v.Position.Z);

                // This matches the working Python importer and creates smooth interpolation
                Vector3 localForward = ComputeTangentFromPositions(sqStroke.Vertices, i);

                // Use Quill's stored tangent for now (will compute from positions later)
                // Vector3 localForward = new Vector3(v.Tangent.X, v.Tangent.Y, -v.Tangent.Z);

                // Use Quill's normal for orientation
                Vector3 localUp = new Vector3(v.Normal.X, v.Normal.Y, -v.Normal.Z);

                // Apply relative transform to keep coordinates local to the top-level OB layer
                Vector3 obPos = toLayerSpace * localPos;
                Vector3 obForward = toLayerSpace.rotation * localForward;
                Vector3 obUp = toLayerSpace.rotation * localUp;

                // Build safe orthonormal orientation with fallback
                Quaternion orient = BuildSafeOrientation(obForward, obUp, prevOrientation);
                prevOrientation = orient;

                // Map width to pressure with potential per-brush tuning
                float pressure = MapPressure(v.Width, maxWidth);

                controlPoints.Add(new PointerManager.ControlPoint
                {
                    m_Pos = obPos,
                    m_Orient = orient,
                    m_Pressure = pressure,
                    m_TimestampMs = time++
                });

                // Capture per-point color from Quill vertex
                perPointColors.Add(new Color32(
                    (byte)(v.Color.R * 255f),
                    (byte)(v.Color.G * 255f),
                    (byte)(v.Color.B * 255f),
                    255 // Quill colors don't have alpha, assume fully opaque
                ));
            }

            var stroke = new Stroke
            {
                m_Type = Stroke.Type.NotCreated,
                m_IntendedCanvas = targetLayer,
                m_BrushGuid = brush.m_Guid,
                m_BrushScale = 1f,
                // Quill width appears to be radius, but OB expects diameter (or vice versa)
                // Multiply by 2 to match Quill's visual appearance
                m_BrushSize = maxWidth * toLayerSpace.scale * 2f,
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
    }
}
