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
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        private enum Axis { X, Y, Z }

        private static void _ChangeBrushBearing(float angle, Vector3 axis)
        {
            ApiManager.Instance.BrushRotation *= Quaternion.AngleAxis(angle, axis);
        }

        private static void _ChangeSpectatorBearing(float angle, Vector3 axis)
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.transform.rotation *= Quaternion.AngleAxis(angle, axis);
        }

        private static TrTransform _CurrentTransform()
        {
            var tr = TrTransform.TR(
                ApiManager.Instance.BrushPosition,
                ApiManager.Instance.BrushRotation
            );
            return tr;
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

        private static void _SetWidgetPosition(GrabWidget widget, Vector3 position)
        {
            var tr = widget.LocalTransform;
            tr.translation = position;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(widget, tr, widget.CustomDimension, true)
            );
        }

        private static void _SetWidgetRotation(GrabWidget widget, Vector3 rotation)
        {
            var tr = widget.LocalTransform;
            tr.rotation = Quaternion.Euler(rotation);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(widget, tr, widget.CustomDimension, true)
            );
        }

        private static void _SetWidgetScale(GrabWidget widget, float scale)
        {
            var tr = widget.LocalTransform;
            tr.scale = scale;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(widget, tr, widget.CustomDimension, true)
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

        private static LightWidget _GetActiveLight(int index)
        {
            index = _NegativeIndexing(index, WidgetManager.m_Instance.ActiveLightWidgets);
            return WidgetManager.m_Instance.ActiveLightWidgets[index].WidgetScript;
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

        private static string _DownloadMediaFileFromUrl(string url, string relativeDestinationFolder)
        {
            return _DownloadMediaFileFromUrl(new Uri(url), relativeDestinationFolder);
        }

        private static string _DownloadMediaFileFromUrl(Uri url, string relativeDestinationFolder)
        {
            var request = System.Net.WebRequest.Create(url);
            request.Method = "HEAD";
            var response = request.GetResponse();

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
                filename = url.AbsolutePath.Split('/').Last();
            }

            string AbsoluteDestinationPath = Path.Combine(App.MediaLibraryPath(), relativeDestinationFolder);
            if (!Directory.Exists(AbsoluteDestinationPath))
            {
                Directory.CreateDirectory(AbsoluteDestinationPath);
            }

            // Check if file already exists
            // If it does, append sequential numbers to the filename until we get a unique filename
            string fullDestinationPath = Path.Combine(App.MediaLibraryPath(), relativeDestinationFolder, filename);
            int fileVersion = 0;
            string uniqueFilename = filename;
            while (File.Exists(fullDestinationPath))
            {
                fileVersion++;
                string baseFilename = Path.GetFileNameWithoutExtension(filename);
                uniqueFilename = $"{baseFilename} ({fileVersion}){Path.GetExtension(filename)}";
                fullDestinationPath = Path.Combine(App.MediaLibraryPath(), relativeDestinationFolder, uniqueFilename);
            }

            WebClient wc = new WebClient();
            wc.DownloadFile(url, fullDestinationPath);
            return uniqueFilename;
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
