// Copyright 2021 The Open Brush Authors
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
        public static void LoadQuill(string path)
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

            // Recurse layers
            TraverseQuillLayers(sequence.RootLayer);
        }

        private static void TraverseQuillLayers(SQ.Layer layer)
        {
            if (layer is SQ.LayerGroup group)
            {
                foreach (var child in group.Children)
                {
                    TraverseQuillLayers(child);
                }
            }
            else if (layer is SQ.LayerPaint paint)
            {
                foreach (var drawing in paint.Drawings)
                {
                    foreach (var stroke in drawing.Data.Strokes)
                    {
                        ConvertAndDrawQuillStroke(stroke);
                    }
                }
            }
        }

        private static void ConvertAndDrawQuillStroke(SQ.Stroke sqStroke)
        {
            if (sqStroke.Vertices.Count < 2) return;

            // Determine Brush
            // Quill: Cylinder, Ribbon, Ellipse, Cube
            string brushName = "ink"; // Default
            if (sqStroke.BrushType == SQ.BrushType.Ribbon) brushName = "paper";
            
            // Map other types if needed.
            // Cylinder is default in Quill, behaves like Ink/Marker.
            // Ellipse is flat?
            // Cube is thick?

            var brush = LookupBrushDescriptor(brushName);
            if (brush == null)
            {
                // Fallback
                 brush = LookupBrushDescriptor("lightwire");
            }
            if (brush == null) return;

            // Calculate average color and max width
            Vector3 avgColorAcc = Vector3.zero;
            float maxWidth = 0f;
            foreach (var v in sqStroke.Vertices)
            {
                avgColorAcc += new Vector3(v.Color.R, v.Color.G, v.Color.B);
                if (v.Width > maxWidth) maxWidth = v.Width;
            }
            Vector3 avgColorVec = avgColorAcc / sqStroke.Vertices.Count;
            Color unityColor = new Color(avgColorVec.x, avgColorVec.y, avgColorVec.z);

            // Create Control Points
            var controlPoints = new List<PointerManager.ControlPoint>(sqStroke.Vertices.Count);
            uint time = 0; // Fake timestamp

            foreach (var v in sqStroke.Vertices)
            {
                 Vector3 pos = new Vector3(v.Position.X, v.Position.Y, v.Position.Z);
                 // Quill coordinate system might need adjustment.
                 // Trying direct mapping first.
                 
                 Vector3 forward = new Vector3(v.Tangent.X, v.Tangent.Y, v.Tangent.Z);
                 Vector3 up = new Vector3(v.Normal.X, v.Normal.Y, v.Normal.Z);
                 Quaternion orient = Quaternion.identity;
                 
                 if (forward.sqrMagnitude > 0.001f && up.sqrMagnitude > 0.001f)
                 {
                     // Quill Tangent is likely the direction of the stroke.
                     // Open Brush control point orientation: forward is tangent?
                     // Need to verify. PointerManager.ControlPoint m_Orient.
                     // Usually for ribbons, the orientation defines the width direction.
                     
                     // In Open Brush, for a ribbon:
                     // Right vector of orientation is the width direction? Or Up?
                     // "Paper" brush (Ribbon):
                     // "Orientation" is the rotation of the brush controller.
                     // Standard brush orientation: Forward is along the brush handle (Z+). Up is top of brush (Y+).
                     // For ribbon, the flat side is usually XZ plane?
                     
                     // Let's assume LookRotation(forward, up) works where forward is stroke direction.
                     orient = Quaternion.LookRotation(forward, up);
                 }
                 
                 float pressure = maxWidth > 0 ? v.Width / maxWidth : 1f;

                 controlPoints.Add(new PointerManager.ControlPoint
                 {
                     m_Pos = pos,
                     m_Orient = orient,
                     m_Pressure = pressure,
                     m_TimestampMs = time++
                 });
            }

            // Create Stroke
             var stroke = new Stroke
                {
                    m_Type = Stroke.Type.NotCreated,
                    m_IntendedCanvas = App.Scene.ActiveCanvas,
                    m_BrushGuid = brush.m_Guid,
                    m_BrushScale = 1f, 
                    m_BrushSize = maxWidth, 
                    m_Color = unityColor,
                    m_Seed = 0,
                    m_ControlPoints = controlPoints.ToArray(),
                };
            
             stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
             stroke.Group = SketchGroupTag.None;
             
             // Create command
             // TODO: Grouping for Undo?
             // Since this is loading a whole sketch, maybe we don't need undo for individual strokes.
             // But we need to execute it.
             
             // Recreate initializes the game object
             stroke.Recreate(TrTransform.identity, App.Scene.ActiveCanvas);
             SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
             
             // We use PerformAndRecordCommand to ensure it's properly registered in the system (undo/redo, batches etc)
             // But if we have 100k strokes, this will be slow and memory intensive if we create 100k commands.
             
             // Optimized approach: 
             // Just add to memory and BatchManager?
             // BaseBrushScript.CreateBatch(...)
             
             // For safety and correctness using existing API:
             var cmd = new BrushStrokeCommand(stroke, WidgetManager.m_Instance.ActiveStencil, 123, null);
             SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }
    }
}
