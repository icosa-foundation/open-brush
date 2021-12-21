using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NaughtyAttributes;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Platforms;
using CommandTerminal;
using System.Linq;

namespace TiltBrush
{

    // public class ApiFunction : KeyedCollection<string, >
    // {
    //     public string name;
    //     public Script script;
    //     public Closure closure;
    // }

    public class LuaManager : MonoBehaviour
    {
        private static LuaManager m_Instance;
        private static string DocumentsDirectory => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Open Brush");
        private static string ScriptsDirectory => Path.Combine(DocumentsDirectory, "Scripts");

        private ApiManager apiManager;

        private static readonly string LuaFileSearchPattern = "*.lua";

        private static readonly string scriptNameKey = "SCRIPT_NAME";
        private static readonly string scriptDirKey = "SCRIPT_DIR";

        private static readonly string initFuncKey = "init";
        private static readonly string updateFuncKey = "update";
        private static readonly string deltaTimeKey = "deltaTime";

        private List<Script> loadedScripts = new List<Script>();
        public IEnumerable<Script> LoadedScripts => loadedScripts;

        private Dictionary<Script, Closure> updateFunctions = new Dictionary<Script, Closure>();

        public List<string> ApiCategories = new List<string>
        {
            "PointerScript", // Modifies the pointer position on every frame
            "ToolScript",    // A scriptable tool that can create strokes based on click/drag/release
            "SymmetryScript" // Generates copies of each new stroke with different transforms
            // Scripts that modify brush settings for each new stroke (JitterScript?)
            // Scripts that modify existing strokes (RepaintScript?)
            // Scriptable Brush mesh generation (BrushScript?)
            // Same as above but applies to the current selection with maybe some logic based on index within selection
        };

        [NonSerialized] public List<Script> PointerScripts;
        [NonSerialized] public List<Script> ToolScripts;
        [NonSerialized] public List<Script> SymmetryScripts;
        [NonSerialized] public int CurrentPointerScript = 0;
        [NonSerialized] public int CurrentToolScript = 0;
        [NonSerialized] public int CurrentSymmetryScript = 0;
        // apiFunctions = new Dictionary<string, Dictionary<Script, Closure>>();

        private LuaDebugger debugger;

        public static LuaManager Instance
        {
            get { return m_Instance; }
        }

        public void SetDebugger(LuaDebugger debugger)
        {
            if (debugger == null)
            {
                this.debugger = null;
                return;
            }

            // don't allow to 'overwrite' an existing debugger
            if (this.debugger != null)
                Debug.LogError("Debugger has already been set");

            this.debugger = debugger;

            //attach all loaded scripts to the debugger
            foreach (Script script in loadedScripts)
                debugger.AttachScript(script);
        }

        void Awake()
        {
            m_Instance = this;
            PointerScripts = new List<Script>();
            ToolScripts = new List<Script>();
            SymmetryScripts = new List<Script>();
        }

        private void Start()
        {
            UserData.RegisterAssembly();
            Script.GlobalOptions.Platform = new StandardPlatformAccessor();
            InitTerminal();
            LuaCustomConverters.RegisterAll();
            LoadScripts();
        }

        private void Update()
        {
            foreach (var func in updateFunctions.Values)
            {
                func.OwnerScript.Globals[deltaTimeKey] = Time.deltaTime;
                func.Call();
            }
        }

        /// <summary>
        /// Returns the name the script is registered with
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public string GetScriptName(Script script)
        {
            if (loadedScripts.Contains(script))
                return (string)script.Globals[scriptNameKey];

            Debug.LogError("The script is not registered");
            return null;
        }

        /// <summary>
        /// Returns the path the script was loaded from
        /// </summary>
        /// <param name="script"></param>
        /// <returns></returns>
        public string GetScriptLoadPath(Script script)
        {
            return Path.Combine((string)script.Globals[scriptDirKey], (string)script.Globals[scriptNameKey] + ".lua");
        }

        /// <summary>
        /// (Re)loads all scripts found in the scripts directory
        /// </summary>
        [Button("Reload Scripts")]
        public void LoadScripts()
        {
            string scriptsDir = ScriptsDirectory;
            Directory.CreateDirectory(scriptsDir);

            Debug.Log("Loading scripts from " + scriptsDir);
            string[] files = Directory.GetFiles(scriptsDir, LuaFileSearchPattern, SearchOption.AllDirectories);

            UnloadAllScripts();

            Dictionary<string, object> globals = GetGlobals();

            foreach (string scriptPath in files)
                LoadScript(scriptPath, globals);

        }

        /// <summary>
        /// Unloads all loaded scripts
        /// </summary>
        public void UnloadAllScripts()
        {
            for (int i = loadedScripts.Count - 1; i >= 0; i--)
                UnloadScript(loadedScripts[i]);
        }

        /// <summary>
        /// Unloads the specified script
        /// </summary>
        /// <param name="script"></param>
        public void UnloadScript(Script script)
        {
            if (debugger != null)
                debugger.DetachScript(script);

            loadedScripts.Remove(script);
            updateFunctions.Remove(script);
        }

        /// <summary>
        /// Lists all loaded scripts
        /// </summary>
        /// <returns></returns>
        public string[] GetAllLoadedScripts()
        {
            string[] list = new string[loadedScripts.Count];
            for (int i = 0; i < loadedScripts.Count; i++)
                list[i] = loadedScripts[i].Globals[scriptNameKey].ToString();

            return list;
        }

        private void LoadScript(string path, Dictionary<string, object> globals)
        {
            Script script = new Script();

            // Set globals
            foreach (var global in globals)
                script.Globals[global.Key] = global.Value;

            script.Options.DebugPrint = s => Debug.Log(s);

            string scriptFilename = Path.GetFileNameWithoutExtension(path);

            // Add constants
            script.Globals[scriptNameKey] = Path.GetFileNameWithoutExtension(path);
            script.Globals[scriptDirKey] = ScriptsDirectory + Path.DirectorySeparatorChar;

            // Attach to debugger
            // if (debugger != null)
            //     debugger.AttachScript(script);

            // Execute script
            Stream fileStream = new FileStream(path, FileMode.Open);
            script.DoStream(fileStream);

            // Add update function
            Closure updateFunc = script.Globals.Get(updateFuncKey).Function;
            if (updateFunc != null)
                updateFunctions.Add(script, updateFunc);

            foreach (string category in ApiCategories)
            {
                if (!scriptFilename.StartsWith(category)) continue;
                string scriptName = scriptFilename.Substring(category.Length + 1);

                // Closure closure = script.Globals.Get(scriptName).Function;
                // DynValue funcName = script.Globals.Get(category).Table.Keys.FirstOrDefault();
                switch (category)
                {
                    case "PointerScript":
                        PointerScripts.Add(script);
                        break;
                    case "ToolScript":
                        ToolScripts.Add(script);
                        break;
                    case "SymmetryScript":
                        SymmetryScripts.Add(script);
                        break;
                }

                // Debug.Log($"{category}: {script.Globals.Get(category).Table.Keys.ToList()[0]}");
                // Debug.Log($"{category} funcName: {funcName?.String}");
                // Debug.Log($"{category} Registry: {String.Join(",", script.Registry.Keys)}");
                // Debug.Log($"{category} Globals: {String.Join(",", script.Globals.Keys)}");
                // Debug.Log($"{category} Options: {String.Join(",", script.Options)}");

            }

            //add script to loaded script list
            loadedScripts.Add(script);

            //call init function
            Closure initFunc = script.Globals.Get(initFuncKey).Function;
            if (initFunc != null)
                initFunc.Call();
            fileStream.Close();
        }

        private void InvokeEndpoint(string key, CommandArg[] args)
        {
            string cmd = args.Length == 0 ? "" : String.Join("", args.Select(x => x.String));
            apiManager.InvokeEndpoint(new KeyValuePair<string, string>(key, cmd));
        }

        static void CommandHelp(CommandArg[] args)
        {
            if (args.Length == 0)
            {
                foreach (var command in Terminal.Shell.Commands)
                {
                    Terminal.Log("{0}: {1}", command.Key.PadRight(16), command.Value.help);
                }
                return;
            }

            string command_name = args[0].String.ToUpper();

            // List subset of commands
            if (!Terminal.Shell.Commands.ContainsKey(command_name))
            {
                foreach (var command in Terminal.Shell.Commands.Where(x => x.Key.StartsWith(command_name)))
                {
                    Terminal.Log("{0}: {1}", command.Key.PadRight(16), command.Value.help);
                }
                return;
            }

            var info = Terminal.Shell.Commands[command_name];

            if (info.help == null)
            {
                Terminal.Log("{0} does not provide any help documentation.", command_name);
            }
            else if (info.hint == null)
            {
                Terminal.Log(info.help);
            }
            else
            {
                Terminal.Log("{0}\nUsage: {1}", info.help, info.hint);
            }
        }

        private void InitTerminal()
        {
            Terminal.Shell.Commands.Remove("HELP");
            Terminal.Shell.AddCommand("help", CommandHelp, 0, 1);

            if (apiManager == null)
            {
                apiManager = FindObjectOfType<ApiManager>();
            }
            if (apiManager == null)
            {
                Debug.LogError("No ApiManager Found");
                return;
            }

            foreach (var e in apiManager.endpoints.Values)
            {
                string cmd = e.Endpoint;
                int numArgs = e.parameterInfo.Length;
                var cmdInfo = ApiManager.Instance.GetCommandInfo(cmd);
                string paramInfo = cmdInfo.paramInfo == "" ? "" : $"({cmdInfo.paramInfo})";
                Terminal.Shell.AddCommand(
                    cmd,
                    x => InvokeEndpoint(cmd, x),
                    numArgs,
                    numArgs,
                    $"{cmdInfo.Description} {paramInfo}"
                );
            }

        }

        private Dictionary<string, object> GetGlobals()
        {

            Dictionary<string, object> globals = new Dictionary<string, object>();

            // Add the script category placeholders
            foreach (string category in ApiCategories)
            {
                globals.Add(category, new List<string>());
            }

            // BrushCatalog brushCatalog = FindObjectOfType<BrushCatalog>();
            // if (brushCatalog)
            //     globals.Add("brushCatalog", brushCatalog);

            // UIManager uiManager = FindObjectOfType<UIManager>();
            // if (uiManager)
            // {
            //     globals.Add("popup", new PopupProxy(uiManager.Popup));
            //     globals.Add("controls", new ControlPanelProxy(uiManager.ControlPanel));
            // }
            //
            // WorldCameraController cameraController = FindObjectOfType<WorldCameraController>();
            // if (cameraController)
            // {
            //     globals.Add("camera", new WorldCameraControllerProxy(cameraController));
            // }
            //
            // globals.Add("geo", new GeoProxy());
            //
            // UserData.RegisterProxyType<TextureProxy, Texture2D>(t => new TextureProxy(t));

            return globals;
        }

        #region Commands

        [RegisterCommand(command_name: "list_scripts", Help = "Lists all loaded lua scripts")]
        private static void Command_List_Scripts(CommandArg[] args)
        {
            if (Terminal.IssuedError) return;

            LuaManager manager = FindObjectOfType<LuaManager>();
            if (manager)
                foreach (string script in manager.GetAllLoadedScripts())
                    Terminal.Log(TerminalLogType.ShellMessage, script);
        }

        [RegisterCommand(command_name: "unload_scripts", Help = "Unloads all lua scripts")]
        private static void Command_Unload_Scripts(CommandArg[] args)
        {
            if (Terminal.IssuedError) return;

            LuaManager manager = FindObjectOfType<LuaManager>();
            if (manager)
                manager.UnloadAllScripts();
        }

        [RegisterCommand(command_name: "unload_script", Help = "Unloads a lua scripts", MaxArgCount = 1, MinArgCount = 1)]
        private static void Command_Unload_Script(CommandArg[] args)
        {
            string scriptName = args[0].String;

            if (Terminal.IssuedError) return;

            LuaManager manager = FindObjectOfType<LuaManager>();
            if (manager)
            {
                Script script = manager.loadedScripts.FirstOrDefault(s => (string)s.Globals[scriptNameKey] == scriptName);
                if (script != null)
                    manager.UnloadScript(script);
            }
        }

        #endregion


        public Script SetScriptContext(Script script)
        {
            // var pointerTr = PointerManager.m_Instance.MainPointer.transform;
            // var xfMain_CS = Coords.AsCanvas[pointerTr];
            var pointerColor = PointerManager.m_Instance.PointerColor;
            float hue, S, V;
            Color.RGBToHSV(pointerColor, out hue, out S, out V);

            // Angle the pointer according to the user-defined pointer angle.
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 pos = rAttachPoint.position;
            Quaternion rot = rAttachPoint.rotation * Quaternion.Euler(new Vector3(0, 180, 0));



            DynValue pointer = DynValue.NewTable(new Table(script));
            // pointer.Table["position"] = xfMain_CS.translation;
            // pointer.Table["rotation"] = xfMain_CS.rotation.eulerAngles;
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

            // Debug.Log($"pointer.position: {script.Globals.Get("pointer").Table.Get("position").ToObject<Vector3>()}");
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

        private void InjectGlobal(List<Script> scriptList, int scriptIndex, Vector3 pos)
        {
            var activeScript = _GetScript(scriptList, scriptIndex);
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
            // InjectGlobal(PointerScripts, CurrentPointerScript, pos);
            DynValue result = _CallScript(PointerScripts, CurrentPointerScript);
            return result?.ToObject<Vector3>();
        }

    }
}
