// Copyright 2022 The Open Brush Authors
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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("stroke.delete", "Delete a stroke by index")]
        public static void DeleteStroke(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
            stroke.Uncreate();
        }

        [ApiEndpoint("stroke.select", "Select a stroke by index.")]
        public static void SelectStroke(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            SelectionManager.m_Instance.SelectStrokes(new List<Stroke> { stroke });
        }

        [ApiEndpoint("strokes.select", "Select multiple strokes by index.")]
        public static void SelectStrokes(int start, int end)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            SelectionManager.m_Instance.SelectStrokes(strokes);
        }

        [ApiEndpoint("selection.recolor", "Recolors the currently selected strokes")]
        public static void RecolorSelection()
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(stroke, true, false, false);
            }
        }

        [ApiEndpoint("strokes.move.to", "Moves several strokes to the given position")]
        public static void TranslateStrokesTo(int start, int end, Vector3 position)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            foreach (var stroke in strokes)
            {
                stroke.RecreateAt(TrTransform.T(position));
            }
        }

        [ApiEndpoint("strokes.move.by", "Moves several strokes to the given coordinates")]
        public static void TranslateStrokesBy(int start, int end, Vector3 translation)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            TransformItemsCommand cmd = new TransformItemsCommand(strokes, null, TrTransform.T(translation), Vector3.zero);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint("strokes.rotate.by", "Rotates multiple brushstrokes around the current brush position")]
        public static void RotateStrokesBy(int start, int end, float angle)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            var axis = ApiManager.Instance.BrushRotation * Vector3.forward;
            var rot = TrTransform.R(angle, axis);
            var pivot = ApiManager.Instance.BrushPosition;
            TransformItemsCommand cmd = new TransformItemsCommand(strokes, null, rot, pivot);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint("strokes.scale.by", "Scales multiple brushstrokes around the current brush position")]
        public static void ScaleStrokesBy(int start, int end, float scale)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            var pivot = ApiManager.Instance.BrushPosition;
            TransformItemsCommand cmd = new TransformItemsCommand(strokes, null, TrTransform.S(scale), pivot);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint("selection.rebrush", "Rebrushes the currently selected strokes")]
        public static void RebrushSelection(bool jitter = false)
        {
            SketchMemoryScript.m_Instance.RepaintSelected(true, false, false, jitter);
        }

        [ApiEndpoint("selection.recolor", "Recolors the currently selected strokes")]
        public static void RecolorSelection(bool jitter = false)
        {
            SketchMemoryScript.m_Instance.RepaintSelected(false, true, false, jitter);
        }

        [ApiEndpoint("selection.resize", "Changes the brush size the currently selected strokes")]
        public static void ResizeSelection(bool jitter = false)
        {
            SketchMemoryScript.m_Instance.RepaintSelected(false, false, true, jitter);
        }

        [ApiEndpoint("selection.trim", "Removes a number of points from the currently selected strokes")]
        public static void TrimSelection(int count)
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                int newCount = Mathf.Max(0, stroke.m_ControlPoints.Length - count);
                if (newCount > 0)
                {
                    Array.Resize(ref stroke.m_ControlPoints, newCount);
                    stroke.Recreate(null, stroke.Canvas);
                    SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(stroke, false, false, true);
                }
                else
                {
                    SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                    stroke.Uncreate();
                }
            }
        }

        [ApiEndpoint("selection.points.addnoise", "Moves the position of all control points in the selection using a noise function")]
        public static void PerlinNoiseSelection(string axis, Vector3 scale)
        {
            Enum.TryParse(axis.ToUpper(), out Axis _axis);
            Func<Vector3, Vector3> quantize = pos => _PerlinNoiseToPosition(pos, scale, _axis);
            _ModifyStrokeControlPoints(quantize);
        }

        [ApiEndpoint("stroke.points.quantize", "Snaps all the points in selected strokes to a grid (buggy)")]
        public static void QuantizeSelection(Vector3 grid)
        {
            Func<Vector3, Vector3> quantize = pos => _QuantizePosition(pos, grid);
            _ModifyStrokeControlPoints(quantize);
        }

        [ApiEndpoint("stroke.join", "Joins a stroke with the previous one")]
        public static void JoinStroke()
        {
            var stroke1 = SketchMemoryScript.m_Instance.GetStrokeAtIndex(0);
            var stroke2 = SketchMemoryScript.m_Instance.GetStrokeAtIndex(-1);
            stroke2.m_ControlPoints = stroke2.m_ControlPoints.Concat(stroke1.m_ControlPoints).ToArray();
            stroke2.Uncreate();
            stroke2.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke2.m_ControlPoints.Length).ToArray();
            stroke2.Recreate(null, stroke2.Canvas);
            DeleteStroke(0);
        }

        [ApiEndpoint("strokes.join", "Joins all strokes between the two indices (inclusive)")]
        public static void JoinStrokes(int start, int end)
        {
            var strokesToJoin = SketchMemoryScript.GetStrokesBetween(start, end);
            var firstStroke = strokesToJoin[0];
            firstStroke.m_ControlPoints = strokesToJoin.SelectMany(x => x.m_ControlPoints).ToArray();
            for (int i = 1; i < strokesToJoin.Count; i++)
            {
                var stroke = strokesToJoin[i];
                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.DestroyStroke();
            }
            firstStroke.Uncreate();
            firstStroke.m_ControlPointsToDrop = Enumerable.Repeat(false, firstStroke.m_ControlPoints.Length).ToArray();
            firstStroke.Recreate(null, firstStroke.Canvas);
        }

        [ApiEndpoint("stroke.add", "Adds a point at the current brush position to the specified stroke")]
        public static void AddPointToStroke(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            var prevCP = stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1];
            Array.Resize(ref stroke.m_ControlPoints, stroke.m_ControlPoints.Length + 1);

            PointerManager.ControlPoint cp = new PointerManager.ControlPoint
            {
                m_Pos = ApiManager.Instance.BrushPosition,
                m_Orient = ApiManager.Instance.BrushRotation,
                m_Pressure = prevCP.m_Pressure,
                m_TimestampMs = prevCP.m_TimestampMs
            };

            stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1] = cp;
            stroke.Uncreate();
            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Recreate(null, stroke.Canvas);
        }

    }
}
