using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VectorGraphics;
using UnityEngine;

namespace TiltBrush
{
    public static class LuaApiMethods
    {
        public static void DrawPath(IPathApiWrapper path)
        {
            DrawStrokes.DrawNestedTrList(path.AsMultiTrList(), TrTransform.identity);
        }

        public static void DrawPaths(IPathApiWrapper paths)
        {
            DrawStrokes.DrawNestedTrList(paths.AsMultiTrList(), TrTransform.identity);
        }

        public static void TransformPath(PathApiWrapper path, TrTransform tr)
        {
            for (int i = 0; i < path._Path.Count; i++)
            {
                path._Path[i] = tr * path._Path[i];
            }
        }

        public static Quaternion VectorToRotation(Vector3 vec)
        {
            return Quaternion.LookRotation(vec, Vector3.up);
        }

        public static Vector3 RotationToVector(Quaternion rot)
        {
            return rot * Vector3.forward;
        }

        public static void JitterColor()
        {
            App.BrushColor.CurrentColor = PointerManager.m_Instance.GenerateJitteredColor(
                PointerManager.m_Instance.MainPointer.CurrentBrush.m_ColorLuminanceMin
            );
        }

        public static Vector3 QuadBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float m = 1 - t;
            float a = m * m;
            float b = 2 * m * t;
            float c = t * t;
            float x = a * p0.x + b * p1.x + c * p2.x;
            float y = a * p0.y + b * p1.y + c * p2.y;
            float z = a * p0.z + b * p1.z + c * p2.z;
            return new Vector3(x, y, z);
        }

        public static List<Vector3> QuadBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, float res)
        {
            var points = new List<Vector3>();
            points.Add(p0);
            for (var t = res; t < 1; t += res)
            {
                points.Add(QuadBezierPoint(p0, p1, p2, t));
            }
            points.Add(p2);
            return points;
        }

        public static void StraightEdge(bool active)
        {
            PointerManager.m_Instance.StraightEdgeModeEnabled = active;
        }

        public static void AutoOrient(bool active)
        {
            SketchControlsScript.m_Instance.AutoOrientAfterRotation = active;
        }

        public static void ViewOnly(bool active)
        {
            SketchControlsScript.m_Instance.ViewOnly(active);
        }

        public static void AutoSimplify(bool active)
        {
            QualityControls.AutosimplifyEnabled = active;
        }

        public static void Disco(bool active)
        {
            LightsControlScript.m_Instance.DiscoMode = active;
        }

        public static void Profiling(bool active, bool deep = false)
        {
            if (active)
            {
                var mode = deep ? ProfilingManager.Mode.Deep : ProfilingManager.Mode.Standard;
                ProfilingManager.Instance.StartProfiling(mode);
            }
            else
            {
                ProfilingManager.Instance.StopProfiling();
            }
        }

        public static void PostProcessing(bool active)
        {
            CameraConfig.PostEffects = active;
        }

        public static void Watermark(bool active)
        {
            CameraConfig.Watermark = active;
        }

        public static void SaveAs(string name)
        {
            // TODO enforce legal filenames and handle overwriting
            var rEnum = SketchControlsScript.GlobalCommands.SaveAs;
            SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, sParam: name);
        }

        public static void Save(bool overwrite)
        {
            if (overwrite)
            {
                var rEnum = SketchControlsScript.GlobalCommands.SaveNew;
                SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, 1);
            }
            else
            {
                var rEnum = SketchControlsScript.GlobalCommands.Save;
                SketchControlsScript.m_Instance.IssueGlobalCommand(rEnum, -1, -1);
            }
        }
    }
}
