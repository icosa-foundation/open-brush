using System.IO;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class SketchApiWrapper
    {
        public static void Open(string name) => ApiMethods.LoadNamedFile(name);
        public static void Save(bool overwrite) => LuaApiMethods.Save(overwrite);
        public static void SaveAs(string name) => LuaApiMethods.SaveAs(name);
        public static void Export() => ApiMethods.ExportRaw();
        public static void NewSketch() => ApiMethods.NewSketch();
        // public static void user() => ApiMethods.LoadUser();
        // public static void curated() => ApiMethods.LoadCurated();
        // public static void liked() => ApiMethods.LoadLiked();
        // public static void drive() => ApiMethods.LoadDrive();
        // public static void exportSelected() => ApiMethods.SaveModel();

        public static CameraPathListApiWrapper cameraPaths => new CameraPathListApiWrapper(
            WidgetManager.m_Instance.ActiveCameraPathWidgets.Select(w => w.WidgetScript).ToList()
        );

        public static StrokeListApiWrapper strokes => new StrokeListApiWrapper(
            SketchMemoryScript.m_Instance.GetAllActiveStrokes()
        );

        public static LayerListApiWrapper layers => new LayerListApiWrapper(
            App.Scene.LayerCanvases.ToList()
        );

        public static ImageListApiWrapper images => new ImageListApiWrapper(
            WidgetManager.m_Instance.ActiveImageWidgets.Select(w => w.WidgetScript).ToList()
        );

        public static VideoListApiWrapper videos => new VideoListApiWrapper(
            WidgetManager.m_Instance.ActiveVideoWidgets.Select(w => w.WidgetScript).ToList()
        );

        public static ModelListApiWrapper models => new ModelListApiWrapper(
            WidgetManager.m_Instance.ActiveModelWidgets.Select(w => w.WidgetScript).ToList()
        );

        public static GuideListApiWrapper guides => new GuideListApiWrapper(
            WidgetManager.m_Instance.ActiveStencilWidgets.Select(w => w.WidgetScript).ToList()
        );

        public static EnvironmentListApiWrapper environments => new EnvironmentListApiWrapper(
            EnvironmentCatalog.m_Instance.AllEnvironments.ToList()
        );

        public static void ImportSkybox(string location)
        {
            ApiMethods.ImportSkybox(location);
        }

        private static CustomLights _CustomLights => LightsControlScript.m_Instance.CustomLightsFromScene;

        public static ColorApiWrapper ambientLightColor
        {
            get => new(_CustomLights.Ambient);
            set => _CustomLights.Ambient = value._Color;
        }

        public static ColorApiWrapper mainLightColor
        {
            get => new(_CustomLights.Shadow.Color);
            set
            {
                _CustomLights.Shadow.Color = value._Color;
                App.Scene.GetLight((int)LightMode.Shadow).color = value._Color;
            }
        }

        public static ColorApiWrapper secondaryLightColor
        {
            get => new(_CustomLights.NoShadow.Color);
            set
            {
                _CustomLights.NoShadow.Color = value._Color;
                App.Scene.GetLight((int)LightMode.NoShadow).color = value._Color;
            }
        }

        public static RotationApiWrapper mainLightRotation
        {
            get => new(_CustomLights.Shadow.Orientation);
            set
            {
                _CustomLights.Shadow.Orientation = value._Quaternion;
                App.Scene.GetLight((int)LightMode.Shadow).transform.rotation = value._Quaternion;
            }
        }

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
