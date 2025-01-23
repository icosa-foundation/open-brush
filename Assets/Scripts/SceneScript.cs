// Copyright 2020 The Tilt Brush Authors
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
using TiltBrush.FrameAnimation;

namespace TiltBrush
{

    // TODO: Allow light count to be reduced.  Having to deal with inactive
    // lights is a burden for the restLayerCanvasesUpdate of the codebase, and can be error prone
    // (e.g. component enabled vs. game object active).
    public class SceneScript : MonoBehaviour
    {

        public delegate void PoseChangedEventHandler(TrTransform prev, TrTransform current);
        public event PoseChangedEventHandler PoseChanged;

        public delegate void ActiveCanvasChangedEventHandler(CanvasScript prev, CanvasScript current);
        public event ActiveCanvasChangedEventHandler ActiveCanvasChanged;

        public delegate void LayerCanvasesUpdateEventHandler();
        public event LayerCanvasesUpdateEventHandler LayerCanvasesUpdate;

        [SerializeField] public CanvasScript m_MainCanvas;
        [SerializeField] private CanvasScript m_SelectionCanvas;

        public AnimationUI_Manager animationUI_manager;

        public GameObject captureRig;

        private bool m_bInitialized;
        private Light[] m_Lights;
        private HashSet<int> m_DeletedLayers;

        public CanvasScript m_ActiveCanvas;
        public List<CanvasScript> m_LayerCanvases;

        /// Helper for getting and setting transforms on Transform components.
        /// Transform natively allows you to access parent-relative ("local")
        /// and root-relative ("global") views of position, rotation, and scale.
        ///
        /// This helper gives you a scene-relative view of the transform.
        /// The syntax is a slight abuse of C#:
        ///
        ///   TrTranform xf_SS = App.Scene.AsScene[gameobj.transform];
        ///   App.Scene.AsScene[gameobj.transform] = xf_SS;
        ///
        /// Safe to use during Awake()
        ///
        public TransformExtensions.RelativeAccessor AsScene;

        [NonSerialized]
        public bool disableTiltProtection;

        /// The global pose of this scene. All scene modifications must go through this.
        /// On assignment, range of local scale is limited (log10) to +/-4.
        /// Emits SceneScript.PoseChanged, CanvasScript.PoseChanged.
        public TrTransform Pose
        {
            get
            {
                return Coords.AsGlobal[transform];
            }
            set
            {
                var prevScene = Coords.AsGlobal[transform];

                value = SketchControlsScript.MakeValidScenePose(value,
                    SceneSettings.m_Instance.HardBoundsRadiusMeters_SS);

                // Clamp scale, and prevent tilt. These are last-ditch sanity checks
                // and are not the proper way to impose UX constraints.
                {
                    value.scale = Mathf.Clamp(Mathf.Abs(value.scale), 1e-4f, 1e4f);
                    bool bRestoreUp = true;
                    bRestoreUp = !disableTiltProtection;
                    if (bRestoreUp)
                    {
                        var qRestoreUp = Quaternion.FromToRotation(
                            value.rotation * Vector3.up, Vector3.up);
                        value = TrTransform.R(qRestoreUp) * value;
                    }
                }

                Coords.AsGlobal[transform] = value;

                // hasChanged is used in development builds to detect unsanctioned
                // changes to the transform. Set to false so we don't trip the detection!
                transform.hasChanged = false;
                if (PoseChanged != null)
                {
                    PoseChanged(prevScene, value);
                }
                using (var canvases = AllCanvases.GetEnumerator())
                {
                    while (canvases.MoveNext())
                    {
                        canvases.Current.OnScenePoseChanged(prevScene, value);
                    }
                }
            }
        }

        /// Safe to use any time after initialization
        public CanvasScript ActiveCanvas
        {
            get
            {
                Debug.Assert(m_bInitialized);
                return m_ActiveCanvas;
            }
            set
            {
                Debug.Assert(m_bInitialized);
                if (value != m_ActiveCanvas)
                {
                    var prev = m_ActiveCanvas;
                    m_ActiveCanvas = value;
                    if (ActiveCanvasChanged != null)
                    {
                        ActiveCanvasChanged?.Invoke(prev, m_ActiveCanvas);
                        // This will be incredibly irritating, but until we have some other feedback...
                        // TODO:Mikesky - replace this popup (console?)
                        // OutputWindowScript.m_Instance.CreateInfoCardAtController(
                        //     InputManager.ControllerName.Brush,
                        //     string.Format("Canvas is now {0}", ActiveCanvas.gameObject.name),
                        //     fPopScalar: 0.5f, false);
                    }
                }
            }
        }

        /// The initial start-up canvas; guaranteed to always exist
        public CanvasScript MainCanvas { get { return m_MainCanvas; } }
        public CanvasScript SelectionCanvas { get { return m_SelectionCanvas; } }

        public IEnumerable<CanvasScript> AllCanvases
        {
            get
            {
                yield return MainCanvas;
                if (SelectionCanvas != null)
                {
                    yield return SelectionCanvas;
                }

                if (m_LayerCanvases != null)
                {
                    for (int i = 0; i < m_LayerCanvases.Count; ++i)
                    {
                        yield return m_LayerCanvases[i];
                    }
                }
            }
        }

        // Same as AllCanvases except it ignores the selection canvas and excludes "deleted" layers
        public IEnumerable<CanvasScript> LayerCanvases
        {
            get
            {
                yield return MainCanvas;

                if (m_LayerCanvases != null)
                {
                    for (int i = 0; i < m_LayerCanvases.Count; ++i)
                    {
                        if (m_DeletedLayers.Contains(i)) continue;
                        yield return m_LayerCanvases[i];
                    }
                }
            }
        }

        public void ResetLayers(bool notify = false)
        {
            if (m_LayerCanvases != null)
            {
                foreach (var canvas in m_LayerCanvases.ToArray())
                {
                    DestroyLayer(canvas);
                }
            }
            m_LayerCanvases = new List<CanvasScript>();
            m_DeletedLayers = new HashSet<int>();
            m_ActiveCanvas = MainCanvas;
            if (notify) App.Scene.LayerCanvasesUpdate?.Invoke();
        }


        // Init unless already initialized. Safe to call zero or multiple times.
        public void Init()
        {
            if (m_bInitialized) return;
            m_bInitialized = true;
            ResetLayers();
            AsScene = new TransformExtensions.RelativeAccessor(transform);
            m_ActiveCanvas = m_MainCanvas;
            foreach (var c in AllCanvases)
            {
                c.Init();
            }
        }

        void Awake()
        {
            Init();
            m_Lights = new Light[(int)LightMode.NumLights];
            for (int i = 0; i < m_Lights.Length; ++i)
            {
                GameObject go = new GameObject(string.Format("SceneLight {0}", i));
                Transform t = go.transform;
                t.parent = App.Instance.m_EnvironmentTransform;
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                Light newLight = go.AddComponent<Light>();
                m_Lights[i] = newLight;
            }

            m_Lights[(int)LightMode.Shadow].shadows = LightShadows.Hard;
            m_Lights[(int)LightMode.Shadow].renderMode = LightRenderMode.ForcePixel;
            m_Lights[(int)LightMode.NoShadow].shadows = LightShadows.None;
            m_Lights[(int)LightMode.NoShadow].renderMode = LightRenderMode.ForceVertex;
        }

        public CanvasScript AddLayerNow()
        {
            var go = new GameObject(string.Format("Layer {0}", LayerCanvases.Count()));
            go.transform.parent = transform;
            Coords.AsLocal[go.transform] = TrTransform.identity;
            go.transform.hasChanged = false;

            // Rather misleadingly the Unity layer "MainCanvas" is actually used for all non-selection canvases
            // Otherwise GPU intersection filters them out
            HierarchyUtils.RecursivelySetLayer(go.transform, App.Scene.MainCanvas.gameObject.layer);
            var layer = go.AddComponent<CanvasScript>();
            m_LayerCanvases.Add(layer);
            App.Scene.ActiveCanvas = layer;
            App.Scene.LayerCanvasesUpdate?.Invoke();
            // Add canvases for other animation frames
            layer = animationUI_manager.AddLayerRefresh(layer);
            return layer;
        }

        public CanvasScript AddCanvas()
        {
            var go = new GameObject("new");
            go.transform.parent = transform;
            Coords.AsLocal[go.transform] = TrTransform.identity;
            go.transform.hasChanged = false;
            HierarchyUtils.RecursivelySetLayer(go.transform, App.Scene.MainCanvas.gameObject.layer);
            var frame = go.AddComponent<CanvasScript>();
            return frame;
        }

        public void DestroyCanvas(CanvasScript layer)
        {
            foreach (Batch b in layer.BatchManager.AllBatches())
                b.Destroy();
            Destroy(layer.gameObject);
        }

        // Destructive delete - no undo possible
        public void DestroyLayer(CanvasScript layer)
        {
            if (layer == MainCanvas) return;
            m_LayerCanvases.Remove(layer);
            foreach (Batch b in layer.BatchManager.AllBatches())
                b.Destroy();
            Destroy(layer.gameObject);
        }

        public bool IsLayerDeleted(CanvasScript layer)
        {
            var layerIndex = GetIndexOfCanvas(layer).Item1 - 1;
            return IsLayerDeleted(layerIndex);
        }

        public (int, int) GetIndexOfCanvas(CanvasScript canvas)
        {
            if (App.Scene.animationUI_manager == null || App.Scene.animationUI_manager.GetTimelineLength() == 0)
            {
                int index = m_LayerCanvases.IndexOf(canvas);
                return (index + 1, 0);
            }
            return App.Scene.animationUI_manager.GetCanvasLocation(canvas);
        }

        public int GetLayerNumFromCanvas(CanvasScript canvas)
        {
            int index = 0;
            foreach (CanvasScript layer in LayerCanvases)
            {
                if (layer.Equals(canvas)) { break; };
                index++;
            }
            return index;
        }

        public CanvasScript GetCanvasFromLayerNum(int num)
        {
            int index = 0;
            foreach (CanvasScript layer in LayerCanvases)
            {
                if (num == index)
                {
                    return layer;
                    ;
                };
                index++;
            }
            return null;
        }

        public bool IsLayerDeleted(int layerIndex)
        {
            return m_DeletedLayers.Contains(layerIndex);
        }

        public int GetNumLights()
        {
            return m_Lights.Length;
        }

        public Light GetLight(int index)
        {
            return m_Lights[index];
        }

        public void ToggleLayerVisibility(int canvasIndex)
        {
            ToggleLayerVisibility(GetCanvasByLayerIndex(canvasIndex));
        }

        public void ToggleLayerVisibility(CanvasScript canvas)
        {
            if (canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
            else canvas.gameObject.SetActive(true);
            animationUI_manager.UpdateLayerVisibilityRefresh(canvas);
        }

        public void TriggerLayersUpdate()
        {
            App.Scene.LayerCanvasesUpdate?.Invoke();
        }

        public void ShowLayer(int canvasIndex) { ShowLayer(GetCanvasByLayerIndex(canvasIndex)); }
        public void ShowLayer(CanvasScript canvas)
        {
            canvas.gameObject.SetActive(true);
            App.Scene.LayerCanvasesUpdate?.Invoke();
        }

        public void ShowCanvas(CanvasScript canvas)
        {
            canvas.gameObject.SetActive(true);
        }

        public void HideLayer(int canvasIndex) { HideLayer(GetCanvasByLayerIndex(canvasIndex)); }
        public void HideLayer(CanvasScript canvas)
        {
            canvas.gameObject.SetActive(false);
            App.Scene.LayerCanvasesUpdate?.Invoke();
        }

        public void HideCanvas(CanvasScript canvas)
        {
            canvas.gameObject.SetActive(false);
        }

        public CanvasScript GetOrCreateLayer(int layerIndex, int frameIndex)
        {
            if (layerIndex < animationUI_manager.Timeline.Count && frameIndex < animationUI_manager.GetTimelineLength())
            {
                return animationUI_manager.GetTimelineCanvas(layerIndex, frameIndex);
            }
            return GetOrCreateLayer(layerIndex);
        }

        public CanvasScript GetOrCreateLayer(int layerIndex)
        {
            // Layers are numbered 0=Main then 1, 2, 3
            if (layerIndex == 0)
                return App.Scene.MainCanvas;

            for (var i = m_LayerCanvases.Count; i < layerIndex; ++i)
                AddLayerNow();

            // Subtract one to use it as index into m_LayerCanvases, which only stores extra layers
            return m_LayerCanvases[layerIndex - 1];
        }

        public void ClearLayerContents(CanvasScript canvas)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ClearLayerCommand(canvas));
            App.Scene.LayerCanvasesUpdate?.Invoke();
        }

        public bool IsLayerVisible(CanvasScript layer)
        {
            return layer.gameObject.activeSelf;
        }

        public void MarkLayerAsDeleted(CanvasScript layer)
        {
            if (layer == MainCanvas) return;
            m_DeletedLayers.Add(GetIndexOfCanvas(layer).Item1 - 1);
            App.Scene.LayerCanvasesUpdate?.Invoke();

            animationUI_manager.MarkLayerAsDeleteRefresh(layer);
        }

        public void RenameLayer(CanvasScript layer, string newName)
        {
            if (layer == MainCanvas) return;
            layer.gameObject.name = newName;
            App.Scene.LayerCanvasesUpdate?.Invoke();
        }

        public void MarkLayerAsNotDeleted(CanvasScript layer)
        {
            m_DeletedLayers.Remove(GetIndexOfCanvas(layer).Item1 - 1);
            animationUI_manager.MarkLayerAsNotDeleteRefresh(layer);

            App.Scene.LayerCanvasesUpdate?.Invoke();
        }

        public void BroadcastCanvasUpdate()
        {
            App.Scene.LayerCanvasesUpdate?.Invoke();
        }

        public CanvasScript GetCanvasByLayerIndex(int layerIndex)
        {
            return LayerCanvases.ToArray()[layerIndex];
        }

        public LayerMetadata[] LayerCanvasesSerialized()
        {
            var layers = LayerCanvases.ToArray();
            var meta = new LayerMetadata[layers.Count()];
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                meta[i] = new LayerMetadata
                {
                    Visible = layer.gameObject.activeSelf,
                    Name = layer.name
                };
            }
            return meta;
        }

        public AnimationMetadata AnimationTracksSerialized()
        {
            var meta = new AnimationMetadata();
            var layers = LayerCanvases.ToArray();
            meta.Tracks = new AnimationTrackMetadata[layers.Length];

            var timeline = animationUI_manager.Timeline;
            List<int> activeTrackIndexes = animationUI_manager.ActiveTrackIndexes();
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                var frameLengthsFound = new List<int>();
                for (var f = 0; f < timeline[activeTrackIndexes[i]].Frames.Count; f++)
                {
                    if (f > 0)
                    {
                        if (timeline[activeTrackIndexes[i]].Frames[f].Canvas.Equals(timeline[activeTrackIndexes[i]].Frames[f - 1].Canvas))
                        {
                            frameLengthsFound[frameLengthsFound.Count - 1]++;
                        }
                        else
                        {
                            frameLengthsFound.Add(1);
                        }
                    }
                    else
                    {
                        frameLengthsFound.Add(1);
                    }
                }
                meta.Tracks[i] = new AnimationTrackMetadata
                {
                    Visible = layer.gameObject.activeSelf,
                    Name = layer.name,
                    frameLengths = frameLengthsFound
                };
            }

            meta.numFrames = activeTrackIndexes.Count;
            return meta;
        }
    }

} // namespace TiltBrush
