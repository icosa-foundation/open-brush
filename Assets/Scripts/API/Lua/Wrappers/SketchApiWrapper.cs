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

        public static LightListApiWrapper lights => new LightListApiWrapper(
            // TODO should this just be custom lights?
            App.Scene.Lights.ToList()
        );

        public static EnvironmentListApiWrapper environments => new EnvironmentListApiWrapper(
            EnvironmentCatalog.m_Instance.AllEnvironments.ToList()
        );

        public static void SetSkybox(ImageApiWrapper imagewrapper)
        {
            var tex = imagewrapper._ImageWidget.ReferenceImage.FullSize;
            RenderSettings.skybox = new Material(Resources.Load<Material>("CustomSkybox"));
            RenderSettings.skybox.SetTexture("_Tex", tex);
        }
    }

}
