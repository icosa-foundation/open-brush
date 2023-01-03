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
        private ApiManager apiManager;
        private static readonly string LuaFileSearchPattern = "*.lua";

        private static string DocumentsDirectory => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Open Brush");
        private static string ScriptsDirectory => Path.Combine(DocumentsDirectory, "Scripts");
        public List<ApiCategory> ApiCategories => Enum.GetValues(typeof(ApiCategory)).Cast<ApiCategory>().ToList();

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

        [NonSerialized] public Dictionary<ApiCategory, SortedDictionary<string, Script>> Scripts;
        [NonSerialized] public Dictionary<ApiCategory, int> ActiveScripts;

        [NonSerialized] public bool PointerScriptsEnabled;
        private List<string> m_ScriptPathsToUpdate;

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
            m_ScriptPathsToUpdate.Add(e.FullPath);
        }


        private void Start()
        {
            m_ScriptPathsToUpdate = new List<string>();
            UserData.RegisterAssembly();
            Script.GlobalOptions.Platform = new StandardPlatformAccessor();
            LuaCustomConverters.RegisterAll();
            LoadScripts();
        }

        private void Update()
        {
            // Consume the queue of scripts that the FileListener reports have changed
            foreach (var path in m_ScriptPathsToUpdate)
            {
                var catMatch = TryGetCategoryFromScriptPath(path);
                if (catMatch.HasValue)
                {
                    var category = catMatch.Value;
                    var activeScriptName = GetScriptNames(category)[ActiveScripts[category]];
                    LoadScriptFromPath(path);
                    ActiveScripts[category] = GetScriptNames(category).IndexOf(activeScriptName);
                }
            }
            m_ScriptPathsToUpdate.Clear();
        }

        public void LoadScripts()
        {
            Scripts = new Dictionary<ApiCategory, SortedDictionary<string, Script>>();
            ActiveScripts = new Dictionary<ApiCategory, int>();
            foreach (var category in ApiCategories)
            {
                Scripts[category] = new SortedDictionary<string, Script>();
                ActiveScripts[category] = 0;
            }
            Directory.CreateDirectory(ScriptsDirectory);
            string[] files = Directory.GetFiles(ScriptsDirectory, LuaFileSearchPattern, SearchOption.AllDirectories);
            foreach (string scriptPath in files)
            {
                LoadScriptFromPath(scriptPath);
            }
        }

        private ApiCategory? TryGetCategoryFromScriptPath(string path)
        {
            string scriptFilename = Path.GetFileNameWithoutExtension(path);
            foreach (ApiCategory category in ApiCategories)
            {
                var categoryName = category.ToString();
                if (scriptFilename.StartsWith(categoryName)) return category;
            }
            return null;
        }


        private void LoadScriptFromPath(string path)
        {
            Script script = new Script();
            script.Options.DebugPrint = s => Debug.Log(s);
            string scriptFilename = Path.GetFileNameWithoutExtension(path);
            Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            script.DoStream(fileStream);
            var catMatch = TryGetCategoryFromScriptPath(path);
            if (catMatch.HasValue)
            {
                var category = catMatch.Value;
                string scriptName = scriptFilename.Substring(category.ToString().Length + 1);
                Scripts[category][scriptName] = script;
                InitScript(script);
            }
            fileStream.Close();
        }

        public void SetScriptContext(Script script)
        {
            var pointerColor = PointerManager.m_Instance.PointerColor;
            float hue, S, V;
            Color.RGBToHSV(pointerColor, out hue, out S, out V);

            DynValue pointer = DynValue.NewTable(new Table(script));
            BaseTool activeTool;
            try
            {
                activeTool = SketchSurfacePanel.m_Instance.ActiveTool;
                pointer.Table["timeSincePressed"] = Time.realtimeSinceStartup - activeTool.TimeBecameActive;
                pointer.Table["timeSinceReleased"] = Time.realtimeSinceStartup - activeTool.TimeBecameInactive;
                pointer.Table["isPressed"] = activeTool.IsActive;
            }
            catch (NullReferenceException e)
            {
                pointer.Table["timeSincePressed"] = 0;
                pointer.Table["timeSinceReleased"] = 0;
                pointer.Table["isPressed"] = false;
            }
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            var attach_CS = App.Scene.ActiveCanvas.AsCanvas[rAttachPoint];
            var pos = attach_CS.translation;
            var rot = attach_CS.rotation * Quaternion.Euler(new Vector3(0, 180, 0));
            pointer.Table["position"] = pos;
            pointer.Table["rotation"] = rot.eulerAngles;
            pointer.Table["direction"] = rot * Vector3.forward;
            pointer.Table["rgb"] = new Vector3(pointerColor.r, pointerColor.g, pointerColor.b);
            pointer.Table["hsv"] = new Vector3(hue, S, V);
            pointer.Table["size"] = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
            pointer.Table["size01"] = PointerManager.m_Instance.MainPointer.BrushSize01;
            pointer.Table["pressure"] = PointerManager.m_Instance.MainPointer.GetPressure();
            pointer.Table["brush"] = PointerManager.m_Instance.MainPointer.CurrentBrush?.m_Description;
            script.Globals.Set("pointer", pointer);

            DynValue app = DynValue.NewTable(new Table(script));
            app.Table["time"] = Time.realtimeSinceStartup;
            script.Globals.Set("app", app);

            DynValue canvas = DynValue.NewTable(new Table(script));
            canvas.Table["scale"] = App.ActiveCanvas.Pose.scale;
            canvas.Table["strokeCount"] = SketchMemoryScript.m_Instance.StrokeCount;
            script.Globals.Set("canvas", canvas);
        }

        public Script GetActiveScript(ApiCategory category)
        {
            string scriptName = GetScriptNames(category)[ActiveScripts[category]];
            return Scripts[category][scriptName];
        }

        private DynValue _CallScript(Script script, string fnName)
        {
            SetScriptContext(script);
            Closure activeFunction = script.Globals.Get(fnName).Function;
            if (activeFunction != null)
            {
                DynValue result = activeFunction.Call();
                return result;
            }
            return DynValue.Nil;
        }

        public ScriptTrTransform CallActivePointerScript()
        {
            var script = GetActiveScript(ApiCategory.PointerScript);
            DynValue result = _CallScript(script, "Main");
            var space = _GetSpaceForActiveScript(ApiCategory.PointerScript);
            TrTransform tr = result.ToObject<TrTransform>();
            return new ScriptTrTransform(tr, space);
        }

        public ScriptTrTransforms CallActiveToolScript()
        {
            var script = GetActiveScript(ApiCategory.ToolScript);
            DynValue result = _CallScript(script, "Main");
            var space = _GetSpaceForActiveScript(ApiCategory.ToolScript);
            return new ScriptTrTransforms(result.ToObject<List<TrTransform>>(), space);
        }

        public ScriptTrTransforms CallActiveSymmetryScript()
        {
            var script = GetActiveScript(ApiCategory.SymmetryScript);
            DynValue result = _CallScript(script, "Main");
            var space = _GetSpaceForActiveScript(ApiCategory.SymmetryScript);
            return new ScriptTrTransforms(result.ToObject<List<TrTransform>>(), space);
        }

        private ScriptCoordSpace _GetSpaceForActiveScript(ApiCategory category)
        {
            // Set defaults here
            ScriptCoordSpace space;
            if (category == ApiCategory.SymmetryScript)
            {
                space = ScriptCoordSpace.Widget;
            }
            else
            {
                space = ScriptCoordSpace.Pointer;
            }

            // See if the defaults are overridden
            var script = GetActiveScript(category);
            var settings = script.Globals.Get("Settings");
            var spaceVal = settings?.Table?.Get("space");
            if (spaceVal != null)
            {
                Enum.TryParse(spaceVal.String, true, out space);
            }
            return space;
        }

        public void ChangeCurrentScript(ApiCategory category, int increment)
        {
            if (Scripts[category].Count == 0) return;
            ActiveScripts[category] += increment;
            ActiveScripts[category] %= Scripts[category].Count;
            var script = GetActiveScript(category);
            InitScript(script);
        }

        private void InitScript(Script script)
        {
            script.Globals["Mathf"] = typeof(UnityMathf);
            // Redirect "print"
            script.Options.DebugPrint = s => { Debug.Log(s); };

            // var libraries = Resources.LoadAll<TextAsset>("LuaLibraries");
            // foreach (var library in libraries)
            // {
            //     Debug.Log($"Loaded lua library {library.name}");
            //     script.DoString(library.text);
            // }

            // UserData.RegisterType<Vector3>();
            // UserData.RegisterType<Vector2>();
            // UserData.RegisterType<Vector4>();
            // UserData.RegisterType<Mathf>();
            // UserData.RegisterType<Quaternion>();

            var configs = GetWidgetConfigs(script);
            foreach (var config in configs.Pairs)
            {
                if (config.Key.Type != DataType.String) continue;
                // Ensure the value is set
                GetOrSetWidgetCurrentValue(script, config);
            }
            _CallScript(script, "Init");
        }

        public void EnablePointerScript(bool enable)
        {
            PointerScriptsEnabled = enable;
        }

        public List<string> GetScriptNames(ApiCategory category)
        {
            return Scripts[category].Keys.ToList();
        }

        public Table GetWidgetConfigs(Script script)
        {
            var configs = script.Globals.Get("Widgets");
            return configs.IsNil() ? new Table(script) : configs.Table;
        }

        public void SetScriptParameterForActiveScript(ApiCategory category, string paramName, float paramValue)
        {
            var script = GetActiveScript(category);
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

        public void ApplyPointerScript(Quaternion pointerRot, ref Vector3 pos_GS, ref Quaternion rot_GS)
        {
            var scriptTransformOutput = CallActivePointerScript();

            switch (scriptTransformOutput.Space)
            {
                case ScriptCoordSpace.Canvas:
                    var tr_CS = TrTransform.TR(
                        scriptTransformOutput.Transform.translation,
                        scriptTransformOutput.Transform.rotation
                    );
                    var tr_GS = App.Scene.Pose * tr_CS;
                    pos_GS = tr_GS.translation;
                    rot_GS = tr_GS.rotation;
                    break;
                case ScriptCoordSpace.Pointer:
                    var oldPos = pos_GS;
                    pos_GS = scriptTransformOutput.Transform.translation;
                    pos_GS = pointerRot * pos_GS;
                    pos_GS += oldPos;
                    rot_GS *= scriptTransformOutput.Transform.rotation;
                    break;
                case ScriptCoordSpace.Widget:
                    var widget = PointerManager.m_Instance.SymmetryWidget;
                    rot_GS = widget.rotation;
                    pos_GS = rot_GS * pos_GS;
                    pos_GS += widget.position;
                    rot_GS *= scriptTransformOutput.Transform.rotation;
                    break;
            }
        }
    }
}
