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
    public static partial class ApiMethods
    {
        [ApiEndpoint("load.quill", "Loads a Quill sketch from the given path")]
        public static void LoadQuill(string path, int maxStrokes = 0, bool loadAnimations = false)
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

            // Recurse layers and collect all strokes and created layers
            int strokeCount = 0;
            List<Stroke> allCollectedStrokes = new List<Stroke>();
            List<CanvasScript> createdLayers = new List<CanvasScript>();

            // Iterate only over top-level layers of the root group
            foreach (var topLevelLayer in sequence.RootLayer.Children)
            {
                // Create exactly one OB layer for each top-level Quill layer
                CanvasScript obLayer = App.Scene.AddLayerNow();
                App.Scene.RenameLayer(obLayer, topLevelLayer.Name);
                
                // Set the top-level layer's transform
                TrTransform topLevelXf = ConvertSQTransform(topLevelLayer.Transform);
                obLayer.LocalPose = topLevelXf;
                createdLayers.Add(obLayer);

                // Recurse into children and flatten ALL descendant strokes into this obLayer
                // Start with the topLevelLayer's world transform
                TraverseAndFlattenQuillLayers(topLevelLayer, topLevelXf, obLayer, ref strokeCount, maxStrokes, loadAnimations, allCollectedStrokes);
                
                if (maxStrokes > 0 && strokeCount >= maxStrokes) break;
            }
            
            if (allCollectedStrokes.Count > 0)
            {
                // Register strokes in memory list first (native loading style)
                foreach (var stroke in allCollectedStrokes)
                {
                    SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                }

                // Optimized batch rendering - all strokes are now on the same obLayer
                SketchMemoryScript.m_Instance.RenderStrokesDirectly(allCollectedStrokes);
                
                // Finalize batches for all created layers
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

        private static void TraverseAndFlattenQuillLayers(SQ.Layer layer, TrTransform worldXf, CanvasScript targetLayer, ref int strokeCount, int maxStrokes, bool loadAnimations, List<Stroke> collectedStrokes)
        {
            if (maxStrokes > 0 && strokeCount >= maxStrokes) return;

            // Note: worldXf is the accumulated transform of this layer in Quill global space.
            // targetLayer.LocalPose is the transform of the top-level layer.
            
            // 1. Process strokes in this layer if it's a Paint layer
            if (layer is SQ.LayerPaint paint)
            {
                // Determine which drawings (frames) to load
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

                        var tbStroke = ConvertQuillStroke(sqStroke, targetLayer, toLayerSpace);
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
                    TraverseAndFlattenQuillLayers(child, childWorldXf, targetLayer, ref strokeCount, maxStrokes, loadAnimations, collectedStrokes);
                    if (maxStrokes > 0 && strokeCount >= maxStrokes) return;
                }
            }
        }

        private static TrTransform ConvertSQTransform(SQ.Transform sqXf)
        {
            Vector3 pos = new Vector3(sqXf.Translation.X, sqXf.Translation.Y, sqXf.Translation.Z);
            Quaternion rot = new Quaternion(sqXf.Rotation.X, sqXf.Rotation.Y, sqXf.Rotation.Z, sqXf.Rotation.W);
            float scale = sqXf.Scale;
            return TrTransform.TRS(pos, rot, scale);
        }

        private static Stroke ConvertQuillStroke(SQ.Stroke sqStroke, CanvasScript targetLayer, TrTransform toLayerSpace)
        {
            if (sqStroke.Vertices.Count < 2) return null;

            // Calculate max width for thresholding
            float maxWidth = 0f;
            foreach (var v in sqStroke.Vertices)
            {
                if (v.Width > maxWidth) maxWidth = v.Width;
            }

            // Determine Brush GUID
            // Default to Wire
            string brushGuid = "ad1ad437-76e2-450d-a23a-e17f8310b960"; 
            
            // Tapering Detection (Threshold is 10% of max width)
            float taperThreshold = maxWidth * 0.1f;
            bool startTapered = sqStroke.Vertices[0].Width < taperThreshold;
            bool endTapered = sqStroke.Vertices.Last().Width < taperThreshold;

            if (sqStroke.BrushType == SQ.BrushType.Cylinder)
            {
                brushGuid = (startTapered || endTapered) 
                    ? "9568870f-8594-60f4-1b20-dfbc8a5eac0e"  // Tapered Wire
                    : "ad1ad437-76e2-450d-a23a-e17f8310b960"; // Wire
            }
            else if (sqStroke.BrushType == SQ.BrushType.Ribbon || sqStroke.BrushType == SQ.BrushType.Ellipse)
            {
                if (startTapered && endTapered) brushGuid = "0d3889f3-3ede-470c-8af4-de4813306126"; // Double Tapered Marker
                else if (startTapered || endTapered) brushGuid = "d90c6ad8-af0f-4b54-b422-e0f92abe1b3c"; // Tapered Marker
                else brushGuid = "429ed64a-4e97-4466-84d3-145a861ef684"; // Marker
            }
            else if (sqStroke.BrushType == SQ.BrushType.Cube)
            {
                brushGuid = "d381e0f5-3def-4a0d-8853-31e9200bcbda"; // Lofted
            }
            
            var brush = BrushCatalog.m_Instance.GetBrush(new Guid(brushGuid));
            if (brush == null)
            {
                 // Fallback to Wire
                 brush = BrushCatalog.m_Instance.GetBrush(new Guid("ad1ad437-76e2-450d-a23a-e17f8310b960"));
            }
            if (brush == null) return null;

            // Calculate average color
            Vector3 avgColorAcc = Vector3.zero;
            foreach (var v in sqStroke.Vertices)
            {
                avgColorAcc += new Vector3(v.Color.R, v.Color.G, v.Color.B);
            }
            Vector3 avgColorVec = avgColorAcc / sqStroke.Vertices.Count;
            Color unityColor = new Color(avgColorVec.x, avgColorVec.y, avgColorVec.z);

            // Create Control Points
            var controlPoints = new List<PointerManager.ControlPoint>(sqStroke.Vertices.Count);
            uint time = 0;

            foreach (var v in sqStroke.Vertices)
            {
                 // Keep positions local to the layer
                 Vector3 localPos = new Vector3(v.Position.X, v.Position.Y, v.Position.Z);
                 Vector3 localForward = new Vector3(v.Tangent.X, v.Tangent.Y, v.Tangent.Z);
                 Vector3 localUp = new Vector3(v.Normal.X, v.Normal.Y, v.Normal.Z);
                 
                 // Apply relative transform to keep coordinates local to the top-level OB layer
                 Vector3 obPos = toLayerSpace * localPos;
                 Vector3 obForward = toLayerSpace.rotation * localForward;
                 Vector3 obUp = toLayerSpace.rotation * localUp;
                 
                 Quaternion orient = Quaternion.identity;
                 if (obForward.sqrMagnitude > 0.001f && obUp.sqrMagnitude > 0.001f)
                 {
                     orient = Quaternion.LookRotation(obForward, obUp);
                 }
                 
                 float pressure = maxWidth > 0 ? v.Width / maxWidth : 1f;

                 controlPoints.Add(new PointerManager.ControlPoint
                 {
                     m_Pos = obPos,
                     m_Orient = orient,
                     m_Pressure = pressure,
                     m_TimestampMs = time++
                 });
            }

            // Create Stroke
             var stroke = new Stroke
                {
                    m_Type = Stroke.Type.NotCreated,
                    m_IntendedCanvas = targetLayer,
                    m_BrushGuid = brush.m_Guid,
                    m_BrushScale = toLayerSpace.scale, 
                    m_BrushSize = maxWidth * toLayerSpace.scale, 
                    m_Color = unityColor,
                    m_Seed = 0,
                    m_ControlPoints = controlPoints.ToArray(),
                };
            
             stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
             stroke.Group = SketchGroupTag.None;
             
             return stroke;
        }
    }
}
