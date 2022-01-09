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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace TiltBrush
{
    public static class DrawStrokes
    {

        public static void SinglePathToStroke(List<List<float>> floatPath, Vector3 origin, float scale = 1f, bool rawStroke = false)
        {
            var floatPaths = new List<List<List<float>>> { floatPath };
            MultiPathsToStrokes(floatPaths, origin, scale, rawStroke);
        }

        public static void SinglePath2dToStroke(List<Vector2> polyline2d, Vector3 origin, float scale = 1f)
        {
            var polylines2d = new List<List<Vector2>> { polyline2d };
            MultiPath2dToStrokes(polylines2d, origin, scale);
        }

        public static void PositionPathsToStroke(List<Vector3> path, Vector3 origin, float scale = 1f)
        {
            var positions = new List<List<Vector3>> { path };
            MultiPositionPathsToStrokes(positions, null, null, origin, scale);
        }

        public static void MultiPathsToStrokes(List<List<List<float>>> strokeData, Vector3 origin, float scale = 1f, bool rawStroke = false)
        {
            var positions = new List<List<Vector3>>();
            var orientations = new List<List<Quaternion>>();
            var pressures = new List<List<float>>();

            // This assumes that the stroke data is consistent.
            // If we have orientation or pressure for the first point, we have it for all
            bool orientationsExist = strokeData[0][0].Count == 6 || strokeData[0][0].Count == 7;
            bool pressuresExist = strokeData[0][0].Count == 6 || strokeData[0][0].Count == 7;

            foreach (List<List<float>> positionList in strokeData)
            {
                var positionsPath = new List<Vector3>();
                var orientationsPath = new List<Quaternion>();
                var pressuresPath = new List<float>();

                foreach (List<float> controlPoint in positionList)
                {
                    if (controlPoint.Count < 3) { controlPoint.Add(0); }  // Handle 2D paths

                    positionsPath.Add(new Vector3(controlPoint[0], controlPoint[1], controlPoint[2]));
                    if (orientationsExist)
                    {
                        orientationsPath.Add(
                            Quaternion.Euler(
                                controlPoint[3],
                                controlPoint[4],
                                controlPoint[5]
                            ));
                    }
                    if (pressuresExist)
                    {
                        pressuresPath.Add(controlPoint.Last());
                    }
                }
                positions.Add(positionsPath);
                if (orientationsExist) orientations.Add(orientationsPath);
                if (pressuresExist) pressures.Add(pressuresPath);
            }
            MultiPositionPathsToStrokes(positions, orientations, pressures, origin, scale, rawStroke);
        }

        public static void MultiPath2dToStrokes(List<List<Vector2>> polylines2d, Vector3 origin, float scale = 1f, bool breakOnOrigin = false)
        {
            var positions = new List<List<Vector3>>();
            foreach (List<Vector2> positionList in polylines2d)
            {
                var path = new List<Vector3>();
                foreach (Vector2 position in positionList)
                {
                    path.Add(new Vector3(position.x, position.y, 0));
                }
                positions.Add(path);
            }
            MultiPositionPathsToStrokes(positions, null, null, origin, scale, breakOnOrigin);
        }

        public static void MultiPositionPathsToStrokes(List<List<Vector3>> positions, List<List<Quaternion>> orientations, List<List<float>> pressures, Vector3 origin, float scale = 1f, bool breakOnOrigin = false, bool rawStrokes = false)
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            uint time = 0;
            float minPressure = PointerManager.m_Instance.MainPointer.CurrentBrush.PressureSizeMin(false);
            float defaultPressure = Mathf.Lerp(minPressure, 1f, 0.5f);
            var group = App.GroupManager.NewUnusedGroup();
            for (var pathIndex = 0; pathIndex < positions.Count; pathIndex++)
            {
                // Single joined paths
                var positionList = positions[pathIndex];
                if (positionList.Count < 2) continue;
                float lineLength = 0;
                var controlPoints = new List<PointerManager.ControlPoint>();
                for (var vertexIndex = 0; vertexIndex < positionList.Count - 1; vertexIndex++)
                {
                    var position = positionList[vertexIndex];
                    Quaternion orientation = orientations?.Any() == true ?
                        orientations[pathIndex][vertexIndex] :
                        Quaternion.identity;
                    float pressure = pressures?.Any() == true ?
                        pressures[pathIndex][vertexIndex] :
                        defaultPressure;
                    var nextPosition = positionList[(vertexIndex + 1) % positionList.Count];
                    // Fix for trailing zeros from SVG.
                    // TODO Find out why and fix it properly
                    if (breakOnOrigin && nextPosition == Vector3.zero)
                    {
                        break;
                    }

                    if (rawStrokes)
                    {
                        controlPoints.Add(new PointerManager.ControlPoint()
                        {
                            m_Pos = position,
                            m_Orient = orientation,
                            m_Pressure = pressure,
                            m_TimestampMs = time++
                        });
                    }
                    else
                    {
                        // Create extra control points if needed
                        // Procedural strokes need to have extra control points added to avoid being smoothed out.
                        for (float step = 0; step <= 1f; step += 0.25f)
                        {
                            controlPoints.Add(new PointerManager.ControlPoint
                            {
                                m_Pos = (position + (nextPosition - position) * step) * scale + origin,
                                m_Orient = orientation,
                                m_Pressure = pressure,
                                m_TimestampMs = time++
                            });
                        }
                    }

                    lineLength += (nextPosition - position).magnitude; // TODO Does this need scaling? Should be in Canvas space
                }
                var stroke = new Stroke
                {
                    m_Type = Stroke.Type.NotCreated,
                    m_IntendedCanvas = App.Scene.ActiveCanvas,
                    m_BrushGuid = brush.m_Guid,
                    m_BrushScale = 1f,
                    m_BrushSize = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute,
                    m_Color = App.BrushColor.CurrentColor,
                    m_Seed = 0,
                    m_ControlPoints = controlPoints.ToArray(),
                };
                stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
                stroke.Group = @group;
                stroke.Recreate(null, App.Scene.ActiveCanvas);
                if (pathIndex != 0) stroke.m_Flags = SketchMemoryScript.StrokeFlags.IsGroupContinue;
                SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new BrushStrokeCommand(stroke, WidgetManager.m_Instance.ActiveStencil, 123) // TODO calc length
                );
            }
        }
    }
}
