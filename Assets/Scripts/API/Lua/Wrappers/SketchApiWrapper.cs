using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("Represents the current sketch")]
    [MoonSharpUserData]
    public static class SketchApiWrapper
    {
        [LuaDocsDescription("Opens a sketch with the specified name in the User's Sketches folder")]
        [LuaDocsExample(@"Sketch:Open(""MySketch.tilt"")")]
        [LuaDocsParameter("name", "The filename of the sketch")]
        public static void Open(string name) => ApiMethods.LoadNamedFile(name);

        [LuaDocsDescription("Saves the current sketch, possibly overwriting an existing one")]
        [LuaDocsExample("Sketch:Save(overwrite)")]
        [LuaDocsParameter("overwrite", "If set to true, overwrite the existing file. If false, the method will not overwrite the file")]
        public static void Save(bool overwrite) => LuaApiMethods.Save(overwrite);

        [LuaDocsDescription("Saves the current sketch with a new name")]
        [LuaDocsExample(@"Sketch:SaveAs(""NewName.tilt"")")]
        [LuaDocsParameter("name", "The new name for the sketch")]
        public static void SaveAs(string name) => LuaApiMethods.SaveAs(name);

        [LuaDocsDescription("Exports the sketch in all supported export formats")]
        [LuaDocsExample("Sketch:Export()")]
        public static void Export() => ApiMethods.ExportRaw();

        [LuaDocsDescription("Creates a new sketch")]
        [LuaDocsExample("Sketch:NewSketch()")]
        public static void NewSketch() => ApiMethods.NewSketch(); // public static void user() => ApiMethods.LoadUser();

        // public static void curated() => ApiMethods.LoadCurated();
        // public static void liked() => ApiMethods.LoadLiked();
        // public static void drive() => ApiMethods.LoadDrive();
        // public static void exportSelected() => ApiMethods.SaveModel();

        [LuaDocsDescription(@"Returns a list of active camera paths in the sketch")]
        public static CameraPathListApiWrapper cameraPaths => new CameraPathListApiWrapper(
            WidgetManager.m_Instance.ActiveCameraPathWidgets.Select(w => w.WidgetScript).ToList()
        );

        [LuaDocsDescription(@"Returns a list of all active strokes in the sketch")]
        public static StrokeListApiWrapper strokes => new StrokeListApiWrapper(
            SketchMemoryScript.m_Instance.GetAllActiveStrokes()
        );

        [LuaDocsDescription(@"Returns a list of all layers in the sketch")]
        public static LayerListApiWrapper layers => new LayerListApiWrapper(
            App.Scene.LayerCanvases.ToList()
        );

        [LuaDocsDescription(@"Returns a list of all layers in the sketch")]
        public static LayerApiWrapper mainLayer => new LayerApiWrapper(App.Scene.MainCanvas);

        [LuaDocsDescription("All the groups in this sketch")]
        public static GroupListApiWrapper groups
        {
            get
            {
                var tags = new HashSet<(SketchGroupTag, CanvasScript)>();
                tags.UnionWith(strokes._Strokes.Select(x => (x.Group, x.Canvas)));
                tags.UnionWith(images._Images.Select(x => (x.Group, x.Canvas)));
                tags.UnionWith(videos._Videos.Select(x => (x.Group, x.Canvas)));
                tags.UnionWith(models._Models.Select(x => (x.Group, x.Canvas)));
                tags.UnionWith(guides._Guides.Select(x => (x.Group, x.Canvas)));
                tags.UnionWith(cameraPaths._CameraPaths.Select(x => (x.Group, x.Canvas)));
                var groups = tags.Select(x => new GroupApiWrapper(x.Item1, x.Item2)).ToList();
                return new GroupListApiWrapper(groups);
            }
        }

        [LuaDocsDescription(@"Returns a list of active image widgets in the sketch")]
        public static ImageListApiWrapper images => new ImageListApiWrapper(
            WidgetManager.m_Instance.ActiveImageWidgets.Select(w => w.WidgetScript).ToList()
        );

        [LuaDocsDescription(@"Returns a list of active video widgets in the sketch")]
        public static VideoListApiWrapper videos => new VideoListApiWrapper(
            WidgetManager.m_Instance.ActiveVideoWidgets.Select(w => w.WidgetScript).ToList()
        );

        [LuaDocsDescription(@"Returns a list of active model widgets in the sketch")]
        public static ModelListApiWrapper models => new ModelListApiWrapper(
            WidgetManager.m_Instance.ActiveModelWidgets.Select(w => w.WidgetScript).ToList()
        );

        [LuaDocsDescription(@"Returns a list of active stencil widgets in the sketch")]
        public static GuideListApiWrapper guides => new GuideListApiWrapper(
            WidgetManager.m_Instance.ActiveStencilWidgets.Select(w => w.WidgetScript).ToList()
        );

        [LuaDocsDescription(@"Returns a list of all the available environments")]
        public static EnvironmentListApiWrapper environments => new EnvironmentListApiWrapper(
            EnvironmentCatalog.m_Instance.AllEnvironments.ToList()
        );

        [LuaDocsDescription("Imports a image with the specified name from the MediaLibrary/BackgroundImages folder and assigns it as a custom skybox")]
        [LuaDocsExample(@"App:ImportSkybox(""landscape.hdr"")")]
        [LuaDocsParameter("filename", "The filename of the image")]
        public static void ImportSkybox(string filename)
        {
            ApiMethods.ImportSkybox(filename);
        }

        private static CustomLights _CustomLights => LightsControlScript.m_Instance.CustomLightsFromScene;

        [LuaDocsDescription(@"The ambient light color")]
        public static ColorApiWrapper ambientLightColor
        {
            get => new(_CustomLights.Ambient);
            set => _CustomLights.Ambient = value._Color;
        }

        [LuaDocsDescription(@"The main light's color")]
        public static ColorApiWrapper mainLightColor
        {
            get => new(_CustomLights.Shadow.Color);
            set
            {
                _CustomLights.Shadow.Color = value._Color;
                App.Scene.GetLight((int)LightMode.Shadow).color = value._Color;
            }
        }

        [LuaDocsDescription(@"The secondary light's color")]
        public static ColorApiWrapper secondaryLightColor
        {
            get => new(_CustomLights.NoShadow.Color);
            set
            {
                _CustomLights.NoShadow.Color = value._Color;
                App.Scene.GetLight((int)LightMode.NoShadow).color = value._Color;
            }
        }

        [LuaDocsDescription(@"The main light's rotation")]
        public static RotationApiWrapper mainLightRotation
        {
            get => new(_CustomLights.Shadow.Orientation);
            set
            {
                _CustomLights.Shadow.Orientation = value._Quaternion;
                App.Scene.GetLight((int)LightMode.Shadow).transform.rotation = value._Quaternion;
            }
        }

        [LuaDocsDescription(@"The secondary light's rotation")]
        public static RotationApiWrapper secondaryLightRotation
        {
            get => new(_CustomLights.NoShadow.Orientation);
            set
            {
                _CustomLights.NoShadow.Orientation = value._Quaternion;
                App.Scene.GetLight((int)LightMode.NoShadow).transform.rotation = value._Quaternion;
            }
        }
    }
}
