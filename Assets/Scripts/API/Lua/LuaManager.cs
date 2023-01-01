using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Platforms;

namespace TiltBrush
{

    public enum ScriptCoordSpace
    {
        Pointer,
        Canvas,
        Widget,
    }

    public class LuaManager : MonoBehaviour
    {
        private FileWatcher m_FileWatcher;
        private static LuaManager m_Instance;
        private static string DocumentsDirectory => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Open Brush");
        private static string ScriptsDirectory => Path.Combine(DocumentsDirectory, "Scripts");

        private ApiManager apiManager;

        private static readonly string LuaFileSearchPattern = "*.lua";

        public enum ApiCategory
        {
            PointerScript = 0, // Modifies the pointer position on every frame
            ToolScript = 1,    // A scriptable tool that can create strokes based on click/drag/release
            SymmetryScript = 2 // Generates copies of each new stroke with different transforms
            // Scripts that modify brush settings for each new stroke (JitterScript?) Maybe combine with Pointerscript
            // Scripts that modify existing strokes (RepaintScript?)
            // Scriptable Brush mesh generation (BrushScript?)
            // Same as above but applies to the current selection with maybe some logic based on index within selection
        }

        [NonSerialized] public SortedDictionary<string, Script> PointerScripts;
        [NonSerialized] public SortedDictionary<string, Script> ToolScripts;
        [NonSerialized] public SortedDictionary<string, Script> SymmetryScripts;
        [NonSerialized] public int CurrentPointerScript;
        [NonSerialized] public int CurrentToolScript;
        [NonSerialized] public int CurrentSymmetryScript;

        [NonSerialized] public bool PointerScriptsEnabled;

        public static LuaManager Instance => m_Instance;


        public struct ScriptTrTransform
        {
            public TrTransform Transform;
            public ScriptCoordSpace Space;

            public ScriptTrTransform(TrTransform transform, ScriptCoordSpace space)
            {
                Transform = transform;
                Space = space;
            }
        }

        public struct ScriptTrTransforms
        {
            public List<TrTransform> Transforms;
            public ScriptCoordSpace Space;

            public ScriptTrTransforms(List<TrTransform> transforms, ScriptCoordSpace space)
            {
                Transforms = transforms;
                Space = space;
            }
        }

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
            var currentPointerScriptName = PointerScripts.Keys.ToList()[CurrentPointerScript];
            var currentSymmetryScriptName = SymmetryScripts.Keys.ToList()[CurrentSymmetryScript];
            var currentToolScriptName = ToolScripts.Keys.ToList()[CurrentToolScript];
            LoadScript(e.FullPath);
            CurrentPointerScript = PointerScripts.Keys.ToList().IndexOf(currentPointerScriptName);
            CurrentSymmetryScript = SymmetryScripts.Keys.ToList().IndexOf(currentSymmetryScriptName);
            CurrentToolScript = ToolScripts.Keys.ToList().IndexOf(currentToolScriptName);
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
            PointerScripts = new SortedDictionary<string, Script>();
            ToolScripts = new SortedDictionary<string, Script>();
            SymmetryScripts = new SortedDictionary<string, Script>();
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

            foreach (ApiCategory category in Enum.GetValues(typeof(ApiCategory)))
            {
                var categoryName = category.ToString();
                if (!scriptFilename.StartsWith(categoryName)) continue;
                string scriptName = scriptFilename.Substring(categoryName.Length + 1);

                switch (category)
                {
                    case ApiCategory.PointerScript:
                        PointerScripts[scriptName] = script;
                        break;
                    case ApiCategory.ToolScript:
                        ToolScripts[scriptName] = script;
                        break;
                    case ApiCategory.SymmetryScript:
                        SymmetryScripts[scriptName] = script;
                        break;
                }
                InitScript(category);
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
            pointer.Table["size"] = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
            pointer.Table["size01"] = PointerManager.m_Instance.MainPointer.BrushSize01;
            pointer.Table["pressure"] = PointerManager.m_Instance.MainPointer.GetPressure();
            pointer.Table["brush"] = PointerManager.m_Instance.MainPointer.CurrentBrush?.m_Description;
            script.Globals.Remove("pointer");
            script.Globals.Set("pointer", pointer);

            DynValue app = DynValue.NewTable(new Table(script));
            app.Table["time"] = Time.realtimeSinceStartup;
            script.Globals.Set("app", app);

            DynValue canvas = DynValue.NewTable(new Table(script));
            canvas.Table["scale"] = App.ActiveCanvas.Pose.scale;
            canvas.Table["strokeCount"] = SketchMemoryScript.m_Instance.StrokeCount;
            script.Globals.Set("canvas", canvas);

            return script;
        }

        public void SetupScriptWidgets(Script script)
        {
            // TODO
            Table widgets = script.Globals.Get("Widgets").Table;
        }

        public Script GetCurrentScript(ApiCategory category)
        {
            SortedDictionary<string, Script> scriptList;
            int scriptIndex;
            switch (category)
            {
                case ApiCategory.PointerScript:
                    scriptList = PointerScripts;
                    scriptIndex = CurrentPointerScript;
                    break;
                case ApiCategory.ToolScript:
                    scriptList = ToolScripts;
                    scriptIndex = CurrentToolScript;
                    break;
                case ApiCategory.SymmetryScript:
                    scriptList = SymmetryScripts;
                    scriptIndex = CurrentSymmetryScript;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
            if (scriptIndex > scriptList.Count - 1) return null;
            string scriptName = scriptList.Keys.ToList()[scriptIndex];
            Script script = scriptList[scriptName];
            return script;
        }

        private DynValue _CallScript(ApiCategory category, string fnName)
        {
            var activeScript = GetCurrentScript(category);
            activeScript = SetScriptContext(activeScript);
            SetupScriptWidgets(activeScript);
            Closure activeFunction = activeScript.Globals.Get(fnName).Function;
            if (activeFunction != null)
            {
                DynValue result = activeFunction.Call();
                return result;
            }
            return DynValue.Nil;
        }

        public ScriptTrTransform CallCurrentPointerScript()
        {
            DynValue result = _CallScript(ApiCategory.PointerScript, "Main");
            var space = _GetSpace(ApiCategory.PointerScript);
            TrTransform tr = result.ToObject<TrTransform>();
            return new ScriptTrTransform(tr, space);
        }

        public ScriptTrTransforms CallCurrentToolScript()
        {
            DynValue result = _CallScript(ApiCategory.ToolScript, "Main");
            var space = _GetSpace(ApiCategory.ToolScript);
            return new ScriptTrTransforms(result.ToObject<List<TrTransform>>(), space);
        }

        public ScriptTrTransforms CallCurrentSymmetryScript()
        {
            DynValue result = _CallScript(ApiCategory.SymmetryScript, "Main");
            var space = _GetSpace(ApiCategory.SymmetryScript);
            return new ScriptTrTransforms(result.ToObject<List<TrTransform>>(), space);
        }

        private ScriptCoordSpace _GetSpace(ApiCategory category)
        {
            // Set defaults here
            ScriptCoordSpace space;
            switch (category)
            {
                case ApiCategory.PointerScript:
                    space = ScriptCoordSpace.Pointer;
                    break;
                case ApiCategory.ToolScript:
                    space = ScriptCoordSpace.Pointer;
                    break;
                case ApiCategory.SymmetryScript:
                    space = ScriptCoordSpace.Widget;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }

            var script = GetCurrentScript(category);
            var settings = script.Globals.Get("Settings");
            var spaceVal = settings?.Table?.Get("space");
            if (spaceVal != null)
            {
                Enum.TryParse(spaceVal.String, true, out space);
            }
            return space;
        }

        public string ChangeCurrentScript(ApiCategory cat, int increment)
        {
            string scriptName = "";
            switch (cat)
            {
                case ApiCategory.PointerScript:
                    if (PointerScripts.Count == 0) break;
                    CurrentPointerScript += increment;
                    CurrentPointerScript %= PointerScripts.Count;
                    scriptName = PointerScripts.Keys.ToList()[CurrentPointerScript];
                    break;
                case ApiCategory.SymmetryScript:
                    if (SymmetryScripts.Count == 0) break;
                    CurrentSymmetryScript += increment;
                    CurrentSymmetryScript %= SymmetryScripts.Count;
                    scriptName = SymmetryScripts.Keys.ToList()[CurrentSymmetryScript];
                    break;
                case ApiCategory.ToolScript:
                    if (ToolScripts.Count == 0) break;
                    CurrentToolScript += increment;
                    CurrentToolScript %= ToolScripts.Count;
                    scriptName = ToolScripts.Keys.ToList()[CurrentToolScript];
                    break;
            }
            InitScript(cat);
            return scriptName;
        }

        private void InitScript(ApiCategory category)
        {
            _CallScript(category, "Init");
            var script = GetCurrentScript(category);
            script.Globals["Mathf"] = typeof(UnityMathf);

            var configs = GetWidgetConfigs(category);
            foreach (var config in configs.Pairs)
            {
                if (config.Key.Type != DataType.String) continue;
                // Ensure the value is set
                GetOrSetWidgetCurrentValue(GetCurrentScript(category), config);
            }
        }

        public void EnablePointerScript(bool enable)
        {
            PointerScriptsEnabled = enable;
        }

        public string GetScriptName(ApiCategory cat, int index)
        {
            switch (cat)
            {
                case ApiCategory.PointerScript:
                    return PointerScripts.Keys.ToList()[index];
                case ApiCategory.SymmetryScript:
                    return SymmetryScripts.Keys.ToList()[index];
                case ApiCategory.ToolScript:
                    return ToolScripts.Keys.ToList()[index];
                default:
                    return null;
            }
        }

        public List<string> GetScriptNames(ApiCategory cat)
        {
            switch (cat)
            {
                case ApiCategory.PointerScript:
                    return PointerScripts.Keys.ToList();
                case ApiCategory.SymmetryScript:
                    return SymmetryScripts.Keys.ToList();
                case ApiCategory.ToolScript:
                    return ToolScripts.Keys.ToList();
                default:
                    return null;
            }
        }

        public Table GetWidgetConfigs(ApiCategory apiCategory)
        {
            var script = GetCurrentScript(apiCategory);
            var configs = script.Globals.Get("Widgets");
            return configs.IsNil() ? new Table(script) : configs.Table;
        }

        public void SetScriptParameter(ApiCategory apiCategory, string paramName, float paramValue)
        {
            var script = GetCurrentScript(apiCategory);
            script.Globals.Set("n", DynValue.NewNumber(1.2f));
            script.Globals.Set(paramName, DynValue.NewNumber(paramValue));
        }

        public float GetOrSetWidgetCurrentValue(Script script, TablePair config)
        {
            // Try and get the value from the script
            var val = script.Globals.Get(config.Key);
            // If it isn't set...
            if (val.Equals(DynValue.Nil))
            {
                // Get the default from the config entry
                val = config.Value.Table.Get("default");
                // Otherwise default to 0
                val = val.Equals(DynValue.Nil) ? DynValue.NewNumber(0) : val;
                // Set the value in the script
                script.Globals.Set(config.Key, val);
            }
            return (float)val.Number;
        }
    }
}
