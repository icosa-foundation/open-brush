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
using System.Linq;
using MoonSharp.Interpreter;
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
        public static Vector3 pastPosition(int back) => LuaManager.Instance.GetPastBrushPos(back);
        public static Quaternion pastRotation(int back) => LuaManager.Instance.GetPastBrushRot(back);
        public static void forcePaintingOn(bool active) => ApiMethods.ForcePaintingOn(active);
        public static void forcePaintingOff(bool active) => ApiMethods.ForcePaintingOff(active);

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
        public static Vector3 pastPosition(int back) => LuaManager.Instance.GetPastWandPos(back);
        public static Quaternion pastRotation(int back) => LuaManager.Instance.GetPastWandRot(back);
    }

    [MoonSharpUserData]
    public static class AppApiWrapper
    {
        public static float time => Time.realtimeSinceStartup;
        public static float frames => Time.frameCount;
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
        public static Vector3 direction => PointerManager.m_Instance.SymmetryWidget.transform.rotation * Vector3.forward;
        public static void mirror() => ApiMethods.SymmetryPlane();
        public static void doubleMirror() => ApiMethods.SymmetryFour();
        public static void twoHandeded() => ApiMethods.SymmetryTwoHanded();
        public static void summonWidget() => ApiMethods.SummonMirror();
        public static void spin(Vector3 rot) => PointerManager.m_Instance.SymmetryWidget.Spin(rot);
    }

    [MoonSharpUserData]
    public static class PathApiWrapper
    {
        public static List<TrTransform> fromSvg(string svg) => LuaApiMethods.PathFromSvg(svg);
        public static List<List<TrTransform>> fromSvgMultiple(string svg) => LuaApiMethods.PathsFromSvg(svg);
        public static List<TrTransform> transform(List<TrTransform> path, TrTransform transform) => LuaApiMethods.TransformPath(path, transform);
        public static List<TrTransform> translate(List<TrTransform> path, Vector3 amount) => LuaApiMethods.TranslatePath(path, amount);
        public static List<TrTransform> rotate(List<TrTransform> path, Quaternion amount) => LuaApiMethods.RotatePath(path, amount);
        public static List<TrTransform> scale(List<TrTransform> path, Vector3 amount) => LuaApiMethods.ScalePath(path, amount);
    }

    [MoonSharpUserData]
    public static class DrawApiWrapper
    {
        public static void path(List<TrTransform> path) => LuaApiMethods.DrawPath(path);
        public static void paths(List<List<TrTransform>> paths) => LuaApiMethods.DrawPaths(paths);
        public static void polygon(int sides, float radius, float angle) => ApiMethods.DrawPolygon(sides, radius, angle);
        public static void text(string text) => ApiMethods.Text(text);
        public static void svg(string svg) => ApiMethods.SvgPath(svg);
        public static void cameraPath(int index) => ApiMethods.DrawCameraPath(index);
        // The Http Api commands for these take strings as input which we don't want
        // public static void paths() => ApiMethods.DrawPaths();
        // public static void path() => ApiMethods.DrawPath();
        // public static void stroke() => ApiMethods.DrawStroke();
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
        public static TrTransform getTransform(int index) => App.Scene.LayerCanvases.ToList()[index].Pose;
        public static Vector3 getPosition(int index) => App.Scene.LayerCanvases.ToList()[index].Pose.translation;
        public static void setPosition(int index, Vector3 position)
        {
            var tr =  App.Scene.LayerCanvases.ToList()[index].Pose;
            tr.translation = position;
            App.Scene.LayerCanvases.ToList()[index].Pose = tr;
        }
        public static Quaternion getRotation(int index) => App.Scene.LayerCanvases.ToList()[index].Pose.rotation;
        public static void setRotation(int index, Quaternion rotation)
        {
            var tr =  App.Scene.LayerCanvases.ToList()[index].Pose;
            tr.rotation = rotation;
            App.Scene.LayerCanvases.ToList()[index].Pose = tr;
        }
        public static void add() => ApiMethods.AddLayer();
        public static void clear(int index) => ApiMethods.ClearLayer(index);
        public static void delete(int index) => ApiMethods.DeleteLayer(index);
        public static void squash(int sourceLayer, int destinationLayer) => ApiMethods.SquashLayer(sourceLayer, destinationLayer);
        public static void activate(int index) => ApiMethods.ActivateLayer(index);
        public static void show(int index) => ApiMethods.ShowLayer(index);
        public static void hide(int index) => ApiMethods.HideLayer(index);
        public static void toggle(int index) => ApiMethods.ToggleLayer(index);
    }

    [MoonSharpUserData]
    public static class ImageApiWrapper
    {
        public static void import(string location) => ApiMethods.ImportImage(location);
        public static void select(int index) => ApiMethods.SelectImage(index);
        public static void moveTo(int index, Vector3 position) => ApiMethods.PositionImage(index, position);
        public static TrTransform getTransform(int index) => App.Scene.LayerCanvases.ToList()[index].Pose;
        public static Vector3 getPosition(int index) => App.Scene.LayerCanvases.ToList()[index].Pose.translation;
        public static Quaternion getRotation(int index) => App.Scene.LayerCanvases.ToList()[index].Pose.rotation;
        public static void setPosition(int index, Vector3 position) => Utils._Transform(ItemType.Image, index, TrTransform.T(position));
        public static void setRotation(int index, Quaternion rotation) => Utils._Transform(ItemType.Image, index, TrTransform.R(rotation));
    }

    [MoonSharpUserData]
    public static class ModelApiWrapper
    {
        // public static void import() => ApiMethods.ImportModel();
        public static void select(int index) => ApiMethods.SelectModel(index);
        public static TrTransform getTransform(int index) => App.Scene.LayerCanvases.ToList()[index].Pose;
        public static Vector3 getPosition(int index) => WidgetManager.m_Instance.ActiveModelWidgets[index].WidgetScript.transform.position;
        public static Vector3 getRotation(int index) => WidgetManager.m_Instance.ActiveModelWidgets[index].WidgetScript.transform.position;
        public static void setPosition(int index, Vector3 position) => Utils._Transform(ItemType.Model, index, TrTransform.T(position));
        public static void setRotation(int index, Quaternion rotation) => Utils._Transform(ItemType.Model, index, TrTransform.R(rotation));
    }

    [MoonSharpUserData]
    public static class CamerapathApiWrapper
    {
        public static void render() => ApiMethods.RenderCameraPath();
        public static void toggleVisuals() => ApiMethods.ToggleCameraPathVisuals();
        public static void togglePreview() => ApiMethods.ToggleCameraPathPreview();
        public static void delete() => ApiMethods.DeleteCameraPath();
        public static void record() => ApiMethods.RecordCameraPath();
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
        public static float sine(float time, float frequency) => PointerManager.CalcWaveform(time, PointerManager.Waveform.SineWave, frequency);
        public static float triangle(float time, float frequency) => PointerManager.CalcWaveform(time, PointerManager.Waveform.TriangleWave, frequency);
        public static float sawtooth(float time, float frequency) => PointerManager.CalcWaveform(time, PointerManager.Waveform.SawtoothWave, frequency);
        public static float square(float time, float frequency) => PointerManager.CalcWaveform(time, PointerManager.Waveform.SquareWave, frequency);
        public static float noise(float time, float frequency) => PointerManager.CalcWaveform(time, PointerManager.Waveform.Noise, frequency);
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

            switch(type)
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
