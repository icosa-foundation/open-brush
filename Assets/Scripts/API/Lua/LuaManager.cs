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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using MoonSharp.Interpreter.Platforms;
using UnityEngine.Networking;

#if UNITY_EDITOR
using System.Reflection;
using MoonSharp.Interpreter.Interop;
#endif

namespace TiltBrush
{
    public enum ScriptCoordSpace
    {
        Default,
        Pointer,
        Canvas,
    }

    public enum LuaApiCategory
    {
        PointerScript = 0,    // Modifies the pointer position on every frame
        ToolScript = 1,       // A scriptable tool that can create strokes based on click/drag/release
        SymmetryScript = 2,   // Generates copies of each new stroke with different transforms
        BackgroundScript = 3, // A general script that is called every frame
        // Scripts that modify brush settings for each new stroke (JitterScript?) Maybe combine with Pointerscript
        // Scripts that modify existing strokes (RepaintScript?)
        // Scriptable Brush mesh generation (BrushScript?)
        // Same as above but applies to the current selection with maybe some logic based on index within selection
    }

    public static class LuaNames
    {
        // By using properties instead of constants, it makes it easier to track down usages in the editor

        // Special Tables

        public static string Parameters => "Parameters";
        public static string Settings => "Settings";
        public static string Colors => "Colors";
        public static string Brushes => "Brushes";

        // Special Methods

        public static string Main => "Main";
        public static string Start => "Start";
        public static string End => "End";
        public static string ScriptNameString => "_ScriptName";
        public static string IsExampleScriptBool => "_IsExampleScript";
        public static string ToolPreviewType => "previewType";
        public static string ToolPreviewAxis => "previewAxis";

        // Injected Toolscript properties

        public static string ToolScriptStartPoint => "startPoint";
        public static string ToolScriptEndPoint => "endPoint";
        public static string ToolScriptVector => "vector";
        public static string ToolScriptRotation => "rotation";
    }

    public struct ScriptWidgetConfig
    {
        public string label;
        public string type;
        public float min;
        public float max;
        public float defaultVal;
    }

    public class LuaManager : MonoBehaviour
    {
        private FileWatcher m_FileWatcher;
        private static LuaManager m_Instance;
        private ApiManager apiManager;
        private static readonly string LuaFileSearchPattern = "*.lua";
        private string m_UserPluginsPath;

        public string UserPluginsPath() { return m_UserPluginsPath; }

        public List<LuaApiCategory> ApiCategories => Enum.GetValues(typeof(LuaApiCategory)).Cast<LuaApiCategory>().ToList();

        public List<string> BackgroundScriptsToRun;
        public int ScriptedWaveformSampleRate = 16000;

        [NonSerialized] public Dictionary<LuaApiCategory, SortedDictionary<string, Script>> Scripts;
        [NonSerialized] public Dictionary<LuaApiCategory, int> ActiveScripts;
        [NonSerialized] public bool PointerScriptsEnabled;
        [NonSerialized] public bool VisualizerScriptingEnabled;
        [NonSerialized] public bool BackgroundScriptsEnabled;
        private List<string> m_ScriptPathsToUpdate;
        private Dictionary<string, Script> m_ActiveBackgroundScripts;
        private Dictionary<string, Dictionary<string, ScriptWidgetConfig>> m_WidgetConfigs;

        private TransformBuffers m_TransformBuffers;
        private bool m_TriggerWasPressed;
        private static Dictionary<(Script OwnerScript, int ReferenceID), LuaTimer> m_Timers;

        public static LuaManager Instance => m_Instance;

        private LinkedList<LuaWebRequest> m_WebRequests;

        public string LuaModulesPath => Path.Join(UserPluginsPath(), "LuaModules");

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

        void Awake()
        {
            m_Instance = this;
            m_UserPluginsPath = Path.Combine(App.UserPath(), "Plugins");
            if (!Directory.Exists(m_UserPluginsPath))
            {
                Directory.CreateDirectory(m_UserPluginsPath);
            }
        }

        void Start()
        {
            Init();
        }

        private void OnScriptsDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            m_ScriptPathsToUpdate.Add(e.FullPath);
        }

        public void Init()
        {
            m_WebRequests = new LinkedList<LuaWebRequest>();
            m_TransformBuffers = new TransformBuffers(128);
            m_ScriptPathsToUpdate = new List<string>();
            m_ActiveBackgroundScripts = new Dictionary<string, Script>();
            m_Timers = new Dictionary<(Script OwnerScript, int ReferenceID), LuaTimer>();
            m_WidgetConfigs = new Dictionary<string, Dictionary<string, ScriptWidgetConfig>>();
            LuaCustomConverters.RegisterAll();
            UserData.RegisterAssembly();
            Script.GlobalOptions.Platform = new StandardPlatformAccessor();
            Scripts = new Dictionary<LuaApiCategory, SortedDictionary<string, Script>>();
            ActiveScripts = new Dictionary<LuaApiCategory, int>();
            foreach (var category in ApiCategories)
            {
                Scripts[category] = new SortedDictionary<string, Script>();
                ActiveScripts[category] = 0;
            }

            if (!Directory.Exists(LuaModulesPath))
            {
                Directory.CreateDirectory(LuaModulesPath);
            }

            CopyLuaModules();

            // Allow includes from Scripts/LuaModules
            Script.DefaultOptions.ScriptLoader = new OpenBrushScriptLoader();
            ((ScriptLoaderBase)Script.DefaultOptions.ScriptLoader).ModulePaths = new[]
            {
                Path.Join(LuaModulesPath, "?.lua")
            };

            LoadExampleScripts();
            LoadUserScripts();

            if (Directory.Exists(UserPluginsPath()))
            {
                m_FileWatcher = new FileWatcher(UserPluginsPath(), "*.lua");
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnScriptsDirectoryChanged;
                m_FileWatcher.FileCreated += OnScriptsDirectoryChanged;
                // m_FileWatcher.FileDeleted += OnScriptsDirectoryChanged; TODO
                m_FileWatcher.EnableRaisingEvents = true;
            }

            var panel = (ScriptsPanel)PanelManager.m_Instance.GetPanelByType(BasePanel.PanelType.Scripts);
            panel.InitScriptUiNav();
            ConfigureScriptButton(LuaApiCategory.PointerScript);
            ConfigureScriptButton(LuaApiCategory.SymmetryScript);
            ConfigureScriptButton(LuaApiCategory.ToolScript);
        }

        public void CopyLuaModules()
        {
            // Copy built-in Lua Libraries to User's LuaModules directory
            var libraries = Resources.LoadAll<TextAsset>("LuaModules");
            foreach (var library in libraries)
            {
                var newFilename = Path.Join(LuaModulesPath, $"{library.name}.lua");
                if (!File.Exists(newFilename) || library.name == "__autocomplete") // Always overwrite autocomplete
                {
                    FileUtils.WriteTextFromResources($"LuaModules/{library.name}", newFilename);
                }
            }
        }

        public void SetBrushBufferSize(int size)
        {
            if (m_TransformBuffers.BrushBufferSize != size) ResizeBrushBuffer(size);
        }

        public void SetWandBufferSize(int size)
        {
            if (m_TransformBuffers.WandBufferSize != size) ResizeWandBuffer(size);
        }

        public void SetHeadBufferSize(int size)
        {
            if (m_TransformBuffers.HeadBufferSize != size) ResizeHeadBuffer(size);
        }

        public void ResizeBrushBuffer(int size)
        {
            m_TransformBuffers.BrushBufferSize = size;
        }

        public void ResizeWandBuffer(int size)
        {
            m_TransformBuffers.WandBufferSize = size;
        }

        public void ResizeHeadBuffer(int size)
        {
            m_TransformBuffers.HeadBufferSize = size;
        }

        private void Update()
        {
            bool sceneIsReady = App.CurrentState == App.AppState.Standard;
            if (BackgroundScriptsToRun.Count > 0 && sceneIsReady)
            {
                // Pop one of the initial scripts and run it
                var panel = (ScriptsPanel)PanelManager.m_Instance.GetPanelByType(BasePanel.PanelType.Scripts);
                panel.BackgroundScriptsButton.IsToggledOn = true;
                BackgroundScriptsEnabled = true;
                var scriptName = BackgroundScriptsToRun[0];
                ActivateBackgroundScript(scriptName, true);
                BackgroundScriptsToRun.RemoveAt(0);
            }

            // Operate on a copy in case we are modified while iterating
            var scriptsToProcess = m_ScriptPathsToUpdate.ToList();
            m_ScriptPathsToUpdate.Clear();
            // Consume the queue of scripts that the FileListener reports have changed
            foreach (var path in scriptsToProcess)
            {
                var scriptFilename = Path.GetFileNameWithoutExtension(Path.GetFileName(path));
                var catMatch = TryGetCategoryFromScriptName(scriptFilename);
                if (catMatch.HasValue)
                {
                    var category = catMatch.Value;
                    var scriptName = scriptFilename.Substring(category.ToString().Length + 1);
                    LoadScriptFromPath(path);
                    if (catMatch == LuaApiCategory.BackgroundScript)
                    {
                        if (m_ActiveBackgroundScripts.ContainsKey(scriptName))
                        {
                            InitScript(m_ActiveBackgroundScripts[scriptName]);
                        }
                    }
                    else
                    {
                        var activeScriptName = GetActiveScriptName(category);
                        ActiveScripts[category] = GetScriptNames(category).IndexOf(activeScriptName);
                        if (activeScriptName == scriptName)
                        {
                            InitScript(GetActiveScript(category));
                        }
                    }
                }
            }

            var toRemove = new List<(Script OwnerScript, int ReferenceID)>();
            foreach (var kv in m_Timers)
            {
                var timer = kv.Value;
                if ((Time.time - timer.m_TimeLastRun >= timer.m_Interval) &&
                    (Time.time - timer.m_TimeAdded >= timer.m_Delay))
                {
                    timer.m_TimeLastRun = Time.time;
                    timer.m_Fn.Call();
                    timer.m_CallCount++;
                    if (timer.m_Repeats != -1 && timer.m_CallCount >= timer.m_Repeats)
                    {
                        toRemove.Add(kv.Key);
                    }
                }
            }
            foreach (var item in toRemove)
            {
                m_Timers.Remove(item);
            }

            // Consume the queue of WebRequests that are still active
            for (var node = m_WebRequests.First; node != null;)
            {
                var nextNode = node.Next;
                var req = node.Value;
                if (!req.script.Globals.Get("_scriptHasEnded").Boolean)
                {
                    if (!req.request.isDone)
                    {
                        node = nextNode;
                        continue; // The only case where we don't remove the item from the queue
                    }
                    switch (req.request.result)
                    {
                        case UnityWebRequest.Result.ConnectionError:
                        case UnityWebRequest.Result.DataProcessingError:
                        case UnityWebRequest.Result.ProtocolError:
                            try { req.onError?.Call(req.request.error, req.context); }
                            catch (InterpreterException e) { LogLuaInterpreterError(req.script, req.onError.ToString(), e); }
                            break;
                        case UnityWebRequest.Result.Success:
                            try { req.onSuccess?.Call(req.request.downloadHandler.text, req.context); }
                            catch (InterpreterException e) { LogLuaInterpreterError(req.script, req.onSuccess.ToString(), e); }
                            break;
                    }
                }
                m_WebRequests.Remove(node);
                node = nextNode;
            }

            if (BackgroundScriptsEnabled) CallActiveBackgroundScripts(LuaNames.Main);
        }

        public void LoadUserScripts()
        {
            string[] files = Directory.GetFiles(UserPluginsPath(), LuaFileSearchPattern, SearchOption.AllDirectories);
            foreach (string scriptPath in files)
            {
                LoadScriptFromPath(scriptPath);
            }
        }

        private void LoadExampleScripts()
        {
            var exampleScripts = Resources.LoadAll("LuaScriptExamples", typeof(TextAsset));
            foreach (var asset in exampleScripts)
            {
                var luaFile = (TextAsset)asset;
                LoadScriptFromString(luaFile.name, luaFile.text, isExampleScript: true);
            }
        }

        private LuaApiCategory? TryGetCategoryFromScriptName(string scriptFilename)
        {
            foreach (LuaApiCategory category in ApiCategories)
            {
                var categoryName = category.ToString();
                if (scriptFilename.StartsWith(categoryName)) return category;
            }
            return null;
        }

        public void LogLuaErrorRaisedByScript(string msg)
        {
            Debug.LogError($"Lua error: {msg}");
            ControllerConsoleScript.m_Instance.AddNewLine(msg, true, true);
        }

        public void LogLuaInterpreterError(Script script, string fnName, InterpreterException e)
        {
            string msg = e.DecoratedMessage ?? e.Message;
            _FormatAndLogLuaError(script, fnName, e, msg);
        }

        public void LogLuaCastError(Script script, string fnName, InvalidCastException e)
        {
            _FormatAndLogLuaError(script, fnName, e, e.Message);
        }

        public void LogGenericLuaError(Script script, string fnName, Exception e)
        {
            if (e is ScriptRuntimeException)
            {
                LogLuaInterpreterError(script, fnName, e as ScriptRuntimeException);
            }
            else if (e is InvalidCastException)
            {
                LogLuaCastError(script, fnName, e as InvalidCastException);
            }
        }

        public static string ReformatLuaError(Script script, string fnName, string msg)
        {
            // Make the message more user friendly
            msg = msg.Replace("chunk_1:", "on line: ");
            // Replace the (line, char range) with just the line number itself
            msg = Regex.Replace(msg, @"(.+)\((\d+),.+\)(.+)", @"$1$2$3");
            return $"Error in {script.Globals.Get(LuaNames.ScriptNameString).String}.{fnName} {msg}";
        }

        private static void _FormatAndLogLuaError(Script script, string fnName, Exception e, string msg)
        {
            string errorMsg = ReformatLuaError(script, fnName, msg);
            LogLuaError(errorMsg, e);
        }

        public static void LogLuaError(string errorMsg, Exception e)
        {
            ControllerConsoleScript.m_Instance.AddNewLine(errorMsg, true, true);
            Debug.LogError($"{errorMsg}\n\n{e.StackTrace}\n\n");
        }

        public static void LogLuaError(Exception e)
        {
            LogLuaError(e.Message, e);
        }

        public static void LogLuaMessage(string s)
        {
            ControllerConsoleScript.m_Instance.AddNewLine(s, false, true);
            Debug.Log(s);
        }

        private string LoadScriptFromPath(string path)
        {
            Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            string contents;
            using (var sr = new StreamReader(fileStream)) contents = sr.ReadToEnd();
            fileStream.Close();
            string filename = Path.GetFileName(path);
            if (filename.StartsWith("__")) return null;
            return LoadScriptFromString(Path.GetFileNameWithoutExtension(filename), contents);
        }

        private string LoadScriptFromString(string scriptFilename, string contents, bool isExampleScript = false)
        {
            Script script = new Script();
            string scriptName = null;
            try
            {
                script.DoString(contents);
            }
            catch (ScriptRuntimeException e)
            {
                LogLuaInterpreterError(script, $"(Runtime Error loading: {scriptFilename})", e);
                return null;
            }
            catch (SyntaxErrorException e)
            {
                LogLuaInterpreterError(script, $"(Syntax Error loading: {scriptFilename})", e);
                return null;
            }
            var catMatch = TryGetCategoryFromScriptName(scriptFilename);
            if (catMatch.HasValue)
            {
                var category = catMatch.Value;
                scriptName = scriptFilename.Substring(category.ToString().Length + 1);

                script.Globals[LuaNames.ScriptNameString] = scriptName;
                script.Globals[LuaNames.IsExampleScriptBool] = isExampleScript;

                Scripts[category][scriptName] = script;
                if (m_ActiveBackgroundScripts.ContainsKey(scriptName))
                {
                    m_ActiveBackgroundScripts[scriptName] = script;
                }
                try
                {
                    InitScriptOnce(script);
                    InitWidgetConfigs(script, scriptName);
                }
                catch (ScriptRuntimeException e)
                {
                    LogLuaInterpreterError(script, $"(ScriptRuntimeException InitScriptOnce: {scriptFilename})", e);
                    return null;
                }
            }
            return scriptName;
        }

        public Vector3 GetPastBrushPos(int back)
        {
            return m_TransformBuffers.PastBrushTr(back).translation;
        }

        public Quaternion GetPastBrushRot(int back)
        {
            return m_TransformBuffers.PastBrushTr(back).rotation;
        }

        public Vector3 GetPastWandPos(int back)
        {
            return m_TransformBuffers.PastWandTr(back).translation;
        }

        public Quaternion GetPastWandRot(int back)
        {
            return m_TransformBuffers.PastWandTr(back).rotation;
        }

        public Vector3 GetPastHeadPos(int back)
        {
            return m_TransformBuffers.PastHeadTr(back).translation;
        }

        public Quaternion GetPastHeadRot(int back)
        {
            return m_TransformBuffers.PastHeadTr(back).rotation;
        }

        // Skips most error checking. Use for repeated redefinition.
        public void SetApiProperty(Script script, string cmd, object action)
        {
            var parts = cmd.Split(".");
            var tbl = script.Globals.Get(parts[0]);
            if (Equals(tbl, DynValue.Nil))
            {
                script.Globals.Set(parts[0], DynValue.NewTable(new Table(script)));
                tbl = script.Globals.Get(parts[0]);
            }
            tbl.Table[parts[1]] = action;
        }

        public void RegisterApiClass(Script script, string fnName, Type t, string prefix = null)
        {
            var target = script.Globals;
            if (prefix != null)
            {
                var container = script.Globals.Get(prefix).Table;
                if (container == null)
                {
                    script.Globals.Set(prefix, DynValue.NewTable(new Table(script)));
                    container = script.Globals.Get(prefix).Table;
                }
                target = container;
            }
            target[fnName] = t;

#if UNITY_EDITOR
            LuaDocsRegistration.RegisterForDocs(t);
#endif
        }

        public string GetActiveScriptName(LuaApiCategory category)
        {
            return GetScriptNames(category)[ActiveScripts[category]];
        }

        public Script GetActiveScript(LuaApiCategory category)
        {
            return Scripts[category][GetActiveScriptName(category)];
        }

        private DynValue _CallScript(Script script, string fnName)
        {
            Closure activeFunction = script.Globals.Get(fnName).Function;
            DynValue result = DynValue.Nil;
            if (activeFunction != null)
            {
                try
                {
                    result = activeFunction.Call();
                }
                catch (InterpreterException e)
                {
                    LogLuaInterpreterError(script, fnName, e);
                }
                catch (InvalidCastException e)
                {
                    LogLuaCastError(script, fnName, e);
                }
            }
            return result;
        }

        public void EndActiveScript(LuaApiCategory category)
        {
            var script = GetActiveScript(category);
            EndScript(script);
        }

        public void EndScript(Script script)
        {
            if (script.Globals.Get("_scriptHasBeenInitialized").Boolean)
            {
                _CallScript(script, LuaNames.End);
                ApiMethods.ForcePaintingOn(false);
                ApiMethods.ForcePaintingOff(false);
                UnsetAllTimers(script);
                script.Globals.Set("_scriptHasBeenInitialized", DynValue.False);
                script.Globals.Set("_scriptHasEnded", DynValue.True);
            }
        }

        private void CallActiveBackgroundScripts(string fnName)
        {
            foreach (var script in m_ActiveBackgroundScripts.Values)
            {
                _CallScript(script, fnName);
            }
        }

        private bool CallActivePointerScript(string fnName, out ScriptTrTransform result)
        {
            var script = GetActiveScript(LuaApiCategory.PointerScript);
            DynValue returnedTr = _CallScript(script, fnName);
            var space = _GetSpaceForActiveScript(LuaApiCategory.PointerScript);
            try
            {
                if (!returnedTr.Equals(DynValue.Nil))
                {
                    result = new ScriptTrTransform(returnedTr.ToObject<TrTransform>(), space);
                    return true;
                }
            }
            catch (InvalidCastException e)
            {
                LogLuaCastError(script, fnName, e);
            }
            result = default;
            return false;
        }

        public PathListApiWrapper CallActiveToolScript(string fnName)
        {
            var script = GetActiveScript(LuaApiCategory.ToolScript);
            DynValue result = _CallScript(script, fnName);
            PathListApiWrapper pathListWrapper = null;

            if (result.Table != null)
            {
                // It's a lua array
                var trList = result.Table.Values.Select(v => v.ToObject<TrTransform>()).ToList();
                var path = new PathApiWrapper(trList);
                pathListWrapper = new PathListApiWrapper(path);
            }
            else
            {
                // It's either a multipath or path
                try
                {
                    // Try to cast to multipath first
                    pathListWrapper = result.ToObject<PathListApiWrapper>();
                }
                catch (Exception _)
                {
                    try
                    {
                        // If that fails, try to cast to path
                        var pathWrapper = result.ToObject<PathApiWrapper>();
                        // and wrap it as a multipath
                        pathListWrapper = new PathListApiWrapper(pathWrapper);
                    }
                    catch (Exception e)
                    {
                        // If neither then log the error
                        LogGenericLuaError(script, fnName, e);
                    }
                }
            }
            if (pathListWrapper != null)
            {
                pathListWrapper._Space = _GetSpaceForActiveScript(LuaApiCategory.ToolScript);
            }
            return pathListWrapper;
        }

        public IPathApiWrapper CallActiveSymmetryScript(string fnName)
        {
            var script = GetActiveScript(LuaApiCategory.SymmetryScript);
            var pathWrapper = new PathApiWrapper();
            try
            {
                DynValue result = _CallScript(script, fnName);
                if (result.Table != null)
                {
                    // It's a lua array
                    var trList = result.Table.Values.Select(v => v.ToObject<TrTransform>()).ToList();
                    pathWrapper = new PathApiWrapper(trList);
                }
                else
                {
                    // It's a PathApiWrapper instance
                    pathWrapper = result.ToObject<PathApiWrapper>();
                }

                if (pathWrapper != null)
                {
                    pathWrapper._Space = _GetSpaceForActiveScript(LuaApiCategory.SymmetryScript);
                }
            }
            catch (InvalidCastException e)
            {
                LogLuaCastError(script, fnName, e);
            }
            return pathWrapper;
        }

        public DynValue GetSettingForActiveScript(LuaApiCategory category, string key)
        {
            var script = GetActiveScript(category);
            var settings = script.Globals.Get(LuaNames.Settings);
            return settings?.Table?.Get(key);
        }

        private ScriptCoordSpace _GetSpaceForActiveScript(LuaApiCategory category)
        {
            ScriptCoordSpace space = ScriptCoordSpace.Default;
            var spaceVal = GetSettingForActiveScript(category, "space");
            if (spaceVal != null)
            {
                Enum.TryParse(spaceVal.String, true, out space);
            }
            return space;
        }

        public void SetActiveScriptByName(LuaApiCategory category, string scriptName)
        {
            int index = GetScriptNames(category).IndexOf(scriptName);
            if (index != -1)
            {
                _EndPreviousScript(category);
                _SetActiveScript(category, index);
            }
        }

        public void ChangeCurrentScript(LuaApiCategory category, int increment)
        {
            if (Scripts[category].Count == 0) return;
            _EndPreviousScript(category);
            int index = (int)Mathf.Repeat(ActiveScripts[category] + increment, Scripts[category].Count);
            _SetActiveScript(category, index);
        }

        private void _EndPreviousScript(LuaApiCategory category)
        {
            var previousScript = GetActiveScript(category);
            EndScript(previousScript);
        }

        private void _SetActiveScript(LuaApiCategory category, int index)
        {
            ActiveScripts[category] = index;
            var script = GetActiveScript(category);
            if (IsCategoryActive(category)) InitScript(script);
            ConfigureScriptButton(category);
        }

        public bool IsAnyCategoryActive()
        {
            bool result = false;
            foreach (var category in Enum.GetValues(typeof(LuaApiCategory)).Cast<LuaApiCategory>())
            {
                result |= IsCategoryActive(category);
            }
            return result;
        }

        public bool IsCategoryActive(LuaApiCategory category)
        {
            // We have booleans for Background and Pointer scripts
            // For symmetry and tool scripts, the UI buttons bypass LuaManager to activate the mode
            // So we have to query their status more indirectly.
            switch (category)
            {
                case LuaApiCategory.BackgroundScript:
                    return BackgroundScriptsEnabled;
                case LuaApiCategory.PointerScript:
                    return PointerScriptsEnabled;
                case LuaApiCategory.SymmetryScript:
                    return PointerManager.m_Instance.CurrentSymmetryMode == PointerManager.SymmetryMode.ScriptedSymmetryMode;
                case LuaApiCategory.ToolScript:
                    return SketchSurfacePanel.m_Instance.GetCurrentToolType() == BaseTool.ToolType.ScriptedTool;
            }
            return false;
        }

        public void ConfigureScriptButton(LuaApiCategory category)
        {
            var script = GetActiveScript(category);
            var scriptName = script.Globals.Get(LuaNames.ScriptNameString).String;
            var panel = (ScriptsPanel)PanelManager.m_Instance.GetPanelByType(BasePanel.PanelType.Scripts);
            string description = script.Globals.Get(LuaNames.Settings)?.Table?.Get("description")?.String;
            panel.ConfigureScriptButton(category, scriptName, description);
        }

        public void InitScriptOnce(Script script)
        {
            script.Options.DebugPrint = LogLuaMessage;

            // Ensure we don't trigger doc generation
            // (mainly if domain reload is disabled in the editor)
            LuaDocsRegistration.ApiDocClasses = null;

            RegisterApiClasses(script);
        }

        public void InitScript(Script script)
        {
            if (!script.Globals.Get("_scriptHasBeenInitialized").Boolean)
            {
                var scriptName = script.Globals.Get(LuaNames.ScriptNameString).String;
                var configs = GetWidgetConfigs(scriptName);
                foreach (var config in configs)
                {
                    // Ensure the value is set
                    GetOrSetWidgetCurrentValue(script, config.Key, config.Value);
                }
                script.Globals.Set("_scriptHasBeenInitialized", DynValue.True); // Used by EndScript
                script.Globals.Set("_scriptHasEnded", DynValue.False);
                _CallScript(script, LuaNames.Start);
            }
        }

        public void RegisterApiClasses(Script script)
        {
            RegisterApiClass(script, "App", typeof(AppApiWrapper));
            RegisterApiClass(script, "Brush", typeof(BrushApiWrapper));
            RegisterApiClass(script, "CameraPath", typeof(CameraPathApiWrapper));
            RegisterApiClass(script, "CameraPathList", typeof(CameraPathListApiWrapper));
            RegisterApiClass(script, "Color", typeof(ColorApiWrapper));
            RegisterApiClass(script, "Environment", typeof(EnvironmentApiWrapper));
            RegisterApiClass(script, "EnvironmentList", typeof(EnvironmentListApiWrapper));
            RegisterApiClass(script, "Easing", typeof(EasingApiWrapper));
            RegisterApiClass(script, "Group", typeof(GroupApiWrapper));
            RegisterApiClass(script, "GroupList", typeof(GroupListApiWrapper));
            RegisterApiClass(script, "Guide", typeof(GuideApiWrapper));
            RegisterApiClass(script, "GuideList", typeof(GuideListApiWrapper));
            RegisterApiClass(script, "Headset", typeof(HeadsetApiWrapper));
            RegisterApiClass(script, "Image", typeof(ImageApiWrapper));
            RegisterApiClass(script, "ImageList", typeof(ImageListApiWrapper));
            RegisterApiClass(script, "Layer", typeof(LayerApiWrapper));
            RegisterApiClass(script, "LayerList", typeof(LayerListApiWrapper));
            RegisterApiClass(script, "Math", typeof(MathApiWrapper));
            RegisterApiClass(script, "Model", typeof(ModelApiWrapper));
            RegisterApiClass(script, "ModelList", typeof(ModelListApiWrapper));
            RegisterApiClass(script, "Path", typeof(PathApiWrapper));
            RegisterApiClass(script, "PathList", typeof(PathListApiWrapper));
            RegisterApiClass(script, "Path2d", typeof(Path2dApiWrapper));
            RegisterApiClass(script, "Pointer", typeof(PointerApiWrapper));
            RegisterApiClass(script, "Random", typeof(RandomApiWrapper));
            RegisterApiClass(script, "Rotation", typeof(RotationApiWrapper));
            RegisterApiClass(script, "Selection", typeof(SelectionApiWrapper));
            RegisterApiClass(script, "Sketch", typeof(SketchApiWrapper));
            RegisterApiClass(script, "Spectator", typeof(SpectatorApiWrapper));
            RegisterApiClass(script, "Stroke", typeof(StrokeApiWrapper));
            RegisterApiClass(script, "StrokeList", typeof(StrokeListApiWrapper));
            RegisterApiClass(script, "Svg", typeof(SvgApiWrapper));
            RegisterApiClass(script, "Symmetry", typeof(SymmetryApiWrapper));
            RegisterApiClass(script, "SymmetrySettings", typeof(SymmetrySettingsApiWrapper));
            RegisterApiClass(script, "Timer", typeof(TimerApiWrapper));
            RegisterApiClass(script, "Transform", typeof(TransformApiWrapper));
            // RegisterApiClass(script, "Turtle", typeof(TurtleApiWrapper));
            RegisterApiClass(script, "User", typeof(UserApiWrapper));
            RegisterApiClass(script, "Vector2", typeof(Vector2ApiWrapper));
            RegisterApiClass(script, "Vector3", typeof(Vector3ApiWrapper));
            RegisterApiClass(script, "Video", typeof(VideoApiWrapper));
            RegisterApiClass(script, "VideoList", typeof(VideoListApiWrapper));
            RegisterApiClass(script, "Visualizer", typeof(VisualizerApiWrapper));
            RegisterApiClass(script, "Wand", typeof(WandApiWrapper));
            RegisterApiClass(script, "Waveform", typeof(WaveformApiWrapper));
            RegisterApiClass(script, "WebRequest", typeof(WebRequestApiWrapper));

            // TODO Proxy this.
            UserData.RegisterType<Texture2D>();

            RegisterApiEnum(script, "SymmetryMode", typeof(SymmetryMode));
            RegisterApiEnum(script, "SymmetryPointType", typeof(SymmetryPointType));
            RegisterApiEnum(script, "SymmetryWallpaperType", typeof(SymmetryWallpaperType));

        }

        public void RegisterApiEnum(Script script, string name, Type t, string prefix = null)
        {
            UserData.RegisterType(t);
            script.Globals[name] = UserData.CreateStatic(t);
#if UNITY_EDITOR
            LuaDocsRegistration.RegisterForDocs(t);
#endif
        }

        public void EnablePointerScript(bool enable)
        {
            PointerScriptsEnabled = enable;
            if (enable)
            {
                InitScript(GetActiveScript(LuaApiCategory.PointerScript));
            }
            else
            {
                EndActiveScript(LuaApiCategory.PointerScript);
            }
        }

        public void EnableBackgroundScripts(bool enable)
        {
            BackgroundScriptsEnabled = enable;
            if (enable)
            {
                foreach (var script in m_ActiveBackgroundScripts.Values)
                {
                    InitScript(script);
                }
            }
            else
            {
                foreach (var script in m_ActiveBackgroundScripts.Values)
                {
                    EndScript(script);
                }
            }
        }

        public List<string> GetScriptNames(LuaApiCategory category)
        {
            if (Scripts != null && Scripts.Count > 0)
            {
                return Scripts[category].Keys.ToList();
            }
            else
            {
                return new List<string>();
            }
        }

        public void InitWidgetConfigs(Script script, string scriptName)
        {
            m_WidgetConfigs[scriptName] = new Dictionary<string, ScriptWidgetConfig>();
            var paramsDynVal = script.Globals.Get(LuaNames.Parameters);
            if (!paramsDynVal.IsNil())
            {
                foreach (var pair in paramsDynVal.Table.Pairs)
                {
                    m_WidgetConfigs[scriptName][pair.Key.String] = new ScriptWidgetConfig
                    {
                        label = pair.Value.Table.Get("label").String,
                        type = pair.Value.Table.Get("type").String,
                        min = (float)pair.Value.Table.Get("min").Number,
                        max = (float)pair.Value.Table.Get("max").Number,
                        defaultVal = (float)pair.Value.Table.Get("default").Number
                    };
                }
            }
            // Replace the config table with the default value
            foreach (var item in m_WidgetConfigs[scriptName])
            {
                SetScriptParam(script, item.Key, item.Value.defaultVal);
            }
        }

        public Dictionary<string, ScriptWidgetConfig> GetWidgetConfigs(string scriptName)
        {
            return m_WidgetConfigs[scriptName];
        }

        public void SetScriptParameterForActiveScript(LuaApiCategory category, string paramName, float paramValue)
        {
            var script = GetActiveScript(category);
            SetScriptParam(script, paramName, paramValue);
        }

        private void SetScriptParam(Script script, string paramName, float paramValue)
        {
            script.Globals.Get(LuaNames.Parameters).Table.Set(paramName, DynValue.NewNumber(paramValue));
        }

        private DynValue GetScriptParam(Script script, object paramName)
        {
            return script.Globals.Get(LuaNames.Parameters).Table.Get(paramName);
        }

        public float GetOrSetWidgetCurrentValue(Script script, string paramName, ScriptWidgetConfig config)
        {
            // Try and get the value from the script
            var dynVal = GetScriptParam(script, paramName);
            float val;
            // If it isn't set...
            if (dynVal.Equals(DynValue.Nil))
            {
                // Get the default from the config entry
                val = config.defaultVal;
                // Set the value in the script
                SetScriptParam(script, paramName, val);
            }
            else
            {
                val = (float)dynVal.Number;
            }
            return val;
        }

        public void RecordPointerPositions(
            Vector3 brushPos_GS, Quaternion brushRot_GS,
            Vector3 wandPos_GS, Quaternion wandRot_GS,
            Vector3 headPos_GS, Quaternion headRot_GS)
        {
            m_TransformBuffers.AddBrushTr(App.Scene.ActiveCanvas.Pose.inverse * TrTransform.TR(brushPos_GS, brushRot_GS));
            m_TransformBuffers.AddWandTr(App.Scene.ActiveCanvas.Pose.inverse * TrTransform.TR(wandPos_GS, wandRot_GS));
            m_TransformBuffers.AddHeadTr(App.Scene.ActiveCanvas.Pose.inverse * TrTransform.TR(headPos_GS, headRot_GS));
        }

        public void ApplyPointerScript(Quaternion pointerRot, ref Vector3 pos_GS, ref Quaternion rot_GS)
        {
            bool shouldEndUndo = false;

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                ApiManager.Instance.StartUndo();
                m_TriggerWasPressed = true;
            }
            else if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                m_TriggerWasPressed = true;
            }
            else if (m_TriggerWasPressed)
            {
                m_TriggerWasPressed = false;
                shouldEndUndo = true;
            }

            bool scriptHasRun = CallActivePointerScript(LuaNames.Main, out var scriptResult);
            if (shouldEndUndo) ApiManager.Instance.EndUndo();
            if (!scriptHasRun) return;

            switch (scriptResult.Space)
            {
                case ScriptCoordSpace.Default:
                case ScriptCoordSpace.Pointer:
                    var oldPos = pos_GS;
                    pos_GS = scriptResult.Transform.translation;
                    pos_GS = pointerRot * pos_GS;
                    pos_GS += oldPos;
                    rot_GS *= scriptResult.Transform.rotation;
                    break;
                case ScriptCoordSpace.Canvas:
                    var tr_CS = TrTransform.TR(
                        scriptResult.Transform.translation,
                        scriptResult.Transform.rotation
                    );
                    var tr_GS = App.Scene.Pose * tr_CS;
                    pos_GS = tr_GS.translation;
                    rot_GS = tr_GS.rotation;
                    break;
            }
        }

        public static void SetTimer(Closure fn, float interval, float delay, int repeats)
        {
            var key = (fn.OwnerScript, fn.ReferenceID);
            m_Timers[key] = new LuaTimer(fn, interval, delay, repeats);
        }

        public static void UnsetTimer(Closure fn)
        {
            var key = (fn.OwnerScript, fn.ReferenceID);
            m_Timers.Remove(key);
        }

        public static void UnsetAllTimers(Script script)
        {
            var toRemove = new List<(Script OwnerScript, int ReferenceID)>();
            foreach (var key in m_Timers.Keys)
            {
                if (key.OwnerScript == script)
                {
                    toRemove.Add(key);
                }
            }
            foreach (var item in toRemove)
            {
                m_Timers.Remove(item);
            }
        }

        public void ToggleBackgroundScript(string scriptName)
        {
            bool isActive = m_ActiveBackgroundScripts.ContainsKey(scriptName);
            ActivateBackgroundScript(scriptName, !isActive);
        }

        public void ActivateBackgroundScript(string scriptName, bool active)
        {
            var script = Scripts[LuaApiCategory.BackgroundScript][scriptName];
            if (active)
            {
                m_ActiveBackgroundScripts[scriptName] = script;
                // Reinit if already present
                // Do we really want this or not?
                if (BackgroundScriptsEnabled) InitScript(script);
            }
            else
            {
                if (m_ActiveBackgroundScripts.ContainsKey(scriptName))
                {
                    m_ActiveBackgroundScripts.Remove(scriptName);
                    // Only call EndScript if background scripts are enabled globally
                    if (BackgroundScriptsEnabled) EndScript(script);
                }
            }
        }

        public bool CopyActiveScriptToUserScriptFolder(LuaApiCategory category)
        {
            var index = ActiveScripts[category];
            var scriptName = GetScriptNames(category)[index];
            return CopyScriptToUserScriptFolder(category, scriptName);
        }

        public bool CopyScriptToUserScriptFolder(LuaApiCategory category, string scriptName)
        {
            var originalFilename = $"{category}.{scriptName}";
            var newFilename = Path.Join(UserPluginsPath(), $"{originalFilename}.lua");
            if (!File.Exists(newFilename))
            {
                FileUtils.WriteTextFromResources($"LuaScriptExamples/{originalFilename}", newFilename);
                return true;
            }
            return false;
        }

        public bool IsBackgroundScriptActive(string scriptName)
        {
            return m_ActiveBackgroundScripts.ContainsKey(scriptName);
        }

        public void QueueWebRequest(UnityWebRequest request, Closure onSuccess, Closure onError, DynValue context)
        {
            var req = new LuaWebRequest
            {
                script = onSuccess.OwnerScript,
                request = request,
                onSuccess = onSuccess,
                onError = onError,
                context = context
            };
            m_WebRequests.AddLast(req);
        }

        private class LuaWebRequest
        {
            public Script script;
            public UnityWebRequest request;
            public Closure onSuccess;
            public Closure onError;
            public DynValue context;
        }

        public void DoToolScript(string fnName, TrTransform firstTr_CS, TrTransform secondTr_CS)
        {
            var result = CallActiveToolScript(fnName);
            if (result == null) return;
            List<List<TrTransform>> transforms = null;

            var drawnVector_CS = secondTr_CS.translation - firstTr_CS.translation;
            var tr_CS = new TrTransform();

            switch (result._Space)
            {
                case ScriptCoordSpace.Default:
                case ScriptCoordSpace.Pointer:

                    tr_CS.translation = firstTr_CS.translation;
                    tr_CS.rotation = drawnVector_CS == Vector3.zero ?
                        Quaternion.identity : Quaternion.LookRotation(drawnVector_CS, ScriptedTool.CalcStableUp(drawnVector_CS));
                    tr_CS.scale = 1f / App.ActiveCanvas.Pose.scale;
                    tr_CS.scale *= drawnVector_CS.magnitude;
                    transforms = result.AsMultiTrList();
                    break;
                case ScriptCoordSpace.Canvas:
                    tr_CS.translation = Vector3.zero;
                    tr_CS.rotation = Quaternion.identity;
                    tr_CS.scale = 1f;
                    transforms = result.AsMultiTrList();
                    break;
            }
            float brushScale = 1f;
            if (transforms != null) DrawStrokes.DrawNestedTrList(transforms, tr_CS, result._Colors, brushScale);
        }


    }
}
