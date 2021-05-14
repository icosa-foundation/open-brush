using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace TiltBrush
{
    public static class DrawStrokes
    {
        public static void PathsToStrokes(List<List<List<float>>> floatPaths, Vector3 origin, float scale = 1f)
        {
            var paths = new List<List<Vector3>>();
            foreach (List<List<float>> positionList in floatPaths)
            {
                var path = new List<Vector3>();
                foreach (List<float> position in positionList)
                {
                    if (position.Count < 3) {position.Add(0);}
                    path.Add(new Vector3(position[0], position[1], position[2]));
                }
                paths.Add(path);
            }
            PathsToStrokes(paths, origin, scale);
        }
        public static void PathsToStrokes(List<List<Vector2>> polyline2d, Vector3 origin, float scale = 1f, bool breakOnOrigin = false)
        {
            var paths = new List<List<Vector3>>();
            foreach (List<Vector2> positionList in polyline2d)
            {
                var path = new List<Vector3>();
                foreach (Vector2 position in positionList)
                {
                    path.Add(new Vector3(position.x, position.y, 0));
                }
                paths.Add(path);
            }
            PathsToStrokes(paths, origin, scale, breakOnOrigin);
        }
        public static void PathsToStrokes(List<List<Vector3>> paths, Vector3 origin, float scale = 1f, bool breakOnOrigin = false)
        {
            Vector3 pos = origin;
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            uint time = 0;
            float minPressure = PointerManager.m_Instance.MainPointer.CurrentBrush.PressureSizeMin(false);
            float pressure = Mathf.Lerp(minPressure, 1f, 0.5f);
            var group = App.GroupManager.NewUnusedGroup();
            for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                var path = paths[pathIndex];
                if (path.Count < 2) continue;
                float lineLength = 0;
                var controlPoints = new List<PointerManager.ControlPoint>();
                for (var vertexIndex = 0; vertexIndex < path.Count - 1; vertexIndex++)
                {
                    var coordList0 = path[vertexIndex];
                    var vert = new Vector3(coordList0[0], coordList0[1], coordList0[2]) * scale;
                    var coordList1 = path[(vertexIndex + 1) % path.Count];
                    // Fix for trailing zeros from SVG.
                    // TODO Find out why and fix it properly
                    if (breakOnOrigin && coordList1 == Vector3.zero)
                    {
                        break;
                    }
                    var nextVert = new Vector3(coordList1[0], coordList1[1], coordList1[2]) * scale;

                    for (float step = 0; step <= 1f; step += .25f)
                    {
                        controlPoints.Add(new PointerManager.ControlPoint
                        {
                            m_Pos = pos + vert + ((nextVert - vert) * step),
                            m_Orient = Quaternion.identity, //.LookRotation(face.Normal, Vector3.up),
                            m_Pressure = pressure,
                            m_TimestampMs = time++
                        });
                    }

                    lineLength += (nextVert - vert).magnitude; // TODO Does this need scaling? Should be in Canvas space
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
