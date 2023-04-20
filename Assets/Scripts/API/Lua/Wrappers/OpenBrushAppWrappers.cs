// Copyright 2023 The Open Brush Authors
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
using System.IO;
using System.Linq;
using MoonSharp.Interpreter;
using ODS;
using UnityAsyncAwaitUtil;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public static class BrushApiWrapper
    {
        public static float timeSincePressed => Time.realtimeSinceStartup - SketchSurfacePanel.m_Instance.ActiveTool.TimeBecameActive;
        public static float timeSinceReleased => Time.realtimeSinceStartup - SketchSurfacePanel.m_Instance.ActiveTool.TimeBecameInactive;
        public static bool triggerIsPressed => SketchSurfacePanel.m_Instance.ActiveTool.IsActive;
        public static bool triggerIsPressedThisFrame => SketchSurfacePanel.m_Instance.ActiveTool.IsActiveThisFrame;
        public static float distanceMoved => SketchSurfacePanel.m_Instance.ActiveTool.DistanceMoved_CS;
        public static float distanceDrawn => SketchSurfacePanel.m_Instance.ActiveTool.DistanceDrawn_CS;
        public static Vector3 position => LuaManager.Instance.GetPastBrushPos(0);
        public static Quaternion rotation => LuaManager.Instance.GetPastBrushRot(0);
        public static Vector3 direction => LuaManager.Instance.GetPastBrushRot(0) * Vector3.forward;
        public static float size
        {
            get => PointerManager.m_Instance.MainPointer.BrushSize01;
            set => ApiMethods.BrushSizeSet(value);
        }
        public static float pressure => PointerManager.m_Instance.MainPointer.GetPressure();
        public static string type
        {
            get => PointerManager.m_Instance.MainPointer.CurrentBrush.m_Description!;
            set => ApiMethods.Brush(value);
        }
        public static float speed => PointerManager.m_Instance.MainPointer.MovementSpeed;
        public static Color colorRgb
        {
            get => PointerManager.m_Instance.PointerColor;
            set => App.BrushColor.CurrentColor = value;
        }

        public static Vector3 colorHsv
        {
            get
            {
                Color.RGBToHSV(App.BrushColor.CurrentColor, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
            set => App.BrushColor.CurrentColor = Color.HSVToRGB(value.x, value.y, value.z);
        }

        public static string colorHtml
        {
            get => ColorUtility.ToHtmlStringRGB(PointerManager.m_Instance.PointerColor);
            set => ApiMethods.SetColorHTML(value);
        }
        public static void colorJitter() => LuaApiMethods.JitterColor();
        public static Color lastColorPicked => PointerManager.m_Instance.m_lastChosenColor;
        public static void resizeBuffer(int size) => LuaManager.Instance.ResizeBrushBuffer(size);
        public static void setBufferSize(int size) => LuaManager.Instance.SetBrushBufferSize(size);
        public static Vector3 pastPosition(int back) => LuaManager.Instance.GetPastBrushPos(back);
        public static Quaternion pastRotation(int back) => LuaManager.Instance.GetPastBrushRot(back);
        public static void forcePaintingOn(bool active) => ApiMethods.ForcePaintingOn(active);
        public static void forcePaintingOff(bool active) => ApiMethods.ForcePaintingOff(active);
        public static void forceNewStroke() => ApiMethods.ForceNewStroke();

        public static Vector3 lastColorPickedHsv
        {
            get
            {
                Color.RGBToHSV(lastColorPicked, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
        }
    }

    [MoonSharpUserData]
    public static class WandApiWrapper
    {
        public static Vector3 position => LuaManager.Instance.GetPastWandPos(0);
        public static Quaternion rotation => LuaManager.Instance.GetPastWandRot(0);
        public static Vector3 direction => LuaManager.Instance.GetPastWandRot(0) * Vector3.forward;
        public static float pressure => InputManager.Wand.GetTriggerValue();
        public static Vector3 speed => InputManager.Wand.m_Velocity;
        public static void resizeBuffer(int size) => LuaManager.Instance.ResizeWandBuffer(size);
        public static void setBufferSize(int size) => LuaManager.Instance.SetWandBufferSize(size);
        public static Vector3 pastPosition(int back) => LuaManager.Instance.GetPastWandPos(back);
        public static Quaternion pastRotation(int back) => LuaManager.Instance.GetPastWandRot(back);
    }

    [MoonSharpUserData]
    public static class AppApiWrapper
    {
        public static float time => Time.realtimeSinceStartup;
        public static float frames => Time.frameCount;
        public static bool physics(bool active) => Physics.autoSimulation = active;
        public static float currentScale => App.Scene.Pose.scale;
        public static List<TrTransform> lastSelectedStroke => SelectionManager.m_Instance.LastSelectedStrokeCP;
        public static List<TrTransform> lastStroke => SelectionManager.m_Instance.LastStrokeCP;
        public static void undo() => ApiMethods.Undo();
        public static void redo() => ApiMethods.Redo();
        public static void addListener(string a) => ApiMethods.AddListener(a);
        public static void resetPanels() => ApiMethods.ResetAllPanels();
        public static void showScriptsFolder() => ApiMethods.OpenUserScriptsFolder();
        public static void showExportFolder() => ApiMethods.OpenExportFolder();
        public static void showSketchesFolder(int a) => ApiMethods.ShowSketchFolder(a);
        public static void straightEdge(bool a) => LuaApiMethods.StraightEdge(a);
        public static void autoOrient(bool a) => LuaApiMethods.AutoOrient(a);
        public static void viewOnly(bool a) => LuaApiMethods.ViewOnly(a);
        public static void autoSimplify(bool a) => LuaApiMethods.AutoSimplify(a);
        public static void disco(bool a) => LuaApiMethods.Disco(a);
        public static void profiling(bool a) => LuaApiMethods.Profiling(a);
        public static void postProcessing(bool a) => LuaApiMethods.PostProcessing(a);
        public static void draftingVisible() => ApiMethods.DraftingVisible();
        public static void draftingTransparent() => ApiMethods.DraftingTransparent();
        public static void draftingHidden() => ApiMethods.DraftingHidden();
        public static string environment
        {
            get => SceneSettings.m_Instance.CurrentEnvironment.m_Description;
            set => ApiMethods.SetEnvironment(value);
        }
        public static void watermark(bool a) => LuaApiMethods.Watermark(a);
        // TODO Unified API for tools and panels
        // public static void SettingsPanel(bool a) => )LuaApiMethods.SettingsPanel)(a);
        // public static void SketchOrigin(bool a) => )LuaApiMethods.SketchOrigin)(a);

        public static string clipboardText {
            get => SystemClipboard.GetClipboardText();
            set => SystemClipboard.SetClipboardText(value);
        }
        public static Texture2D clipboardImage {
            get => SystemClipboard.GetClipboardImage();
            // set => SystemClipboard.SetClipboardImage(value);
        }

        public static string readFile(string path)
        {
            bool valid = false;
            // Disallow absolute paths
            valid = !Path.IsPathRooted(path);
            if (valid)
            {
                path = Path.Join(ApiManager.Instance.UserScriptsPath(), path);
                // Check path is a subdirectory of User folder
                valid = _IsSubdirectory(path, App.UserPath());
            }
            if (!valid)
            {
                // TODO think long and hard about security
                Debug.LogWarning($"Path is not a subdirectory of User folder: {path}");
                return null;
            }

            Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            string contents;
            using (var sr = new StreamReader(fileStream)) contents = sr.ReadToEnd();
            fileStream.Close();

            return contents;
        }

        public static void setFont(string fontData) => ApiManager.Instance.SetTextFont(fontData);

        public static void takeSnapshot(TrTransform tr, string filename, int width, int height, float superSampling = 1f)
        {
            bool saveAsPng;
            if (filename.ToLower().EndsWith(".jpg") || filename.ToLower().EndsWith(".jpeg"))
            {
                saveAsPng = false;
            }
            else if (filename.ToLower().EndsWith(".png"))
            {
                saveAsPng = true;
            }
            else
            {
                saveAsPng = false;
                filename += ".jpg";
            }
            string path = Path.Join(App.SnapshotPath(), filename);
            MultiCamTool cam = SketchSurfacePanel.m_Instance.GetToolOfType(BaseTool.ToolType.MultiCamTool) as MultiCamTool;

            if (cam != null)
            {
                var rig = SketchControlsScript.m_Instance.MultiCamCaptureRig;
                App.Scene.AsScene[rig.gameObject.transform] = tr;
                var rMgr = rig.ManagerFromStyle(
                    MultiCamStyle.Snapshot
                );
                var initialState = rig.gameObject.activeSelf;
                rig.gameObject.SetActive(true);
                RenderTexture tmp = rMgr.CreateTemporaryTargetForSave(width, height);
                RenderWrapper wrapper = rMgr.gameObject.GetComponent<RenderWrapper>();
                float ssaaRestore = wrapper.SuperSampling;
                wrapper.SuperSampling = superSampling;
                rMgr.RenderToTexture(tmp);
                wrapper.SuperSampling = ssaaRestore;
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    ScreenshotManager.Save(fs, tmp, bSaveAsPng: saveAsPng);
                }
                rig.gameObject.SetActive(initialState);
            }
        }

        public static void take360Snapshot(TrTransform tr, string filename, int width = 4096)
        {
            var odsDriver = App.Instance.InitOds();
            App.Scene.AsScene[odsDriver.gameObject.transform] = tr;
            odsDriver.FramesToCapture = 1;
            odsDriver.OdsCamera.basename = filename;
            odsDriver.OdsCamera.outputFolder = App.SnapshotPath();
            odsDriver.OdsCamera.imageWidth = width;
            odsDriver.OdsCamera.outputFolder = App.SnapshotPath();
            odsDriver.OdsCamera.SetOdsRendererType(HybridCamera.OdsRendererType.Slice);
            odsDriver.OdsCamera.gameObject.SetActive(true);
            odsDriver.OdsCamera.enabled = true;
            AsyncCoroutineRunner.Instance.StartCoroutine(odsDriver.OdsCamera.Render(odsDriver.transform));
        }

        private static bool _IsSubdirectory(string path, string basePath)
        {
            var relPath = Path.GetRelativePath(
                basePath.Replace('\\', '/'),
                path.Replace('\\', '/')
            );
            return relPath != "." && relPath != ".."
                && !relPath.StartsWith("../")
                && !Path.IsPathRooted(relPath);
        }
    }


    [MoonSharpUserData]
    public static class SpectatorApiWrapper
    {
        public static void turn(float angle) => ApiMethods.SpectatorYaw(angle);
        public static void turnX(float angle) => ApiMethods.SpectatorPitch(angle);
        public static void turnZ(float angle) => ApiMethods.SpectatorRoll(angle);
        public static void direction(Vector3 direction) => ApiMethods.SpectatorDirection(direction);
        public static void lookAt(Vector3 position) => ApiMethods.SpectatorLookAt(position);
        public static void mode(string mode) => ApiMethods.SpectatorMode(mode);
        public static void show(string type) => ApiMethods.SpectatorShow(type);
        public static void hide(string type) => ApiMethods.SpectatorHide(type);
        public static void toggle() => ApiMethods.ToggleSpectator();
        public static void on() => ApiMethods.EnableSpectator();
        public static void off() => ApiMethods.DisableSpectator();
        public static Vector3 position
        {
            get => SketchControlsScript.m_Instance.GetDropCampWidget().transform.position;
            set => SketchControlsScript.m_Instance.GetDropCampWidget().transform.position = value;
        }
        public static Quaternion rotation
        {
            get => SketchControlsScript.m_Instance.GetDropCampWidget().transform.rotation;
            set => SketchControlsScript.m_Instance.GetDropCampWidget().transform.rotation = value;
        }

    }

    [MoonSharpUserData]
    public static class VisualizerApiWrapper
    {
        public static float sampleRate => LuaManager.Instance.ScriptedWaveformSampleRate;
        public static float duration => (1f / sampleRate) * VisualizerManager.m_Instance.FFTSize;
        public static void enableScripting(string name) => AudioCaptureManager.m_Instance.EnableScripting();
        public static void disableScripting() => AudioCaptureManager.m_Instance.DisableScripting();
        public static void setWaveform(float[] data) => VisualizerManager.m_Instance.ProcessAudio(data, LuaManager.Instance.ScriptedWaveformSampleRate);
        public static void setFft(float[] data1, float[] data2, float[] data3, float[] data4) => VisualizerManager.m_Instance.InjectScriptedFft(data1, data2, data3, data4);
        public static void setBeats(float x, float y, float z, float w) => VisualizerManager.m_Instance.InjectScriptedBeats(new Vector4(x, y, z, w));
        public static void setBeatAccumulators(float x, float y, float z, float w) => VisualizerManager.m_Instance.InjectScriptedBeatAccumulator(new Vector4(x, y, z, w));
        public static void setBandPeak(float peak) => VisualizerManager.m_Instance.InjectBandPeaks(new Vector4(0, peak, 0, 0));
    }


    [MoonSharpUserData]
    public static class SymmetryApiWrapper
    {
        public static TrTransform transform
        {
            get => TrTransform.FromTransform(PointerManager.m_Instance.SymmetryWidget.transform);
            set => ApiMethods.SymmetrySetTransform(value.translation, value.rotation);
        }
        public static Vector3 position
        {
            get => PointerManager.m_Instance.SymmetryWidget.transform.position;
            set => ApiMethods.SymmetrySetPosition(value);
        }
        public static Quaternion rotation
        {
            get => PointerManager.m_Instance.SymmetryWidget.transform.rotation;
            set => ApiMethods.SymmetrySetRotation(value);
        }

        public static Vector3 brushOffset => (App.Scene.MainCanvas.AsCanvas[PointerManager.m_Instance.SymmetryWidget.transform].inverse * TrTransform.T(LuaManager.Instance.GetPastBrushPos(0))).translation;
        public static Vector3 wandOffset => (App.Scene.MainCanvas.AsCanvas[PointerManager.m_Instance.SymmetryWidget.transform].inverse * TrTransform.T(LuaManager.Instance.GetPastWandPos(0))).translation;
        public static Vector3 direction => PointerManager.m_Instance.SymmetryWidget.transform.rotation * Vector3.forward;
        public static void mirror() => ApiMethods.SymmetryPlane();
        public static void doubleMirror() => ApiMethods.SymmetryFour();
        public static void twoHandeded() => ApiMethods.SymmetryTwoHanded();
        public static void summonWidget() => ApiMethods.SummonMirror();
        public static void spin(Vector3 rot) => PointerManager.m_Instance.SymmetryWidget.Spin(rot);
        public static float ellipse(float angle, float minorRadius)
        {
            return minorRadius / Mathf.Sqrt(Mathf.Pow(minorRadius * Mathf.Cos(angle), 2) + Mathf.Pow(Mathf.Sin(angle), 2));
        }
        public static float square(float angle)
        {
            const float halfEdgeLength = 0.5f;
            float x = Mathf.Abs(Mathf.Cos(angle));
            float y = Mathf.Abs(Mathf.Sin(angle));
            float maxComponent = Mathf.Max(x, y);
            return halfEdgeLength / maxComponent;
        }
        public static float superellipse(float angle, float n, float a = 1f, float b = 1f)
        {
            float cosTheta = Mathf.Cos(angle);
            float sinTheta = Mathf.Sin(angle);
            float cosThetaN = Mathf.Pow(Mathf.Abs(cosTheta), n);
            float sinThetaN = Mathf.Pow(Mathf.Abs(sinTheta), n);
            float radius = Mathf.Pow(Mathf.Pow(a, n) * cosThetaN + Mathf.Pow(b, n) * sinThetaN, -1 / n);
            return radius;
        }
        public static float rsquare(float angle, float halfSideLength, float cornerRadius)
        {
            float x = Mathf.Abs(Mathf.Cos(angle));
            float y = Mathf.Abs(Mathf.Sin(angle));

            // Check if the point lies in the rounded corner area
            if (x > halfSideLength - cornerRadius && y > halfSideLength - cornerRadius)
            {
                // Calculate the distance to the rounded corner center
                float dx = x - (halfSideLength - cornerRadius);
                float dy = y - (halfSideLength - cornerRadius);
                float distanceToCornerCenter = Mathf.Sqrt(dx * dx + dy * dy);

                // Calculate the distance to the rounded corner edge
                return halfSideLength + cornerRadius - distanceToCornerCenter;
            }
            // Calculate the distance to the square edge as before
            float maxComponent = Mathf.Max(x, y);
            return halfSideLength / maxComponent;
        }
        public static float polygon(float angle, int numSides, float radius=1f)
        {
            // Calculate the angle of each sector in the polygon
            float sectorAngle = 2 * Mathf.PI / numSides;

            // Find the nearest vertex by rounding the angle to the nearest sector angle
            float nearestVertexAngle = Mathf.Round(angle / sectorAngle) * sectorAngle;

            // Calculate the bisector angle (half of the sector angle)
            float bisectorAngle = sectorAngle / 2;

            // Calculate the distance from the center to the midpoint of the edge
            float apothem = radius * Mathf.Cos(bisectorAngle);

            // Calculate the angle between the input angle and the nearest vertex angle
            float deltaAngle = Mathf.Abs(angle - nearestVertexAngle);

            // Calculate the distance from the midpoint of the edge to the point on the edge at the given angle
            float edgePointDistance = apothem * Mathf.Tan(deltaAngle);

            // Calculate the distance from the center to the point on the edge at the given angle
            float distanceToEdge = Mathf.Sqrt(apothem * apothem + edgePointDistance * edgePointDistance);

            return distanceToEdge;
        }

        public static void setColors(List<Color> colors)
        {
            PointerManager.m_Instance.SymmetryPointerColors = colors;
        }

        public static List<Color> getColors()
        {
            return PointerManager.m_Instance.SymmetryPointerColors;
        }

        public static void setBrushes(List<string> brushes)
        {
            PointerManager.m_Instance.SymmetryPointerBrushes = brushes.Select(
                x => ApiMethods.LookupBrushDescriptor(x)
            ).Where(x => x != null).ToList();
        }

        public static List<string> getBrushNames()
        {
            return PointerManager.m_Instance.SymmetryPointerBrushes.Select(
                x => x.m_Description
            ).ToList();
        }

        public static List<string> getBrushGuids()
        {
            return PointerManager.m_Instance.SymmetryPointerBrushes.Select(
                x => x.m_Guid.ToString()
            ).ToList();
        }

        public static List<TrTransform> transformsToPolar(List<TrTransform> transforms)
        {
            return pointsToPolar(transforms.Select(x =>
            {
                var vector3 = x.translation;
                return new Vector2(vector3.x, vector3.y);
            }).ToList());
        }

        // Converts an array of points centered on the origin to a list of TrTransforms
        // suitable for use with symmetry scripts default space
        public static List<TrTransform> pointsToPolar(List<Vector2> cartesianPoints)
        {
            var polarCoordinates = new List<TrTransform>();

            foreach (Vector2 point in cartesianPoints)
            {
                float radius = Mathf.Sqrt(point.x * point.x + point.y * point.y);
                float angle = Mathf.Atan2(point.y, point.x);

                polarCoordinates.Add(
                    TrTransform.TR(
                        new Vector3(
                            brushOffset.x * radius,
                            brushOffset.y,
                            brushOffset.z * radius
                        ),
                        Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0)
                    )
                );
            }
            return polarCoordinates;
        }

    }

    [MoonSharpUserData]
    public static class PathApiWrapper
    {
        public static List<TrTransform> fromSvg(string svg, float scale) => LuaApiMethods.PathFromSvg(svg, scale);
        public static List<List<TrTransform>> fromSvgMultiple(string svg) => LuaApiMethods.PathsFromSvg(svg);
        public static List<TrTransform> transform(List<TrTransform> path, TrTransform transform) => LuaApiMethods.TransformPath(path, transform);
        public static List<TrTransform> translate(List<TrTransform> path, Vector3 amount) => LuaApiMethods.TranslatePath(path, amount);
        public static List<TrTransform> rotate(List<TrTransform> path, Quaternion amount) => LuaApiMethods.RotatePath(path, amount);
        public static List<TrTransform> scale(List<TrTransform> path, Vector3 amount) => LuaApiMethods.ScalePath(path, amount);

        public static List<TrTransform> centered(List<TrTransform> path)
        {
            Vector3 centroid = path.Aggregate(
                Vector3.zero,
                (acc, x) => acc + x.translation
            ) / path.Count;
            return path.Select(x => x * TrTransform.T(-centroid)).ToList();
        }

        public static List<TrTransform> startingFrom(List<TrTransform> path, int index)
        {
            return path.Skip(index).Concat(path.Take(index)).ToList();
        }

        public static int findClosest(List<TrTransform> path, Vector3 point)
        {
            return path.Select((x, i) => new {i, x}).Aggregate(
                (acc, x) => (x.x.translation - point).sqrMagnitude < (acc.x.translation - point).sqrMagnitude ? x : acc
            ).i;
        }

        public static int findMinimum(List<TrTransform> path, int axis)
        {
            return path
                .Select((v, i) => (translation: v.translation[axis], index: i))
                .Aggregate((a, b) => a.translation < b.translation ? a : b)
                .index;
        }

        public static int findMaximum(List<TrTransform> path, int axis)
        {
            return path
                .Select((v, i) => (translation: v.translation[axis], index: i))
                .Aggregate((a, b) => a.translation > b.translation ? a : b)
                .index;
        }

        public static List<TrTransform> normalized(List<TrTransform> path)
        {
            // Find the min and max values for each axis
            float minX = path.Min(v => v.translation.x);
            float minY = path.Min(v => v.translation.y);
            float minZ = path.Min(v => v.translation.z);

            float maxX = path.Max(v => v.translation.x);
            float maxY = path.Max(v => v.translation.y);
            float maxZ = path.Max(v => v.translation.z);

            // Compute the range for each axis
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            float rangeZ = maxZ - minZ;

            // Find the largest range to maintain the aspect ratio
            float largestRange = Mathf.Max(rangeX, rangeY, rangeZ);

            // If the largest range is zero, return the original path to avoid division by zero
            if (largestRange == 0)
            {
                return path;
            }

            // Compute the uniform scale factor
            float scaleFactor = 1 / largestRange;

            // Calculate the center of the original path
            Vector3 center = new Vector3(
                (minX + maxX) / 2,
                (minY + maxY) / 2,
                (minZ + maxZ) / 2
            );

            // Apply the scale factor to each Vector3 in the input list
            return path.Select(tr => TrTransform.TRS(
                (tr.translation - center) * scaleFactor,
                tr.rotation,
                tr.scale
            )).ToList();
        }

        public static List<TrTransform> resample(List<TrTransform> path, float spacing)
        {
            if (path == null || path.Count < 2 || spacing <= 0)
            {
                return new List<TrTransform>(path);
            }

            List<TrTransform> resampledPath = new List<TrTransform>();
            resampledPath.Add(path[0]);

            float accumulatedDistance = 0f;
            int originalPathIndex = 0;
            var startPoint = path[0];

            while (originalPathIndex < path.Count - 1)
            {
                var endPoint = path[originalPathIndex + 1];
                float segmentDistance = Vector3.Distance(startPoint.translation, endPoint.translation);
                float remainingDistance = segmentDistance - accumulatedDistance;

                if (accumulatedDistance + segmentDistance >= spacing)
                {
                    float interpolationFactor = (spacing - accumulatedDistance) / segmentDistance;
                    Vector3 newTranslation = Vector3.Lerp(startPoint.translation, endPoint.translation, interpolationFactor);
                    Quaternion newRotation = Quaternion.Lerp(startPoint.rotation, endPoint.rotation, interpolationFactor);
                    float newScale = Mathf.Lerp(startPoint.scale, endPoint.scale, interpolationFactor);
                    var newPoint = TrTransform.TRS(newTranslation, newRotation, newScale);
                    resampledPath.Add(newPoint);
                    startPoint = newPoint;
                    accumulatedDistance = 0f;
                }
                else
                {
                    accumulatedDistance += segmentDistance;
                    startPoint = endPoint;
                    originalPathIndex++;
                }
            }
            resampledPath.Add(path[^1]);
            return resampledPath;
        }
    }

    [MoonSharpUserData]
    public static class DrawApiWrapper
    {
        public static void path(List<TrTransform> path) => LuaApiMethods.DrawPath(path);
        public static void paths(List<List<TrTransform>> paths) => LuaApiMethods.DrawPaths(paths);
        public static void polygon(int sides, TrTransform tr=default) => DrawStrokes.Polygon(sides, tr);
        public static void text(string text, TrTransform tr=default) => DrawStrokes.Text(text, tr);
        public static void svg(string svg, TrTransform tr=default) => DrawStrokes.SvgPath(svg, tr);
        public static void cameraPath(int index) => ApiMethods.DrawCameraPath(index);
    }

    [MoonSharpUserData]
    public static class StrokesApiWrapper
    {
        public static void delete(int index) => ApiMethods.DeleteStroke(index);
        public static void select(int index) => ApiMethods.SelectStroke(index);
        // public static void add(int index) => ApiMethods.AddPointToStroke(index);
        public static void selectMultiple(int from, int to) => ApiMethods.SelectStrokes(from, to);
        // public static void quantize() => ApiMethods.QuantizeSelection(index);
        // public static void addNoise(Vector3 a) => ApiMethods.PerlinNoiseSelection(a);
        public static void join(int from, int to) => ApiMethods.JoinStrokes(from, to);
        public static void joinPrevious() => ApiMethods.JoinStroke();
        public static void import(string name) => ApiMethods.MergeNamedFile(name);
        public static float count => SketchMemoryScript.m_Instance.StrokeCount;
    }

    [MoonSharpUserData]
    public static class HeadsetApiWrapper
    {
        public static void resizeBuffer(int size) => LuaManager.Instance.ResizeHeadBuffer(size);
        public static void setBufferSize(int size) => LuaManager.Instance.SetHeadBufferSize(size);
        public static Vector3 pastPosition(int count) => LuaManager.Instance.GetPastHeadPos(count);
        public static Quaternion pastRotation(int count) => LuaManager.Instance.GetPastHeadRot(count);
    }

    [MoonSharpUserData]
    public static class UserApiWrapper
    {
        public static Vector3 position
        {
            get => App.Scene.Pose.translation;
            set
            {
                TrTransform pose = App.Scene.Pose;
                pose.translation = value;
                float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
                pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
                App.Scene.Pose = pose;
            }
        }
        public static Quaternion rotation
        {
            get => App.Scene.Pose.rotation;
            set
            {
                TrTransform pose = App.Scene.Pose;
                pose.rotation = value;
                float BoundsRadius = SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
                pose = SketchControlsScript.MakeValidScenePose(pose, BoundsRadius);
                App.Scene.Pose = pose;
            }
        }
    }

    [MoonSharpUserData]
    public static class LayerApiWrapper
    {
        public static int getActive => App.Scene.LayerCanvases.ToList().IndexOf(App.Scene.ActiveCanvas);
        public static Vector3 getPosition(int index) => (App.Scene.Pose.inverse * App.Scene.GetLayerByIndex(index).Pose).translation;
        public static void setPosition(int index, Vector3 position)
        {
            var layer = App.Scene.GetLayerByIndex(index);
            var tr = layer.Pose;
            var newTransform = TrTransform.T(position);
            newTransform = App.Scene.Pose * newTransform;
            tr.translation = newTransform.translation;
            layer.Pose = tr;
        }
        public static Quaternion getRotation(int index) => (App.Scene.Pose.inverse * App.Scene.GetLayerByIndex(index).Pose).rotation;
        public static void setRotation(int index, Quaternion rotation)
        {
            var layer = App.Scene.GetLayerByIndex(index);
            var tr = layer.Pose;
            var newTransform = TrTransform.R(rotation);
            newTransform = App.Scene.Pose * newTransform;
            tr.rotation = newTransform.rotation;
            layer.Pose = tr;
        }

        public static void centerPivot(int index)
        {
            App.Scene.GetLayerByIndex(index).CenterPivot();
        }

        public static void showPivot(int index)
        {
            App.Scene.GetLayerByIndex(index).ShowGizmo();
        }

        public static void hidePivot(int index)
        {
            App.Scene.GetLayerByIndex(index).HideGizmo();
        }

        public static TrTransform getTransform(int index) => App.Scene.Pose.inverse * App.Scene.GetLayerByIndex(index).Pose;
        public static void setTransform(int index, TrTransform newTransform)
        {
            newTransform = App.Scene.Pose * newTransform;
            App.Scene.GetLayerByIndex(index).Pose = newTransform;
        }
        public static void add() => ApiMethods.AddLayer();
        public static void clear(int index) => ApiMethods.ClearLayer(index);
        public static void delete(int index) => ApiMethods.DeleteLayer(index);
        public static void squash(int sourceLayer, int destinationLayer) => ApiMethods.SquashLayer(sourceLayer, destinationLayer);
        public static void activate(int index) => ApiMethods.ActivateLayer(index);
        public static void show(int index) => ApiMethods.ShowLayer(index);
        public static void hide(int index) => ApiMethods.HideLayer(index);
        public static void toggle(int index) => ApiMethods.ToggleLayer(index);
        public static int count => App.Scene.LayerCanvases.Count();
    }

    [MoonSharpUserData]
    public static class ImageApiWrapper
    {
        public static void import(string location) => ApiMethods.ImportImage(location);
        public static void select(int index) => ApiMethods.SelectImage(index);
        public static void moveTo(int index, Vector3 position) => ApiMethods.PositionImage(index, position);
        public static TrTransform getTransform(int index) => App.Scene.GetLayerByIndex(index).Pose;
        public static Vector3 getPosition(int index) => App.Scene.GetLayerByIndex(index).Pose.translation;
        public static Quaternion getRotation(int index) => App.Scene.GetLayerByIndex(index).Pose.rotation;
        public static void setPosition(int index, Vector3 position) => Utils._Transform(ItemType.Image, index, TrTransform.T(position));
        public static void setRotation(int index, Quaternion rotation) => Utils._Transform(ItemType.Image, index, TrTransform.R(rotation));
    }

    [MoonSharpUserData]
    public static class ModelApiWrapper
    {
        // public static void import() => ApiMethods.ImportModel();
        public static void select(int index) => ApiMethods.SelectModel(index);
        public static TrTransform getTransform(int index) => App.Scene.GetLayerByIndex(index).Pose;
        public static Vector3 getPosition(int index) => WidgetManager.m_Instance.ActiveModelWidgets[index].WidgetScript.transform.position;
        public static Vector3 getRotation(int index) => WidgetManager.m_Instance.ActiveModelWidgets[index].WidgetScript.transform.position;
        public static void setPosition(int index, Vector3 position) => Utils._Transform(ItemType.Model, index, TrTransform.T(position));
        public static void setRotation(int index, Quaternion rotation) => Utils._Transform(ItemType.Model, index, TrTransform.R(rotation));
    }

    [MoonSharpUserData]
    public static class SelectionApiWrapper
    {
        public static void duplicate() => ApiMethods.Duplicate();
        public static void group() => ApiMethods.ToggleGroupStrokesAndWidgets();
        public static void invert() => ApiMethods.InvertSelection();
        public static void flip() => ApiMethods.FlipSelection();
        public static void recolor() => ApiMethods.RecolorSelection();
        public static void rebrush() => ApiMethods.RebrushSelection();
        public static void resize() => ApiMethods.ResizeSelection();
        public static void trim(int count) => ApiMethods.TrimSelection(count);
        public static void selectAll() => ApiMethods.SelectAll();
    }

    [MoonSharpUserData]
    public static class SketchApiWrapper
    {
        public static void open(string name) => ApiMethods.LoadNamedFile(name);
        public static void save(bool overwrite) => LuaApiMethods.Save(overwrite);
        public static void saveAs(string name) => LuaApiMethods.SaveAs(name);
        public static void export() => ApiMethods.ExportRaw();
        public static void newSketch() => ApiMethods.NewSketch();
        // public static void user() => ApiMethods.LoadUser();
        // public static void curated() => ApiMethods.LoadCurated();
        // public static void liked() => ApiMethods.LoadLiked();
        // public static void drive() => ApiMethods.LoadDrive();
        // public static void exportSelected() => ApiMethods.SaveModel();
    }

    [MoonSharpUserData]
    public static class GuidesApiWrapper
    {
        public static void addCube(TrTransform tr) => _Add(StencilType.Cube, tr);
        public static void addSphere(TrTransform tr) => _Add(StencilType.Sphere, tr);
        public static void addCapsule(TrTransform tr) => _Add(StencilType.Capsule, tr);
        public static void addCone(TrTransform tr) => _Add(StencilType.Cone, tr);
        public static void addEllipsoid(TrTransform tr) => _Add(StencilType.Ellipsoid, tr);
        public static void select(int index) => ApiMethods.SelectGuide(index);
        public static void moveTo(int index, Vector3 position) => ApiMethods.PositionGuide(index, position);
        public static void scale(int index, Vector3 scale) => ApiMethods.ScaleGuide(index, scale);
        public static void toggle() => ApiMethods.StencilsDisable();

        private static void _Add(StencilType type, TrTransform tr)
        {
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
            WidgetManager.m_Instance.GetStencilPrefab(type), tr);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
        }
    }

    [MoonSharpUserData]
    public static class TurtleApiWrapper
    {
        public static TrTransform transform => TrTransform.TR(position, rotation);
        public static Vector3 position => ApiManager.Instance.BrushPosition;
        public static Quaternion rotation => ApiManager.Instance.BrushRotation;
        public static void moveTo(Vector3 position) => ApiMethods.BrushMoveTo(position);
        public static void moveBy(Vector3 amount) => ApiMethods.BrushMoveBy(amount);
        public static void move(float amount) => ApiMethods.BrushMove(amount);
        public static void draw(float amount) => ApiMethods.BrushDraw(amount);
        public static void drawPolygon(int sides, float radius=1, float angle=0) => ApiMethods.DrawPolygon(sides, radius, angle);
        public static void drawText(string text) => ApiMethods.Text(text);
        public static void drawSvg(string svg) => ApiMethods.SvgPath(svg);
        public static void turnY(float angle) => ApiMethods.BrushYaw(angle);
        public static void turnX(float angle) => ApiMethods.BrushPitch(angle);
        public static void turnZ(float angle) => ApiMethods.BrushRoll(angle);
        public static void lookAt(Vector3 amount) => ApiMethods.BrushLookAt(amount);
        public static void lookForwards() => ApiMethods.BrushLookForwards();
        public static void lookUp() => ApiMethods.BrushLookUp();
        public static void lookDown() => ApiMethods.BrushLookDown();
        public static void lookLeft() => ApiMethods.BrushLookLeft();
        public static void lookRight() => ApiMethods.BrushLookRight();
        public static void lookBackwards() => ApiMethods.BrushLookBackwards();
        public static void homeReset() => ApiMethods.BrushHome();
        public static void homeSet() => ApiMethods.BrushSetHome();
        public static void transformPush() => ApiMethods.BrushTransformPush();
        public static void transformPop() => ApiMethods.BrushTransformPop();
    }

    [MoonSharpUserData]
    public static class WaveformApiWrapper
    {
        public static float sine(float time, float frequency) => WaveGenerator.SineWave(time, frequency);
        public static float cosine(float time, float frequency) => WaveGenerator.CosineWave(time, frequency);
        public static float triangle(float time, float frequency) => WaveGenerator.TriangleWave(time, frequency);
        public static float sawtooth(float time, float frequency) => WaveGenerator.SawtoothWave(time, frequency);
        public static float square(float time, float frequency) => WaveGenerator.SquareWave(time, frequency);
        public static float pulse(float time, float frequency, float pulseWidth) => WaveGenerator.PulseWave(time, frequency, pulseWidth);
        public static float exponent(float time, float frequency) => WaveGenerator.ExponentWave(time, frequency);
        public static float power(float time, float frequency, float power) => WaveGenerator.PowerWave(time, frequency, power);
        public static float parabolic(float time, float frequency) => WaveGenerator.ParabolicWave(time, frequency);
        public static float exponentialSawtooth(float time, float frequency, float exponent) => WaveGenerator.ExponentialSawtoothWave(time, frequency, exponent);
        public static float perlinNoise(float time, float frequency) => WaveGenerator.PerlinNoise(time, frequency);
        public static float whiteNoise() => WaveGenerator.WhiteNoise();
        public static float brownNoise(float previous) => WaveGenerator.BrownNoise(previous);
        public static float blueNoise(float previous) => WaveGenerator.BlueNoise(previous);

        // Bulk methods
        public static float[] sine(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SineWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] cosine(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.CosineWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] triangle(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.TriangleWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] sawtooth(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SawtoothWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] square(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SquareWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] exponent(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ExponentWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] parabolic(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ParabolicWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] pulse(float time, float frequency, float pulseWidth, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PulseWave, frequency, pulseWidth, time, duration, sampleRate, amplitude);
        public static float[] power(float time, float frequency, float power, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PowerWave, frequency, power, time, duration, sampleRate, amplitude);
        public static float[] exponentialSawtoothWave(float time, float frequency, float exponent, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ExponentialSawtoothWave, frequency, exponent, time, duration, sampleRate, amplitude);
        public static float[] perlinNoise(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PerlinNoise, frequency, time, duration, sampleRate, amplitude);
        public static float[] whiteNoise(float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.WhiteNoise, duration, sampleRate, amplitude);
        public static float[] brownNoise(float previous, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.BrownNoise, duration, sampleRate, amplitude);
        public static float[] blueNoise(float previous, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.BlueNoise, duration, sampleRate, amplitude);
    }

    [MoonSharpUserData]
    public static class TimerApiWrapper
    {
        public static void set(Closure fn, float interval, float delay = 0, int repeats = -1) => LuaManager.SetTimer(fn, interval, delay, repeats);
        public static void unset(Closure fn) => LuaManager.UnsetTimer(fn);
    }

    enum ItemType
    {
        Image,
        Model,
        Video,
        CameraPath,
        Stencil
    }

    static class Utils
    {
        private static int _NegativeIndexing<T>(int index, IEnumerable<T> enumerable)
        {
            // Python style: negative numbers count from the end
            int count = enumerable.Count();
            if (index < 0) index = count - Mathf.Abs(index);
            return index;
        }

        public static void _Transform(ItemType type, int index, TrTransform tr)
        {
            void _Action(GrabWidget widget)
            {
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(
                        widget,
                        tr,
                        widget.CustomDimension,
                        true
                    )
                );
            }

            switch (type)
            {
                case ItemType.Image:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
                case ItemType.Model:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
                case ItemType.Video:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
                case ItemType.CameraPath:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
                case ItemType.Stencil:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
            }
        }
    }
}
