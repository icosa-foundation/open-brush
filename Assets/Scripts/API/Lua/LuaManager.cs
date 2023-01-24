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

#if UNITY_EDITOR
        // Used when called via MenuItem("Open Brush/API/Generate Lua Autocomplete File")
        public static List<string> AutoCompleteEntries;
#endif

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

        private TransformBuffers m_TransformBuffers;
        private bool m_TriggerWasPressed;

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
            if (Directory.Exists(ApiManager.Instance.UserScriptsPath()))
            {
                m_FileWatcher = new FileWatcher(ApiManager.Instance.UserScriptsPath(), "*.lua");
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
            Init();
        }

        public void Init()
        {
            m_TransformBuffers = new TransformBuffers(512);
            m_ScriptPathsToUpdate = new List<string>();
            UserData.RegisterAssembly();
            Script.GlobalOptions.Platform = new StandardPlatformAccessor();
            LuaCustomConverters.RegisterAll();
            InitScriptDataStructures();
            LoadExampleScripts();
            LoadUserScripts();
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
                    var scriptName = LoadScriptFromPath(path);
                    ActiveScripts[category] = GetScriptNames(category).IndexOf(activeScriptName);
                    if (activeScriptName==scriptName) InitScript(GetActiveScript(category));
                }
            }
            m_ScriptPathsToUpdate.Clear();
        }

        public void InitScriptDataStructures()
        {
            Scripts = new Dictionary<ApiCategory, SortedDictionary<string, Script>>();
            ActiveScripts = new Dictionary<ApiCategory, int>();
            foreach (var category in ApiCategories)
            {
                Scripts[category] = new SortedDictionary<string, Script>();
                ActiveScripts[category] = 0;
            }
        }

        public void LoadUserScripts()
        {
            string[] files = Directory.GetFiles(ApiManager.Instance.UserScriptsPath(), LuaFileSearchPattern, SearchOption.AllDirectories);
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
                LoadScriptFromString(luaFile.name, luaFile.text);
            }
        }

        private void LoadScriptFromString(string filename, string contents)
        {
            Script script = new Script();
            script.Options.DebugPrint = s => Debug.Log(s);
            if (filename.StartsWith("__")) return;
            script.DoString(contents);
            ApiCategory? catMatch = null;
            foreach (ApiCategory category in ApiCategories)
            {
                var categoryName = category.ToString();
                if (filename.StartsWith(categoryName))
                {
                    catMatch = category;
                    break;
                };
            }
            if (catMatch.HasValue)
            {
                var category = catMatch.Value;
                string scriptName = filename.Substring(category.ToString().Length + 1);
                Scripts[category][scriptName] = script;
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

        public void LogLuaError(Script script, string fnName, InterpreterException e)
        {
            string msg = e.DecoratedMessage;
            if (string.IsNullOrEmpty(msg)) msg = e.Message;
            string errorMsg = $"{script.Globals.Get("ScriptName").String}:{fnName}:{msg}";
            ControllerConsoleScript.m_Instance.AddNewLine(errorMsg, true, true);
            Debug.LogError(errorMsg);
        }

        public void LogLuaMessage(string s)
        {
            ControllerConsoleScript.m_Instance.AddNewLine(s, false, true);
            Debug.Log(s);
        }

        private string LoadScriptFromPath(string path)
        {
            Script script = new Script();
            string scriptName = null;

            string scriptFilename = Path.GetFileNameWithoutExtension(path);
            if (scriptFilename.StartsWith("__")) return null;
            Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            script.DoStream(fileStream);
            var catMatch = TryGetCategoryFromScriptPath(path);
            if (catMatch.HasValue)
            {
                var category = catMatch.Value;
                scriptName = scriptFilename.Substring(category.ToString().Length + 1);
                script.Globals["ScriptName"] = scriptName;
                Scripts[category][scriptName] = script;
            }
            fileStream.Close();
            return scriptName;
        }

        public void SetDynamicScriptContext(Script script)
        {
            var brush_CS = m_TransformBuffers.CurrentBrushTr;
            var brushPos = brush_CS.translation;
            var brushRot = brush_CS.rotation;

            var wand_CS = m_TransformBuffers.CurrentWandTr;
            var wandPos = wand_CS.translation;
            var wandRot = wand_CS.rotation;

            float h, s, v;
            var pointerColor = PointerManager.m_Instance.PointerColor;
            Color.RGBToHSV(pointerColor, out h, out s, out v);

            BaseTool activeTool;

            try
            {
                activeTool = SketchSurfacePanel.m_Instance.ActiveTool;
                RegisterApiProperty(script, "brush.timeSincePressed", Time.realtimeSinceStartup - activeTool.TimeBecameActive);
                RegisterApiProperty(script, "brush.timeSinceReleased", Time.realtimeSinceStartup - activeTool.TimeBecameInactive);
                RegisterApiProperty(script, "brush.triggerIsPressed", activeTool.IsActive);
                RegisterApiProperty(script, "brush.triggerIsPressedThisFrame", activeTool.IsActiveThisFrame);
                RegisterApiProperty(script, "brush.distanceMoved", activeTool.DistanceMoved_CS);
                RegisterApiProperty(script, "brush.distanceDrawn", activeTool.DistanceDrawn_CS);
            }
            catch (NullReferenceException e)
            {
                // Monoscopic mode?
                RegisterApiProperty(script, "brush.timeSincePressed", 0);
                RegisterApiProperty(script, "brush.timeSinceReleased", 0);
                RegisterApiProperty(script, "brush.isPressed", false);
                RegisterApiProperty(script, "brush.isPressedThisFrame", false);
                RegisterApiProperty(script, "brush.distanceMoved", 0);
                RegisterApiProperty(script, "brush.distanceDrawn", 0);
            }

            RegisterApiProperty(script, "brush.position", brushPos);
            RegisterApiProperty(script, "brush.rotation", brushRot.eulerAngles);
            RegisterApiProperty(script, "brush.direction", brushRot * Vector3.forward);
            RegisterApiProperty(script, "brush.size.get", PointerManager.m_Instance.MainPointer.BrushSizeAbsolute);
            RegisterApiProperty(script, "brush.size01.get", PointerManager.m_Instance.MainPointer.BrushSize01);
            RegisterApiProperty(script, "brush.pressure", PointerManager.m_Instance.MainPointer.GetPressure());
            RegisterApiProperty(script, "brush.name", PointerManager.m_Instance.MainPointer.CurrentBrush?.m_Description);
            RegisterApiProperty(script, "brush.speed", PointerManager.m_Instance.MainPointer.MovementSpeed);

            RegisterApiProperty(script, "wand.position", wandPos);
            RegisterApiProperty(script, "wand.rotation", wandRot.eulerAngles);
            RegisterApiProperty(script, "wand.direction", wandRot * Vector3.forward);
            RegisterApiProperty(script, "wand.pressure", InputManager.Wand.GetTriggerValue());
            RegisterApiProperty(script, "wand.speed", InputManager.Wand.m_Velocity);

            // Colors
            var currentColor = PointerManager.m_Instance.PointerColor;
            var lastColor = PointerManager.m_Instance.m_lastChosenColor;
            RegisterApiProperty(script, "brush.color", currentColor);
            RegisterApiProperty(script, "brush.lastColorPicked", lastColor);
            Color.RGBToHSV(currentColor, out h, out s, out v);
            RegisterApiProperty(script, "brush.colorHsv", new Vector3(h, s, v));
            Color.RGBToHSV(lastColor, out h, out s, out v);
            RegisterApiProperty(script, "brush.lastColorPickedHsv", new Vector3(h, s, v));

            RegisterApiProperty(script, "app.time", Time.realtimeSinceStartup);
            RegisterApiProperty(script, "app.frames", Time.frameCount);
            RegisterApiProperty(script, "app.lastSelectedStroke", SelectionManager.m_Instance.LastSelectedStrokeCP);
            RegisterApiProperty(script, "app.lastStroke", SelectionManager.m_Instance.LastStrokeCP);

            RegisterApiProperty(script, "canvas.scale", App.ActiveCanvas.Pose.scale);
            RegisterApiProperty(script, "canvas.strokeCount", SketchMemoryScript.m_Instance.StrokeCount);
        }

        public void SetStaticScriptContext(Script script)
        {
            // Various useful methods
            RegisterApiCommand(script, "path.fromSvg", (Func<string, List<TrTransform>>)LuaApiMethods.PathFromSvg);
            RegisterApiCommand(script, "paths.fromSvg", (Func<string, List<List<TrTransform>>>)LuaApiMethods.PathsFromSvg);
            RegisterApiCommand(script, "brush.pastPosition", (Func<int, Vector3>)GetPastBrushPos);
            RegisterApiCommand(script, "brush.pastRotation", (Func<int, Quaternion>)GetPastBrushRot);
            RegisterApiCommand(script, "wand.pastPosition", (Func<int, Vector3>)GetPastWandPos);
            RegisterApiCommand(script, "wand.pastRotation", (Func<int, Quaternion>)GetPastWandRot);
            RegisterApiCommand(script, "path.transform", (Func<List<TrTransform>, TrTransform, List<TrTransform>>)LuaApiMethods.TransformPath);
            RegisterApiCommand(script, "path.translate", (Func<List<TrTransform>, Vector3, List<TrTransform>>)LuaApiMethods.TranslatePath);
            RegisterApiCommand(script, "path.rotate", (Func<List<TrTransform>, Quaternion, List<TrTransform>>)LuaApiMethods.RotatePath);
            RegisterApiCommand(script, "path.scale", (Func<List<TrTransform>, Vector3, List<TrTransform>>)LuaApiMethods.ScalePath);
            RegisterApiCommand(script, "vector.vectorToRotation", (Func<Vector3, Quaternion>)LuaApiMethods.VectorToRotation);
            RegisterApiCommand(script, "vector.rotationToVector", (Func<Quaternion, Vector3>)LuaApiMethods.RotationToVector);

            // The Http Api commands for these take strings as input which we don't want
            // RegisterApiCommand(script, "draw.paths", (Action<string>)ApiMethods.DrawPaths);
            // RegisterApiCommand(script, "draw.path", (Action<string>)ApiMethods.DrawPath);
            // RegisterApiCommand(script, "draw.stroke", (Action<string>)ApiMethods.DrawStroke);

            RegisterApiCommand(script, "draw.path", (Action<List<TrTransform>>) LuaApiMethods.DrawPath);
            RegisterApiCommand(script, "draw.paths", (Action<List<List<TrTransform>>>) LuaApiMethods.DrawPaths);

            RegisterApiCommand(script, "draw.polygon", (Action<int, float, float>)ApiMethods.DrawPolygon);
            RegisterApiCommand(script, "draw.text", (Action<string>)ApiMethods.Text);
            RegisterApiCommand(script, "draw.svg", (Action<string>)ApiMethods.SvgPath);
            RegisterApiCommand(script, "brush.type", (Action<string>)ApiMethods.Brush);

            RegisterApiCommand(script, "color.addHsv", (Action<Vector3>)ApiMethods.AddColorHSV);
            RegisterApiCommand(script, "color.addRgb", (Action<Vector3>)ApiMethods.AddColorRGB);
            RegisterApiCommand(script, "color.setRgb", (Action<Vector3>)ApiMethods.SetColorRGB);
            RegisterApiCommand(script, "color.setHsv", (Action<Vector3>)ApiMethods.SetColorHSV);
            RegisterApiCommand(script, "color.setHtml", (Action<string>)ApiMethods.SetColorHTML);
            RegisterApiCommand(script, "color.jitter", (Action)LuaApiMethods.JitterColor);

            RegisterApiCommand(script, "brush.sizeSet", (Action<float>)ApiMethods.BrushSizeSet);
            RegisterApiCommand(script, "brush.sizeAdd", (Action<float>)ApiMethods.BrushSizeAdd);
            RegisterApiCommand(script, "draw.camerapath", (Action<int>)ApiMethods.DrawCameraPath);

            RegisterApiCommand(script, "listenfor.strokes", (Action<string>)ApiMethods.AddListener);
            RegisterApiCommand(script, "showfolder.scripts", (Action)ApiMethods.OpenUserScriptsFolder);
            RegisterApiCommand(script, "showfolder.exports", (Action)ApiMethods.OpenExportFolder);
            RegisterApiCommand(script, "spectator.moveTo", (Action<Vector3>)ApiMethods.MoveSpectatorTo);
            RegisterApiCommand(script, "user.moveTo", (Action<Vector3>)ApiMethods.MoveUserTo);
            RegisterApiCommand(script, "spectator.moveBy", (Action<Vector3>)ApiMethods.MoveSpectatorBy);
            RegisterApiCommand(script, "user.moveBy", (Action<Vector3>)ApiMethods.MoveUserBy);
            RegisterApiCommand(script, "spectator.turnY", (Action<float>)ApiMethods.SpectatorYaw);
            RegisterApiCommand(script, "spectator.turnX", (Action<float>)ApiMethods.SpectatorPitch);
            RegisterApiCommand(script, "spectator.turnY", (Action<float>)ApiMethods.SpectatorRoll);
            RegisterApiCommand(script, "spectator.direction", (Action<Vector3>)ApiMethods.SpectatorDirection);
            RegisterApiCommand(script, "spectator.lookAt", (Action<Vector3>)ApiMethods.SpectatorLookAt);
            RegisterApiCommand(script, "spectator.mode", (Action<string>)ApiMethods.SpectatorMode);
            RegisterApiCommand(script, "spectator.show", (Action<string>)ApiMethods.SpectatorShow);
            RegisterApiCommand(script, "spectator.hide", (Action<string>)ApiMethods.SpectatorHide);

            // Turtle based drawing isn't currently relevant to the runtime API
            // RegisterApiCommand(script, "brush.move.to", (Action<Vector3>)ApiMethods.BrushMoveTo);
            // RegisterApiCommand(script, "brush.move.by", (Action<Vector3>)ApiMethods.BrushMoveBy);
            // RegisterApiCommand(script, "brush.move", (Action<float>)ApiMethods.BrushMove);
            // RegisterApiCommand(script, "brush.draw", (Action<float>)ApiMethods.BrushDraw);
            // RegisterApiCommand(script, "brush.turn.y", (Action<float>)ApiMethods.BrushYaw);
            // RegisterApiCommand(script, "brush.turn.x", (Action<float>)ApiMethods.BrushPitch);
            // RegisterApiCommand(script, "brush.turn.z", (Action<float>)ApiMethods.BrushRoll);
            // RegisterApiCommand(script, "brush.look.at", (Action<Vector3>)ApiMethods.BrushLookAt);
            // RegisterApiCommand(script, "brush.look.forwards", (Action)ApiMethods.BrushLookForwards);
            // RegisterApiCommand(script, "brush.look.up", (Action)ApiMethods.BrushLookUp);
            // RegisterApiCommand(script, "brush.look.down", (Action)ApiMethods.BrushLookDown);
            // RegisterApiCommand(script, "brush.look.left", (Action)ApiMethods.BrushLookLeft);
            // RegisterApiCommand(script, "brush.look.right", (Action)ApiMethods.BrushLookRight);
            // RegisterApiCommand(script, "brush.look.backwards", (Action)ApiMethods.BrushLookBackwards);
            // RegisterApiCommand(script, "brush.home.reset", (Action)ApiMethods.BrushHome);
            // RegisterApiCommand(script, "brush.home.set", (Action)ApiMethods.BrushSetHome);
            // RegisterApiCommand(script, "brush.transform.push", (Action)ApiMethods.BrushTransformPush);
            // RegisterApiCommand(script, "brush.transform.pop", (Action)ApiMethods.BrushTransformPop);
            RegisterApiCommand(script, "debug.brush", (Action)ApiMethods.DebugBrush);
            RegisterApiCommand(script, "image.import", (Action<string>)ApiMethods.ImportImage);
            RegisterApiCommand(script, "environmentType", (Action<string>)ApiMethods.SetEnvironment);
            RegisterApiCommand(script, "layer.add", (Action)ApiMethods.AddLayer);
            RegisterApiCommand(script, "layer.clear", (Action<int>)ApiMethods.ClearLayer);
            RegisterApiCommand(script, "layer.delete", (Action<int>)ApiMethods.DeleteLayer);
            RegisterApiCommand(script, "layer.squash", (Action<int, int>)ApiMethods.SquashLayer);
            RegisterApiCommand(script, "layer.activate", (Action<int>)ApiMethods.ActivateLayer);
            RegisterApiCommand(script, "layer.show", (Action<int>)ApiMethods.ShowLayer);
            RegisterApiCommand(script, "layer.hide", (Action<int>)ApiMethods.HideLayer);
            RegisterApiCommand(script, "layer.toggle", (Action<int>)ApiMethods.ToggleLayer);
            RegisterApiCommand(script, "model.select", (Action<int>)ApiMethods.SelectModel);
            RegisterApiCommand(script, "model.position", (Action<int, Vector3>)ApiMethods.PositionModel);
            RegisterApiCommand(script, "brush.forcePaintingOn", (Action<bool>)ApiMethods.ForcePaintingOn);
            RegisterApiCommand(script, "brush.forcePaintingOff", (Action<bool>)ApiMethods.ForcePaintingOff);
            RegisterApiCommand(script, "image.position", (Action<int, Vector3>)ApiMethods.PositionImage);
            RegisterApiCommand(script, "guide.add", (Action<string>)ApiMethods.AddGuide);

            RegisterApiCommand(script, "save.overwrite", (Action)ApiMethods.SaveOverwrite);
            RegisterApiCommand(script, "save.new", (Action)ApiMethods.SaveNew);
            RegisterApiCommand(script, "export.all", (Action)ApiMethods.ExportAll);
            RegisterApiCommand(script, "drafting.visible", (Action)ApiMethods.DraftingVisible);
            RegisterApiCommand(script, "drafting.transparent", (Action)ApiMethods.DraftingTransparent);
            RegisterApiCommand(script, "drafting.hidden", (Action)ApiMethods.DraftingHidden);

            // TODO Fix name collision with "load"
            // RegisterApiCommand(script, "load.user", (Action<int>)ApiMethods.LoadUser);
            // RegisterApiCommand(script, "load.curated", (Action<int>)ApiMethods.LoadCurated);
            // RegisterApiCommand(script, "load.liked", (Action<int>)ApiMethods.LoadLiked);
            // RegisterApiCommand(script, "load.drive", (Action<int>)ApiMethods.LoadDrive);
            // RegisterApiCommand(script, "load.named", (Action<string>)ApiMethods.LoadNamedFile);
            // And "new"
            // RegisterApiCommand(script, "new", (Action)ApiMethods.NewSketch);

            RegisterApiCommand(script, "merge.named", (Action<string>)ApiMethods.MergeNamedFile);
            RegisterApiCommand(script, "symmetry.mirror", (Action)ApiMethods.SymmetryPlane);
            RegisterApiCommand(script, "symmetry.doublemirror", (Action)ApiMethods.SymmetryFour);
            RegisterApiCommand(script, "twohandeded.toggle", (Action)ApiMethods.SymmetryTwoHanded);
            RegisterApiCommand(script, "straightedge.toggle", (Action)ApiMethods.StraightEdge);
            RegisterApiCommand(script, "autoorient.toggle", (Action)ApiMethods.AutoOrient);
            RegisterApiCommand(script, "undo", (Action)ApiMethods.Undo);
            RegisterApiCommand(script, "redo", (Action)ApiMethods.Redo);
            RegisterApiCommand(script, "panels.reset", (Action)ApiMethods.ResetAllPanels);
            RegisterApiCommand(script, "sketch.origin", (Action)ApiMethods.SketchOrigin);
            RegisterApiCommand(script, "viewonly.toggle", (Action)ApiMethods.ViewOnly);
            RegisterApiCommand(script, "spectator.toggle", (Action)ApiMethods.ToggleSpectator);
            RegisterApiCommand(script, "spectator.on", (Action)ApiMethods.EnableSpectator);
            RegisterApiCommand(script, "spectator.off", (Action)ApiMethods.DisableSpectator);
            RegisterApiCommand(script, "autosimplify.toggle", (Action)ApiMethods.ToggleAutosimplification);
            RegisterApiCommand(script, "export.current", (Action)ApiMethods.ExportRaw);
            RegisterApiCommand(script, "showfolder.sketch", (Action<int>)ApiMethods.ShowSketchFolder);
            RegisterApiCommand(script, "guides.disable", (Action)ApiMethods.StencilsDisable);
            RegisterApiCommand(script, "disco", (Action)ApiMethods.Disco);
            RegisterApiCommand(script, "selection.duplicate", (Action)ApiMethods.Duplicate);
            RegisterApiCommand(script, "selection.group", (Action)ApiMethods.ToggleGroupStrokesAndWidgets);
            RegisterApiCommand(script, "export.selected", (Action)ApiMethods.SaveModel);
            RegisterApiCommand(script, "camerapath.render", (Action)ApiMethods.RenderCameraPath);
            RegisterApiCommand(script, "profiling.toggle", (Action)ApiMethods.ToggleProfiling);
            RegisterApiCommand(script, "settings.toggle", (Action)ApiMethods.ToggleSettings);
            RegisterApiCommand(script, "mirror.summon", (Action)ApiMethods.SummonMirror);
            RegisterApiCommand(script, "selection.invert", (Action)ApiMethods.InvertSelection);

            // Another collision?
            // RegisterApiCommand(script, "select.all", (Action)ApiMethods.SelectAll);

            RegisterApiCommand(script, "selection.flip", (Action)ApiMethods.FlipSelection);
            RegisterApiCommand(script, "postprocessing.toggle", (Action)ApiMethods.ToggleCameraPostEffects);
            RegisterApiCommand(script, "watermark.toggle", (Action)ApiMethods.ToggleWatermark);
            RegisterApiCommand(script, "camerapath.togglevisuals", (Action)ApiMethods.ToggleCameraPathVisuals);
            RegisterApiCommand(script, "camerapath.togglepreview", (Action)ApiMethods.ToggleCameraPathPreview);
            RegisterApiCommand(script, "camerapath.delete", (Action)ApiMethods.DeleteCameraPath);
            RegisterApiCommand(script, "camerapath.record", (Action)ApiMethods.RecordCameraPath);

            RegisterApiCommand(script, "stroke.delete", (Action<int>)ApiMethods.DeleteStroke);
            RegisterApiCommand(script, "stroke.select", (Action<int>)ApiMethods.SelectStroke);
            RegisterApiCommand(script, "strokes.select", (Action<int, int>)ApiMethods.SelectStrokes);
            RegisterApiCommand(script, "selection.recolor", (Action)ApiMethods.RecolorSelection);
            RegisterApiCommand(script, "selection.rebrush", (Action)ApiMethods.RebrushSelection);
            RegisterApiCommand(script, "selection.resize", (Action)ApiMethods.ResizeSelection);
            RegisterApiCommand(script, "selection.trim", (Action<int>)ApiMethods.TrimSelection);
            RegisterApiCommand(script, "selection.pointsPerlin", (Action<string, Vector3>)ApiMethods.PerlinNoiseSelection);
            RegisterApiCommand(script, "stroke.pointsQuantize", (Action<Vector3>)ApiMethods.QuantizeSelection);
            RegisterApiCommand(script, "stroke.join", (Action)ApiMethods.JoinStroke);
            RegisterApiCommand(script, "strokes.join", (Action<int, int>)ApiMethods.JoinStrokes);
            RegisterApiCommand(script, "stroke.add", (Action<int>)ApiMethods.AddPointToStroke);

#if UNITY_EDITOR
            // These are placeholder entries to populate the autocomplete file
            // Populated for real from other places
            RegisterApiProperty(script, "tool.startPosition", Vector3.one);
            RegisterApiProperty(script, "tool.endPosition", Vector3.one);
            RegisterApiProperty(script, "tool.vector", Vector3.one);
#endif
        }

        private Vector3 GetPastBrushPos(int back)
        {
            return m_TransformBuffers.PastBrushTr(back).translation;
        }

        private Quaternion GetPastBrushRot(int back)
        {
            return m_TransformBuffers.PastBrushTr(back).rotation;
        }

        private Vector3 GetPastWandPos(int back)
        {
            return m_TransformBuffers.PastWandTr(back).translation;
        }

        private Quaternion GetPastWandRot(int back)
        {
            return m_TransformBuffers.PastWandTr(back).rotation;
        }

        public void RegisterApiProperty(Script script, string cmd, object action)
        {
            _RegisterToApi(script, cmd, action);
#if UNITY_EDITOR
            if (Application.isEditor && AutoCompleteEntries!=null)
            {
                AutoCompleteEntries.Add($"{cmd} = nil");
            }
#endif
        }

        public void RegisterApiCommand(Script script, string cmd, object action)
        {
            _RegisterToApi(script, cmd, action);
#if UNITY_EDITOR
            if (Application.isEditor && AutoCompleteEntries!=null)
            {
                string paramNames = "";
                Delegate d = action as Delegate;
                var paramNameList = d.Method.GetParameters().Select(p => p.Name);
                paramNames = string.Join(", ", paramNameList);
                AutoCompleteEntries.Add($"function {cmd}({paramNames}) end");
            }
#endif
        }

        public void _RegisterToApi(Script script, string cmd, object action)
        {
            var parts = cmd.Split(".");
            Table currentTable = script.Globals;
            for (var index = 0; index < parts.Length; index++)
            {
                var part = parts[index];
                if (index < parts.Length - 1)
                {
                    if (Equals(currentTable.Get(part), DynValue.Nil))
                    {
                        currentTable.Set(part, DynValue.NewTable(new Table(script)));
                    }
                    currentTable = currentTable.Get(part).Table;
                }
                else
                {
                    currentTable[part] = action;
                }
            }
        }

        public Script GetActiveScript(ApiCategory category)
        {
            string scriptName = GetScriptNames(category)[ActiveScripts[category]];
            return Scripts[category][scriptName];
        }

        private DynValue _CallScript(Script script, string fnName)
        {
            SetDynamicScriptContext(script);
            Closure activeFunction = script.Globals.Get(fnName).Function;
            DynValue result = DynValue.Nil;
            if (activeFunction != null)
            {
                try
                {
                    result = activeFunction.Call();
                }
                catch (InterpreterException  e)
                {
                    LogLuaError(script, fnName, e);
                }
            }
            return result;
        }

        private ScriptTrTransform CallActivePointerScript(string fnName)
        {
            var script = GetActiveScript(ApiCategory.PointerScript);
            DynValue result = _CallScript(script, fnName);
            var space = _GetSpaceForActiveScript(ApiCategory.PointerScript);
            var tr = TrTransform.identity;
            if (!Equals(result, DynValue.Nil)) tr = result.ToObject<TrTransform>();
            return new ScriptTrTransform(tr, space);
        }

        public ScriptTrTransforms CallActiveToolScript(string fnName)
        {
            var script = GetActiveScript(ApiCategory.ToolScript);
            var space = _GetSpaceForActiveScript(ApiCategory.ToolScript);
            var trs = _TrTransformsFromScript(fnName, script);
            return new ScriptTrTransforms(trs, space);
        }

        public ScriptTrTransforms CallActiveSymmetryScript(string fnName)
        {
            var script = GetActiveScript(ApiCategory.SymmetryScript);
            var space = _GetSpaceForActiveScript(ApiCategory.SymmetryScript);
            var trs = _TrTransformsFromScript(fnName, script);
            return new ScriptTrTransforms(trs, space);
        }

        private List<TrTransform> _TrTransformsFromScript(string fnName, Script script)
        {
            DynValue result = _CallScript(script, fnName);
            var trs = new List<TrTransform>();
            try
            {
                if (!Equals(result, DynValue.Nil)) trs = result.ToObject<List<TrTransform>>();
            }
            catch (InterpreterException  e)
            {
                LogLuaError(script, fnName, e);
            }
            return trs;
        }

        public DynValue GetSettingForActiveScript(ApiCategory category, string key)
        {
            var script = GetActiveScript(category);
            var settings = script.Globals.Get("Settings");
            return settings?.Table?.Get(key);
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
            var spaceVal = GetSettingForActiveScript(category, "space");
            if (spaceVal != null)
            {
                Enum.TryParse(spaceVal.String, true, out space);
            }

            return space;
        }

        public void ChangeCurrentScript(ApiCategory category, int increment)
        {

            var previousScript = GetActiveScript(category);
            // TODO Only call this if previousScript has been initialized and hasn't already been ended
            // Checking a method for null does this but only really as a side-effect
            if (previousScript.Globals.Get("draw.path").Function != null) _CallScript(previousScript, "End");

            int ActualMod(int x, int m) => (x % m + m) % m;

            if (Scripts[category].Count == 0) return;
            ActiveScripts[category] += increment;
            ActiveScripts[category] = ActualMod(ActiveScripts[category], Scripts[category].Count);
            InitScript(GetActiveScript(category));
        }

        public void InitScript(Script script)
        {
            // Redirect "print"
            script.Options.DebugPrint = LogLuaMessage;

            // // Automatic reg
            // var libraries = Resources.LoadAll<TextAsset>("LuaLibraries");
            // foreach (var library in libraries)
            // {
            //     Debug.Log($"Loaded lua library {library.name}");
            //     script.DoString(library.text);
            // }

            script.Globals["Mathf"] = typeof(UnityMathf);

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
            SetStaticScriptContext(script);
            _CallScript(script, "Start");
        }

        public void EnablePointerScript(bool enable)
        {
            PointerScriptsEnabled = enable;
            if (enable)
            {
                InitScript(GetActiveScript(ApiCategory.PointerScript));
            }
            else
            {
                CallActivePointerScript("End");
            }
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
            ScriptTrTransform scriptTransformOutput = new ScriptTrTransform();
            bool scriptHasRun = false;

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                scriptTransformOutput = CallActivePointerScript("OnTriggerPressed");
                m_TriggerWasPressed = true;
                scriptHasRun = true;
            }
            else if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                scriptTransformOutput = CallActivePointerScript("WhileTriggerPressed");
                m_TriggerWasPressed = true;
                scriptHasRun = true;
            }
            else if (m_TriggerWasPressed)
            {
                scriptTransformOutput = CallActivePointerScript("OnTriggerReleased");
                m_TriggerWasPressed = false;
                scriptHasRun = true;
            }

            if (!scriptHasRun) return;

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
