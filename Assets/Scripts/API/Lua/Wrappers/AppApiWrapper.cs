using System.IO;
using MoonSharp.Interpreter;
using ODS;
using UnityAsyncAwaitUtil;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("Various properties and methods that effect the entire app")]
    [MoonSharpUserData]
    public static class AppApiWrapper
    {
        [LuaDocsDescription("The time in seconds since Open Brush was launched")]
        public static float time => UnityEngine.Time.realtimeSinceStartup;

        [LuaDocsDescription("The number of frames that have been rendered since Open Brush was launched")]
        public static float frames => UnityEngine.Time.frameCount;

        [LuaDocsDescription("Whether physics simulation is active (defaults is off)")]
        public static bool physics
        {
            get => UnityEngine.Physics.autoSimulation;
            set => UnityEngine.Physics.autoSimulation = value;
        }

        [LuaDocsDescription("The current scale of the scene")]
        public static float currentScale => App.Scene.Pose.scale;

        [LuaDocsDescription("Undo the last action")]
        [LuaDocsExample("App:Undo()")]
        public static void Undo() => ApiMethods.Undo();

        [LuaDocsDescription("Redo the previously undone action")]
        [LuaDocsExample("App:Redo()")]
        public static void Redo() => ApiMethods.Redo();

        [LuaDocsDescription("Adds a url that should be sent the data for each stroke as soon as the user finishes drawing it")]
        [LuaDocsExample(@"App:AddListener(""http://example.com"")")]
        [LuaDocsParameter("url", "The url to send the stroke data to")]
        public static void AddListener(string url) => ApiMethods.AddListener(url);

        [LuaDocsDescription("Reset all panels")]
        [LuaDocsExample("App:ResetPanels()")]
        public static void ResetPanels() => ApiMethods.ResetAllPanels();

        [LuaDocsDescription("Opens an Explorer/Finder window outside of VR showing the user's Scripts folder on the desktop (Mac/Windows only)")]
        [LuaDocsExample("App:ShowScriptsFolder()")]
        public static void ShowScriptsFolder() => ApiMethods.OpenUserScriptsFolder();

        [LuaDocsDescription("Opens an Explorer/Finder window outside of VR showing the user's Export folder on the desktop (Mac/Windows only)")]
        [LuaDocsExample("App:ShowExportFolder()")]
        public static void ShowExportFolder() => ApiMethods.OpenExportFolder();

        [LuaDocsDescription("Opens an Explorer/Finder window outside of VR showing the user's Sketches folder on the desktop (Mac/Windows only)")]
        [LuaDocsExample("App:ShowSketchesFolder()")]
        public static void ShowSketchesFolder() => ApiMethods.ShowSketchFolder();

        [LuaDocsDescription("Activate or deactivate straight edge mode")]
        [LuaDocsExample("App:StraightEdge(true)")]
        [LuaDocsParameter("active", "True means activate, false means deactivate")]
        public static void StraightEdge(bool active) => LuaApiMethods.StraightEdge(active);

        [LuaDocsDescription("Activate or deactivate auto orientation mode")]
        [LuaDocsExample("App:AutoOrient(true)")]
        [LuaDocsParameter("active", "True means activate, false means deactivate")]
        public static void AutoOrient(bool active) => LuaApiMethods.AutoOrient(active);

        [LuaDocsDescription("Activate or deactivate view only mode")]
        [LuaDocsExample("App:ViewOnly(true)")]
        [LuaDocsParameter("active", "True means activate, false means deactivate")]
        public static void ViewOnly(bool active) => LuaApiMethods.ViewOnly(active);

        [LuaDocsDescription("Activate or deactivate auto simplification mode")]
        [LuaDocsExample("App:AutoSimplify(true)")]
        [LuaDocsParameter("active", "True means activate, false means deactivate")]
        public static void AutoSimplify(bool active) => LuaApiMethods.AutoSimplify(active);

        [LuaDocsDescription("Activate or deactivate disco mode")]
        [LuaDocsExample("App:Disco(true)")]
        [LuaDocsParameter("active", "True means activate, false means deactivate")]
        public static void Disco(bool active) => LuaApiMethods.Disco(active);

        [LuaDocsDescription("Activate or deactivate profiling mode")]
        [LuaDocsExample("App:Profiling(true)")]
        [LuaDocsParameter("active", "True means activate, false means deactivate")]
        public static void Profiling(bool active) => LuaApiMethods.Profiling(active);

        [LuaDocsDescription("Activate or deactivate post-processing")]
        [LuaDocsExample("App:PostProcessing(true)")]
        [LuaDocsParameter("active", "True means activate, false means deactivate")]
        public static void PostProcessing(bool active) => LuaApiMethods.PostProcessing(active);

        [LuaDocsDescription("Set the drafting mode to visible")]
        [LuaDocsExample("App:DraftingVisible()")]
        public static void DraftingVisible() => ApiMethods.DraftingVisible();

        [LuaDocsDescription("Set the drafting mode to transparent")]
        [LuaDocsExample("App:DraftingTransparent()")]
        public static void DraftingTransparent() => ApiMethods.DraftingTransparent();

        [LuaDocsDescription("Set the drafting mode to hidden")]
        [LuaDocsExample("App:DraftingHidden()")]
        public static void DraftingHidden() => ApiMethods.DraftingHidden();

        [LuaDocsDescription("Get or set the current environment by name")]
        // [LuaDocsExample("App.environment = ""Space"")]
        public static string environment
        {
            get => SceneSettings.m_Instance.CurrentEnvironment.Description;
            set => ApiMethods.SetEnvironment(value);
        }

        [LuaDocsDescription("Activate or deactivate the watermark")]
        [LuaDocsExample("App:Watermark(true)")]
        [LuaDocsParameter("active", "True means activate, false means deactivate")]
        public static void Watermark(bool active) => LuaApiMethods.Watermark(active);

        // TODO Unified API for tools and panels
        // [LuaDocsDescription("Shows or hides the settings panel")]
        // public static void SettingsPanel(bool active) => )LuaApiMethods.SettingsPanel)(active);
        // public static void SketchOrigin(bool active) => )LuaApiMethods.SketchOrigin)(active);

        [LuaDocsDescription("Get or set the clipboard text")]
        // [LuaDocsExample("App.clipboardText = ""Hello, World!"")]
        public static string clipboardText
        {
            get => SystemClipboard.GetClipboardText();
            set => SystemClipboard.SetClipboardText(value);
        }

        // [LuaDocsDescription("Gets the current image in the clipboard")]
        // public static Texture2D clipboardImage {
        //     get => SystemClipboard.GetClipboardImage();
        //     // set => SystemClipboard.SetClipboardImage(value);
        // }

        [LuaDocsDescription("Read the contents of a file")]
        [LuaDocsExample(@"App:ReadFile(""path/to/file.txt"")")]
        [LuaDocsParameter("path", "The file path to read from. It must be relative to and contined within the Scripts folder")]
        [LuaDocsReturnValue("The contents of the file as a string")]
        public static string ReadFile(string path)
        {
            bool valid = false;
            // Disallow absolute paths
            valid = !Path.IsPathRooted(path);
            if (valid)
            {
                path = Path.Join(LuaManager.Instance.UserPluginsPath(), path);
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

        [LuaDocsDescription("Displays an error message on the back of the user's brush controller")]
        [LuaDocsExample(@"App:Error(""This is an error message."")")]
        [LuaDocsParameter("message", "The error message to display")]
        public static void Error(string message) => LuaManager.Instance.LogLuaErrorRaisedByScript(message);

        [LuaDocsDescription("Set the font used for drawing text")]
        [LuaDocsExample(@"App:SetFont(""fontData"")")]
        [LuaDocsParameter("fontData", "Font data in .chr format")]
        public static void SetFont(string fontData) => ApiManager.Instance.SetTextFont(fontData);

        [LuaDocsDescription("Take a snapshot of your scene and save it to your Snapshots folder")]
        [LuaDocsExample(@"App:TakeSnapshop(Transform:New(0, 12, 3), ""mysnapshot.png"", 1024, 768, true)")]
        [LuaDocsParameter("tr", "Determines the position and orientation of the camera used to take the snapshot")]
        [LuaDocsParameter("filename", "The filename to use for the saved snapshot")]
        [LuaDocsParameter("width", "Image width")]
        [LuaDocsParameter("height", "Image height")]
        [LuaDocsParameter("superSampling", "The supersampling strength to apply (between 0.125 and 4.0)")]
        [LuaDocsParameter("renderDepth", "If true then render the depth buffer instead of the image")]
        public static void TakeSnapshot(TrTransform tr, string filename, int width, int height, float superSampling = 1f, bool renderDepth = false, bool removeBackground = false)
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
                var rMgr = rig.ManagerFromStyle(MultiCamStyle.Snapshot);
                var initialState = rig.gameObject.activeSelf;
                rig.gameObject.SetActive(true);
                RenderTexture tmp = rMgr.CreateTemporaryTargetForSave(width, height);
                RenderWrapper wrapper = rMgr.gameObject.GetComponent<RenderWrapper>();
                float ssaaRestore = wrapper.SuperSampling;
                wrapper.SuperSampling = superSampling;
                rMgr.RenderToTexture(tmp, asDepth: renderDepth, removeBackground: removeBackground);
                wrapper.SuperSampling = ssaaRestore;
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    ScreenshotManager.Save(fs, tmp, bSaveAsPng: saveAsPng);
                }
                rig.gameObject.SetActive(initialState);
            }
        }

        [LuaDocsDescription("Take a 360-degree snapshot of the scene and save it")]
        [LuaDocsExample(@"App:Take360Snapshot(Transform:Position(0, 12, 3), ""my360snapshot.png"", 4096)")]
        [LuaDocsParameter("tr", "Determines the position and orientation of the camera used to take the snapshot")]
        [LuaDocsParameter("filename", "The filename to use for the saved snapshot")]
        [LuaDocsParameter("width", "The width of the image")]
        public static void Take360Snapshot(TrTransform tr, string filename, int width = 4096)
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
}