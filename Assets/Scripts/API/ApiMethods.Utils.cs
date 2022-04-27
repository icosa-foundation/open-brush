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
using System.IO;
using System.Linq;
using System.Net;
using Polyhydra.Core;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        private enum Axis { X, Y, Z }
        
        private static Quaternion _LookAt(Vector3 sourcePoint, Vector3 destPoint)
        {
            Vector3 forwardVector = Vector3.Normalize(destPoint - sourcePoint);
            float dot = Vector3.Dot(Vector3.forward, forwardVector);
            if (Math.Abs(dot - (-1.0f)) < 0.000001f)
            {
                return new Quaternion(Vector3.up.x, Vector3.up.y, Vector3.up.z, 3.1415926535897932f);
            }
            if (Math.Abs(dot - (1.0f)) < 0.000001f)
            {
                return Quaternion.identity;
            }

            float rotAngle = (float)Math.Acos(dot);
            Vector3 rotAxis = Vector3.Cross(Vector3.forward, forwardVector);
            rotAxis = Vector3.Normalize(rotAxis);
            return _CreateFromAxisAngle(rotAxis, rotAngle);
        }
        
        private static Quaternion _CreateFromAxisAngle(Vector3 axis, float angle)
        {
            float halfAngle = angle * .5f;
            float s = Mathf.Sin(halfAngle);
            Quaternion q;
            q.x = axis.x * s;
            q.y = axis.y * s;
            q.z = axis.z * s;
            q.w = Mathf.Cos(halfAngle);
            return q;
        }
        
        private static void _ChangeBrushBearing(float angle, Vector3 axis)
        {
            Debug.Log($"angle: {axis} angle: {axis}");
            Debug.Log($"ApiManager.Instance.BrushRotation: {ApiManager.Instance.BrushRotation}");
            ApiManager.Instance.BrushRotation *= Quaternion.AngleAxis(angle, axis);
            Debug.Log($"ApiManager.Instance.BrushRotation: {ApiManager.Instance.BrushRotation}");
        }
        
        private static void _ChangeSpectatorBearing(float angle, Vector3 axis)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.transform.rotation *= Quaternion.AngleAxis(angle, axis);
        }
        
        private static Vector3 _RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rot)
        {
            Vector3 dir = point - pivot;
            dir = rot * dir;
            point = dir + pivot;
            return point;
        }

        private static TrTransform _CurrentTransform()
        {
            var tr = TrTransform.TR(
                ApiManager.Instance.BrushPosition,
                ApiManager.Instance.BrushRotation
            );
            return tr;
        }

        private static void _PolyFromPath(List<Vector3> path, TrTransform tr, Color color)
        {
            var face = new List<IEnumerable<int>> { Enumerable.Range(0, path.Count) };
            var poly = new PolyMesh(path, face);
            poly.InitTags(color);
            EditableModelManager.GeneratePolyMesh(poly, tr, ColorMethods.ByTags, GeneratorTypes.GeometryData);
        }

        private static void _ApplyOp(EditableModelWidget widget, PolyMesh.Operation op, float param1 = float.NaN, float param2 = float.NaN)
        {
            var id = widget.GetId();
            var poly = EditableModelManager.m_Instance.GetPolyMesh(id);
            OpParams p;
            if (float.IsNaN(param1) && float.IsNaN(param2))
            {
                p = new OpParams();
            }
            else if (float.IsNaN(param2))
            {
                p = new OpParams(param1);
            }
            else
            {
                p = new OpParams(param1, param2);
            }
            poly = poly.AppyOperation(op, p);
            EditableModelManager.m_Instance.RegenerateMesh(widget, poly);
        }
        
        private static EditableModelWidget _GetModelIdByIndex(int index)
        {
            EditableModelWidget widget = GetActiveEditableModel(index);
            return widget;
        }
        
        private static Vector3 _QuantizePosition(Vector3 pos, Vector3 grid)
        {
            float round(float val, float res) { return Mathf.Round(val / res) * res; }
            return new Vector3(round(pos.x, grid.x), round(pos.y, grid.y), round(pos.z, grid.z));
        }
        
        private static void _ModifyStrokeControlPoints(Func<Vector3, Vector3> func)
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                var newCPs = new List<PointerManager.ControlPoint>();
                for (var i = 0; i < stroke.m_ControlPoints.Length; i++)
                {
                    var cp = stroke.m_ControlPoints[i];
                    cp.m_Pos = func(cp.m_Pos);
                    // Only add a control point if it's pos is different to it's predecessor
                    if (i == 0 || (i > 0 && cp.m_Pos != stroke.m_ControlPoints[i - 1].m_Pos))
                    {
                        newCPs.Add(cp);
                    }
                }
                stroke.m_ControlPoints = newCPs.ToArray();
                stroke.Uncreate();
                stroke.Recreate(null, stroke.Canvas);
            }
        }
        
        private static Vector3 _PerlinNoiseToPosition(Vector3 pos, Vector3 scale, Axis axis)
        {
            pos = new Vector3(pos.x / scale.x, pos.y / scale.y, pos.z / scale.z);
            switch (axis)
            {
                case Axis.X:
                    pos.x += Mathf.PerlinNoise(pos.y, pos.z) * scale.x;
                    break;
                case Axis.Y:
                    pos.y += Mathf.PerlinNoise(pos.x, pos.z) * scale.y;
                    break;
                case Axis.Z:
                    pos.z += Mathf.PerlinNoise(pos.x, pos.y) * scale.z;
                    break;
            }
            return new Vector3(pos.x * scale.x, pos.y * scale.y, pos.z * scale.z);
        }
        
        private static void _PositionWidget(GrabWidget widget, Vector3 position)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(
                    widget,
                    TrTransform.T(position),
                    widget.CustomDimension,
                    true
                )
            );
        }
        
        private static void _ScaleWidget(GrabWidget widget, float scale)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(
                    widget,
                    TrTransform.S(scale),
                    widget.CustomDimension,
                    true
                )
            );
        }
        
        private static int _NegativeIndexing<T>(int index, IEnumerable<T> enumerable)
        {
            // Python style: negative numbers count from the end
            int count = enumerable.Count();
            if (index < 0) index = count - Mathf.Abs(index);
            return index;
        }
        
        private static ImageWidget _GetActiveImage(int index)
        {
            index = _NegativeIndexing(index, WidgetManager.m_Instance.ActiveImageWidgets);
            return WidgetManager.m_Instance.ActiveImageWidgets[index].WidgetScript;
        }
        
        private static VideoWidget _GetActiveVideo(int index)
        {
            index = _NegativeIndexing(index, WidgetManager.m_Instance.ActiveVideoWidgets);
            return WidgetManager.m_Instance.ActiveVideoWidgets[index].WidgetScript;
        }
        
        private static ModelWidget _GetActiveModel(int index)
        {
            index = _NegativeIndexing(index, WidgetManager.m_Instance.ActiveModelWidgets);
            return WidgetManager.m_Instance.ActiveModelWidgets[index].WidgetScript;
        }
        
        private static CameraPathWidget _GetActiveCameraPath(int index)
        {
            index = _NegativeIndexing(index, WidgetManager.m_Instance.ActiveCameraPathWidgets);
            return WidgetManager.m_Instance.GetNthActiveCameraPath(index);
        }
        
        private static string _DownloadMediaFileFromUrl(string url, string destinationFolder)
        {
            var request = System.Net.WebRequest.Create(url);
            request.Method = "HEAD";
            var response = request.GetResponse();
            Uri uri = new Uri(url);

            string filename;
            var contentDisposition = response.Headers["Content-Disposition"];
            if (!String.IsNullOrEmpty(contentDisposition))
            {
                int idx = contentDisposition.IndexOf("filename=") + 10;
                filename = contentDisposition.Substring(idx);
                filename.Replace("\"", "");
            }
            else
            {
                filename = uri.AbsolutePath.Split('/').Last();
            }

            var path = Path.Combine(App.MediaLibraryPath(), destinationFolder, filename);
            WebClient wc = new WebClient();
            wc.DownloadFile(uri, path);
            return filename;
        }
        
        private static void _SpectatorShowHide(string thing, bool state)
        {
            // Friendly names to layer names
            string layerName = null;
            switch (thing.Trim().ToLower())
            {
                case "widgets":
                    layerName = "GrabWidgets";
                    break;
                case "strokes":
                    layerName = "MainCanvas";
                    break;
                case "selection":
                    layerName = "SelectionCanvas";
                    break;
                case "headset":
                    layerName = "HeadMesh";
                    break;
                case "panels":
                    layerName = "Panels";
                    break;
                case "ui":
                    layerName = "UI";
                    break;
                case "usertools":
                    layerName = "UserTools";
                    break;
            }

            if (layerName == null) return;

            int mask = 1 << LayerMask.NameToLayer(layerName);
            Camera cam = SketchControlsScript.m_Instance.GetDropCampWidget().GetComponentInChildren<Camera>();
            if (state)
            {
                cam.cullingMask |= mask;
            }
            else
            {
                cam.cullingMask = ~(~cam.cullingMask | mask);
            }
        }

    }
}
