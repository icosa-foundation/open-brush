using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Platforms;

namespace TiltBrush
{
    public class LuaManager : MonoBehaviour
    {
        private FileWatcher m_FileWatcher;
        private static LuaManager m_Instance;
        private static string DocumentsDirectory => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Open Brush");
        private static string ScriptsDirectory => Path.Combine(DocumentsDirectory, "Scripts");

        private ApiManager apiManager;

        private static readonly string LuaFileSearchPattern = "*.lua";

        public enum ApiCategories
        {
            PointerScript = 0, // Modifies the pointer position on every frame
            ToolScript = 1,    // A scriptable tool that can create strokes based on click/drag/release
            SymmetryScript = 2 // Generates copies of each new stroke with different transforms
            // Scripts that modify brush settings for each new stroke (JitterScript?) Maybe combine with Pointerscript
            // Scripts that modify existing strokes (RepaintScript?)
            // Scriptable Brush mesh generation (BrushScript?)
            // Same as above but applies to the current selection with maybe some logic based on index within selection
        }

        [NonSerialized] public List<Script> PointerScripts;
        [NonSerialized] public List<Script> ToolScripts;
        [NonSerialized] public List<Script> SymmetryScripts;
        [NonSerialized] public List<string> PointerScriptNames;
        [NonSerialized] public List<string> ToolScriptNames;
        [NonSerialized] public List<string> SymmetryScriptNames;
        [NonSerialized] public int CurrentPointerScript;
        [NonSerialized] public int CurrentToolScript;
        [NonSerialized] public int CurrentSymmetryScript;

        private bool PointerScriptsEnabled;

        public static LuaManager Instance => m_Instance;

        void Awake()
        {
            m_Instance = this;
            if (Directory.Exists(ScriptsDirectory))
            {
                m_FileWatcher = new FileWatcher(ScriptsDirectory, "*.lua");
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnScriptsDirectoryChanged;
                m_FileWatcher.FileCreated += OnScriptsDirectoryChanged;
                // m_FileWatcher.FileDeleted += OnScriptsDirectoryChanged; TODO
                m_FileWatcher.EnableRaisingEvents = true;
            }
        }

        private void OnScriptsDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            LoadScript(e.FullPath);
        }


        private void Start()
        {
            UserData.RegisterAssembly();
            Script.GlobalOptions.Platform = new StandardPlatformAccessor();
            LuaCustomConverters.RegisterAll();
            LoadScripts();
        }

        public void LoadScripts()
        {
            PointerScripts = new List<Script>();
            PointerScriptNames = new List<string>();
            ToolScripts = new List<Script>();
            ToolScriptNames = new List<string>();
            SymmetryScripts = new List<Script>();
            SymmetryScriptNames = new List<string>();
            Directory.CreateDirectory(ScriptsDirectory);
            string[] files = Directory.GetFiles(ScriptsDirectory, LuaFileSearchPattern, SearchOption.AllDirectories);

            foreach (string scriptPath in files)
                LoadScript(scriptPath);
        }


        private void LoadScript(string path)
        {
            Script script = new Script();

            script.Options.DebugPrint = s => Debug.Log(s);

            string scriptFilename = Path.GetFileNameWithoutExtension(path);

            // Execute script
            Stream fileStream = new FileStream(path, FileMode.Open);
            script.DoStream(fileStream);

            foreach (ApiCategories category in Enum.GetValues(typeof(ApiCategories)))
            {
                var categoryName = category.ToString();
                if (!scriptFilename.StartsWith(categoryName)) continue;
                string scriptName = scriptFilename.Substring(categoryName.Length + 1);

                switch (category)
                {
                    case ApiCategories.PointerScript:
                        PointerScripts.Add(script);
                        PointerScriptNames.Add(scriptName);
                        break;
                    case ApiCategories.ToolScript:
                        ToolScripts.Add(script);
                        ToolScriptNames.Add(scriptName);
                        break;
                    case ApiCategories.SymmetryScript:
                        SymmetryScripts.Add(script);
                        SymmetryScriptNames.Add(scriptName);
                        break;
                }
            }

            fileStream.Close();
        }

        public Script SetScriptContext(Script script)
        {
            var pointerColor = PointerManager.m_Instance.PointerColor;
            float hue, S, V;
            Color.RGBToHSV(pointerColor, out hue, out S, out V);

            // Angle the pointer according to the user-defined pointer angle.
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 pos = rAttachPoint.position;
            Quaternion rot = rAttachPoint.rotation * Quaternion.Euler(new Vector3(0, 180, 0));

            DynValue pointer = DynValue.NewTable(new Table(script));
            pointer.Table["position"] = pos;
            pointer.Table["rotation"] = rot.eulerAngles;
            pointer.Table["rgb"] = new Vector3(pointerColor.r, pointerColor.g, pointerColor.b);
            pointer.Table["hsv"] = new Vector3(hue, S, V);
            pointer.Table["size01"] = PointerManager.m_Instance.MainPointer.BrushSize01;
            pointer.Table["size"] = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
            pointer.Table["brush"] = PointerManager.m_Instance.MainPointer.CurrentBrush.m_Description;
            script.Globals.Remove("pointer");
            script.Globals.Set("pointer", pointer);

            DynValue app = DynValue.NewTable(new Table(script));
            app.Table["time"] = Time.realtimeSinceStartup;
            script.Globals.Set("app", app);

            DynValue canvas = DynValue.NewTable(new Table(script));
            canvas.Table["scale"] = App.ActiveCanvas.Pose.scale;
            canvas.Table["strokes"] = SketchMemoryScript.m_Instance.StrokeCount;
            script.Globals.Set("canvas", canvas);

            return script;
        }

        public void SetupScriptWidgets(Script script)
        {
            // TODO
            Table widgets = script.Globals.Get("Widgets").Table;
        }

        private Script _GetScript(List<Script> scriptList, int scriptIndex)
        {
            if (scriptIndex > scriptList.Count - 1) return null;
            Script script = scriptList[scriptIndex];
            return script;
        }

        private DynValue _CallScript(List<Script> scriptList, int scriptIndex)
        {
            var activeScript = _GetScript(scriptList, scriptIndex);
            activeScript = SetScriptContext(activeScript);
            SetupScriptWidgets(activeScript);
            Closure activeFunction = activeScript.Globals.Get("Main").Function;
            DynValue result = activeFunction.Call();
            return result;
        }

        public List<Vector3> CallCurrentToolScript()
        {
            DynValue result = _CallScript(ToolScripts, CurrentToolScript);
            return result?.ToObject<List<Vector3>>();
        }

        public List<TrTransform> CallCurrentSymmetryScript()
        {
            DynValue result = _CallScript(SymmetryScripts, CurrentSymmetryScript);
            return result?.ToObject<List<TrTransform>>(); //.Select(tr=>tr.TransformBy(App.ActiveCanvas.Pose)).ToList();
        }

        public Vector3? CallCurrentPointerScript(Vector3 pos)
        {
            if (!PointerScriptsEnabled) return pos;
            DynValue result = _CallScript(PointerScripts, CurrentPointerScript);
            return result?.ToObject<Vector3>();
        }

        public string ChangeCurrentScript(ApiCategories cat, int increment)
        {
            string scriptName = "";
            switch (cat)
            {
                case ApiCategories.PointerScript:
                    if (PointerScripts.Count == 0) break;
                    CurrentPointerScript += increment;
                    CurrentPointerScript %= PointerScripts.Count;
                    scriptName = PointerScriptNames[CurrentPointerScript];
                    break;
                case ApiCategories.SymmetryScript:
                    if (SymmetryScripts.Count == 0) break;
                    CurrentSymmetryScript += increment;
                    CurrentSymmetryScript %= SymmetryScripts.Count;
                    scriptName = SymmetryScriptNames[CurrentSymmetryScript];
                    break;
                case ApiCategories.ToolScript:
                    if (ToolScripts.Count == 0) break;
                    CurrentToolScript += increment;
                    CurrentToolScript %= ToolScripts.Count;
                    scriptName = ToolScriptNames[CurrentToolScript];
                    break;
            }
            return scriptName;
        }

        public void EnablePointerScript(bool enable)
        {
            PointerScriptsEnabled = enable;
        }

        public string GetScriptName(ApiCategories cat, int index)
        {
            switch (cat)
            {
                case ApiCategories.PointerScript:
                    return PointerScriptNames[index];
                case ApiCategories.SymmetryScript:
                    return SymmetryScriptNames[index];
                case ApiCategories.ToolScript:
                    return ToolScriptNames[index];
                default:
                    return null;
            }
        }

        public List<string> GetScriptNames(ApiCategories cat)
        {
            switch (cat)
            {
                case ApiCategories.PointerScript:
                    return PointerScriptNames;
                case ApiCategories.SymmetryScript:
                    return SymmetryScriptNames;
                case ApiCategories.ToolScript:
                    return ToolScriptNames;
                default:
                    return null;
            }
        }
    }
}
