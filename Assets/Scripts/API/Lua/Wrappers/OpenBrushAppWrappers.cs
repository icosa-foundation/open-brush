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
        public static float size => PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
        public static float size01 => PointerManager.m_Instance.MainPointer.BrushSize01;
        public static float pressure => PointerManager.m_Instance.MainPointer.GetPressure();
        // ReSharper disable once Unity.NoNullPropagation
        public static string name => PointerManager.m_Instance.MainPointer.CurrentBrush?.m_Description;
        public static void type(string brushName) => ApiMethods.Brush(brushName);
        public static float speed => PointerManager.m_Instance.MainPointer.MovementSpeed;
        public static Color color => PointerManager.m_Instance.PointerColor;
        public static Color lastColorPicked => PointerManager.m_Instance.m_lastChosenColor;
        public static Vector3 pastPosition(int back) => LuaManager.Instance.GetPastBrushPos(back);
        public static Quaternion pastRotation(int back) => LuaManager.Instance.GetPastBrushRot(back);
        public static void sizeSet(float amount) => ApiMethods.BrushSizeSet(amount);
        public static void sizeAdd(float amount) => ApiMethods.BrushSizeAdd(amount);
        public static void forcePaintingOn(bool active) => ApiMethods.ForcePaintingOn(active);
        public static void forcePaintingOff(bool active) => ApiMethods.ForcePaintingOff(active);

        public static Vector3 colorHsv
        {
            get
            {
                Color.RGBToHSV(color, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
        }

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
        public static void setEnvironment(string environmentName) => ApiMethods.SetEnvironment(environmentName);
        public static void watermark(bool a) => LuaApiMethods.Watermark(a);
        // TODO Unified API for tools and panels
        // public static void SettingsPanel(bool a) => )LuaApiMethods.SettingsPanel)(a);
        // public static void SketchOrigin(bool a) => )LuaApiMethods.SketchOrigin)(a);
    }


    [MoonSharpUserData]
    public static class SpectatorApiWrapper
    {
        public static void moveTo(Vector3 position) => ApiMethods.MoveSpectatorTo(position);
        public static void moveBy(Vector3 distance) => ApiMethods.MoveSpectatorBy(distance);
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
    }

    [MoonSharpUserData]
    public static class SymmetryApiWrapper
    {
        public static Vector3 position => PointerManager.m_Instance.SymmetryWidget.transform.position;
        public static Quaternion rotation => PointerManager.m_Instance.SymmetryWidget.transform.rotation;
        public static Vector3 direction => PointerManager.m_Instance.SymmetryWidget.transform.rotation * Vector3.forward;
        public static void mirror() => ApiMethods.SymmetryPlane();
        public static void doubleMirror() => ApiMethods.SymmetryFour();
        public static void twoHandeded() => ApiMethods.SymmetryTwoHanded();
        public static void setPosition(Vector3 position) => ApiMethods.SymmetrySetPosition(position);
        public static void setRotation(Quaternion rotation) => ApiMethods.SymmetrySetRotation(rotation);
        public static void setTransform(Vector3 position, Quaternion rotation) => ApiMethods.SymmetrySetTransform(position, rotation);
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
    public static class ColorApiWrapper
    {
        public static void addHsv(Vector3 color) => ApiMethods.AddColorHSV(color);
        public static void addRgb(Vector3 color) => ApiMethods.AddColorRGB(color);
        public static void setRgb(Vector3 color) => ApiMethods.SetColorRGB(color);
        public static void setHsv(Vector3 color) => ApiMethods.SetColorHSV(color);
        public static void setHtml(string color) => ApiMethods.SetColorHTML(color);
        public static void jitter() => LuaApiMethods.JitterColor();
    }

    [MoonSharpUserData]
    public static class UserApiWrapper
    {
        public static void moveTo(Vector3 position) => ApiMethods.MoveUserTo(position);
        public static void moveBy(Vector3 distance) => ApiMethods.MoveUserBy(distance);
    }

    [MoonSharpUserData]
    public static class LayerApiWrapper
    {
        public static int active => App.Scene.LayerCanvases.ToList().IndexOf(App.Scene.ActiveCanvas);
        public static TrTransform transform(int index) => App.Scene.LayerCanvases.ToList()[index].Pose;
        public static Vector3 position(int index) => App.Scene.LayerCanvases.ToList()[index].Pose.translation;
        public static Quaternion rotation(int index) => App.Scene.LayerCanvases.ToList()[index].Pose.rotation;
        public static void moveTo(int index, Vector3 position) =>  _MoveTo(index, position);
        public static void moveBy(int index, Vector3 distance) => _MoveBy(index, distance);
        public static void add() => ApiMethods.AddLayer();
        public static void clear(int index) => ApiMethods.ClearLayer(index);
        public static void delete(int index) => ApiMethods.DeleteLayer(index);
        public static void squash(int sourceLayer, int destinationLayer) => ApiMethods.SquashLayer(sourceLayer, destinationLayer);
        public static void activate(int index) => ApiMethods.ActivateLayer(index);
        public static void show(int index) => ApiMethods.ShowLayer(index);
        public static void hide(int index) => ApiMethods.HideLayer(index);
        public static void toggle(int index) => ApiMethods.ToggleLayer(index);

        public static void _MoveTo(int index, Vector3 pos)
        {
            var layer = App.Scene.LayerCanvases.ToList()[index];
            var tr = layer.Pose;
            tr.translation = pos;
            layer.Pose = tr;
        }
        public static void _MoveBy(int index, Vector3 distance)
        {
            var layer = App.Scene.LayerCanvases.ToList()[index];
            var tr = layer.Pose;
            tr.translation += distance;
            layer.Pose = tr;
        }
        public static void _TransformLayer(int index, TrTransform tr)
        {
            var layer = App.Scene.LayerCanvases.ToList()[index];
            layer.Pose = tr;
        }
    }

    [MoonSharpUserData]
    public static class ImageApiWrapper
    {
        public static void import(string location) => ApiMethods.ImportImage(location);
        public static void select(int index) => ApiMethods.SelectImage(index);
        public static void moveTo(int index, Vector3 position) => ApiMethods.PositionImage(index, position);
    }

    [MoonSharpUserData]
    public static class ModelApiWrapper
    {
        // public static void import() => ApiMethods.ImportModel();
        public static void select(int index) => ApiMethods.SelectModel(index);
        public static void moveTo(int index, Vector3 position) => ApiMethods.PositionModel(index, position);
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
}
