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

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TiltBrush
{
    public class SoundClipWidget : Media2dWidget
    {
        // AudioState is used to restore the state of a sound clip when loading, or when a SoundClipWidget is
        // restored from being tossed with an undo.
        private class SoundClipState
        {
            public bool Paused;
            public float Volume;
            public float? Time;
            public bool Loop = true;
            public float SpatialBlend;
            public float MinDistance = 1f;
            public float MaxDistance = 500f;
        }

        [SerializeField] private float m_ConstantWorldSize = 0.5f;
        [SerializeField] private Color m_MinDistanceColor = new Color(0f, 1f, 0.8f, 0.08f);
        [SerializeField] private Color m_MaxDistanceColor = new Color(1f, 0.6f, 0f, 0.05f);

        private SoundClip m_SoundClip;
        private SoundClipState m_InitialState;
        private GameObject m_MinDistanceSphere;
        private GameObject m_MaxDistanceSphere;

        public SoundClip SoundClip
        {
            get { return m_SoundClip; }
        }

        public override float? AspectRatio => 1.0f;

        public TrTransform SaveTransform
        {
            get
            {
                TrTransform xf = TrTransform.FromLocalTransform(transform);
                xf.scale = GetSignedWidgetSize();
                return xf;
            }
        }

        public SoundClip.SoundClipController SoundClipController { get; private set; }

        /// Set audio properties that will be applied once the controller is initialized.
        /// Call after SetSoundClip.
        public void SetAudioProperties(float volume, bool loop = true,
            float spatialBlend = 0f, float minDistance = 1f, float maxDistance = 500f)
        {
            m_InitialState = new SoundClipState
            {
                Volume = volume,
                Loop = loop,
                SpatialBlend = spatialBlend,
                MinDistance = minDistance,
                MaxDistance = maxDistance,
            };
        }

        public void SetSoundClip(SoundClip soundClip)
        {
            m_SoundClip = soundClip;
            m_Size = m_ConstantWorldSize;

            // Create in the main canvas.
            HierarchyUtils.RecursivelySetLayer(transform, App.Scene.MainCanvas.gameObject.layer);
            HierarchyUtils.RecursivelySetMaterialBatchID(transform, m_BatchId);

            Play();
        }

        protected override void OnShow()
        {
            base.OnShow();
            Play();
        }

        public override void RestoreFromToss()
        {
            base.RestoreFromToss();
            Play();
        }

        protected override void OnHide()
        {
            base.OnHide();
            // store off the sound clip state so that if the widget gets shown again it will reset to that.
            if (SoundClipController != null)
            {
                m_InitialState = new SoundClipState
                {
                    Paused = !SoundClipController.Playing,
                    Time = SoundClipController.Time,
                    Volume = SoundClipController.Volume,
                    Loop = SoundClipController.Loop,
                    SpatialBlend = SoundClipController.SpatialBlend,
                    MinDistance = SoundClipController.MinDistance,
                    MaxDistance = SoundClipController.MaxDistance,
                };
                SoundClipController.Dispose();
                SoundClipController = null;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SoundClipController?.Dispose();
            SoundClipController = null;
            if (m_MinDistanceSphere != null) Destroy(m_MinDistanceSphere);
            if (m_MaxDistanceSphere != null) Destroy(m_MaxDistanceSphere);
        }

        private void Play()
        {
            if (m_SoundClip == null || SoundClipController != null)
            {
                return;
            }
            if (SoundClipController == null)
            {
                SoundClipController = m_SoundClip.CreateController(this);

                //SoundClipController.m_SoundClipAudioSource.Play();
                SoundClipController.OnSoundClipInitialized += OnSoundClipInitialized;
            }
            else
            {
                // If instances of the sound clip already exist, don't override with new state.
                m_InitialState = null;
            }
        }

        private void OnSoundClipInitialized()
        {
            UpdateScale();
            if (m_InitialState != null)
            {
                SoundClipController.Volume = m_InitialState.Volume;
                SoundClipController.Loop = m_InitialState.Loop;
                SoundClipController.SpatialBlend = m_InitialState.SpatialBlend;
                SoundClipController.MinDistance = m_InitialState.MinDistance;
                SoundClipController.MaxDistance = m_InitialState.MaxDistance;
                if (m_InitialState.Time.HasValue)
                {
                    SoundClipController.Time = m_InitialState.Time.Value;
                }
                if (m_InitialState.Paused)
                {
                    SoundClipController.Playing = false;
                }
                m_InitialState = null;
            }
            UpdateDistanceVisualization();
        }

        public static List<SoundClipWidget> FromModelWidget(ModelWidget modelWidget)
        {
            var go = modelWidget.gameObject;
            string baseName = go.name.Replace("ModelWidget", "SoundClipWidget");
            var soundClipWidgets = new List<SoundClipWidget>();
            var layer = go.layer;

            foreach (var gltfAudio in go.GetComponentsInChildren<GltfAudioSource>())
            {
                var soundClip = new SoundClip(gltfAudio.AbsoluteFilePath);
                var widget = Object.Instantiate(WidgetManager.m_Instance.SoundClipWidgetPrefab);
                widget.LoadingFromSketch = true;
                widget.transform.parent = App.Instance.m_CanvasTransform;
                widget.transform.localScale = Vector3.one;
                widget.SetAudioProperties(gltfAudio.Gain, gltfAudio.Loop, gltfAudio.SpatialBlend,
                    gltfAudio.MinDistance, gltfAudio.MaxDistance);
                widget.SetSoundClip(soundClip);
                widget.Show(bShow: true, bPlayAudio: false);
                widget.transform.position = gltfAudio.transform.position;
                widget.transform.rotation = gltfAudio.transform.rotation;
                widget.SetCanvas(App.Scene.MainCanvas);
                TiltMeterScript.m_Instance.AdjustMeterWithWidget(widget.GetTiltMeterCost(), up: true);
                HierarchyUtils.RecursivelySetLayer(widget.transform, layer);
                widget.name = baseName;
                WidgetManager.m_Instance.RegisterGrabWidget(widget.gameObject);
                soundClipWidgets.Add(widget);
            }

            modelWidget.Hide();
            WidgetManager.m_Instance.UnregisterGrabWidget(modelWidget.gameObject);
            Destroy(modelWidget.gameObject);
            return soundClipWidgets;
        }

        public static void FromTiltSoundClip(TiltSoundClip tiltSoundClip)
        {
            SoundClipWidget soundClipWidget = Instantiate(WidgetManager.m_Instance.SoundClipWidgetPrefab);
            soundClipWidget.m_LoadingFromSketch = true;
            soundClipWidget.transform.parent = App.Instance.m_CanvasTransform;
            soundClipWidget.transform.localScale = Vector3.one;

            var soundClip = SoundClipCatalog.Instance.GetSoundClipByPersistentPath(tiltSoundClip.FilePath);
            if (soundClip == null)
            {
                soundClip = SoundClip.CreateDummySoundClip();
                ControllerConsoleScript.m_Instance.AddNewLine(
                    $"Could not find sound clip {App.SoundClipLibraryPath()}\\{tiltSoundClip.FilePath}.");
            }
            soundClipWidget.SetSoundClip(soundClip);
            soundClipWidget.m_InitialState = new SoundClipState
            {
                Volume = tiltSoundClip.Volume,
                Paused = tiltSoundClip.Paused,
                Loop = tiltSoundClip.Loop,
                SpatialBlend = tiltSoundClip.SpatialBlend,
                MinDistance = tiltSoundClip.MinDistance,
                MaxDistance = tiltSoundClip.MaxDistance,
            };
            if (tiltSoundClip.Paused)
            {
                soundClipWidget.m_InitialState.Time = tiltSoundClip.Time;
            }
            soundClipWidget.SetSignedWidgetSize(tiltSoundClip.Transform.scale);
            soundClipWidget.Show(bShow: true, bPlayAudio: false);
            soundClipWidget.transform.localPosition = tiltSoundClip.Transform.translation;
            soundClipWidget.transform.localRotation = tiltSoundClip.Transform.rotation;
            if (tiltSoundClip.Pinned)
            {
                soundClipWidget.PinFromSave();
            }
            soundClipWidget.Group = App.GroupManager.GetGroupFromId(tiltSoundClip.GroupId);
            soundClipWidget.SetCanvas(App.Scene.GetOrCreateLayer(tiltSoundClip.LayerId));
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(soundClipWidget.GetTiltMeterCost(), up: true);
            soundClipWidget.UpdateScale();
        }

        override public GrabWidget Clone()
        {
            return Clone(transform.position, transform.rotation, m_Size);
        }

        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            SoundClipWidget clone = Instantiate(WidgetManager.m_Instance.SoundClipWidgetPrefab) as SoundClipWidget;
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.m_LoadingFromSketch = true; // prevents intro animation
            clone.transform.parent = transform.parent;
            clone.SetSoundClip(m_SoundClip);
            clone.SetSignedWidgetSize(size);
            clone.Show(bShow: true, bPlayAudio: false);
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);
            clone.CloneInitialMaterials(this);
            clone.TrySetCanvasKeywordsFromObject(transform);
            return clone;
        }

        public override void Activate(bool bActive)
        {
            base.Activate(bActive);
            if (bActive)
            {
                App.Switchboard.TriggerSoundClipWidgetActivated(this);
                UpdateDistanceVisualization();
            }
            else
            {
                SetDistanceSpheresVisible(false);
            }
        }

        private GameObject CreateDistanceSphere(Color color, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            // Remove the collider — this is visualization only
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var renderer = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;

            // Set up transparency
            mat.SetFloat("_Mode", 3); // transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_ALPHABLEND_ON");
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            return go;
        }

        private void EnsureDistanceSpheres()
        {
            if (m_MinDistanceSphere == null)
            {
                m_MinDistanceSphere = CreateDistanceSphere(m_MinDistanceColor, "MinDistanceSphere");
            }
            if (m_MaxDistanceSphere == null)
            {
                m_MaxDistanceSphere = CreateDistanceSphere(m_MaxDistanceColor, "MaxDistanceSphere");
            }
        }

        private void SetDistanceSpheresVisible(bool visible)
        {
            if (m_MinDistanceSphere != null) m_MinDistanceSphere.SetActive(visible);
            if (m_MaxDistanceSphere != null) m_MaxDistanceSphere.SetActive(visible);
        }

        public void UpdateDistanceVisualization()
        {
            if (SoundClipController == null || SoundClipController.SpatialBlend <= 0f)
            {
                SetDistanceSpheresVisible(false);
                return;
            }

            EnsureDistanceSpheres();

            // Distances are authored in scene/canvas space.
            // The sphere primitive has diameter 1, so local scale = 2 * distance.
            // Divide by the widget's local scale so that the parent (canvas) transform
            // provides the scene-space-to-world-space conversion naturally.
            float widgetLocalScale = Mathf.Max(transform.localScale.x, 0.001f);
            float minDiameter = 2f * SoundClipController.MinDistance / widgetLocalScale;
            float maxDiameter = 2f * SoundClipController.MaxDistance / widgetLocalScale;

            m_MinDistanceSphere.transform.localScale = Vector3.one * minDiameter;
            m_MaxDistanceSphere.transform.localScale = Vector3.one * maxDiameter;

            m_MinDistanceSphere.SetActive(true);
            m_MaxDistanceSphere.SetActive(true);
        }

        protected override void UpdateScale()
        {
            // Maintain constant world-space size regardless of canvas/scene scale.
            float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            transform.localScale = Vector3.one * m_ConstantWorldSize / Mathf.Max(parentScale, 0.001f);

            // Update AudioSource distances: authored values * scene scale = world distances
            if (SoundClipController != null)
            {
                SoundClipController.ApplyDistanceScale(parentScale);
            }
        }

        public override Vector2 GetWidgetSizeRange()
        {
            // Return identical min/max to prevent manual scaling.
            float absSize = Mathf.Max(Mathf.Abs(m_Size), 0.001f);
            return new Vector2(absSize, absSize);
        }

        protected override void SetWidgetSizeInternal(float fScale)
        {
            // Skip Media2dWidget's version which resets transform.localScale to Vector3.one.
            // Only store m_Size for save/load; visual size is controlled by m_ConstantWorldSize.
            var sizeRange = GetWidgetSizeRange();
            m_Size = Mathf.Sign(fScale) * Mathf.Clamp(Mathf.Abs(fScale), sizeRange.x, sizeRange.y);
            UpdateScale();
        }

        public override float GetActivationScore(
            Vector3 vControllerPos, InputManager.ControllerName name)
        {
            // Media2dWidget's version applies a size-based penalty that always returns 0
            // for fixed-size widgets (size == max). Replicate GrabWidget's distance-based
            // scoring directly, plus the ungrabbable-from-inside check.
            if (m_BoxCollider == null) return -1f;

            if (m_UngrabbableFromInside)
            {
                if (PointInCollider(ViewpointScript.Head.position) &&
                    PointInCollider(InputManager.m_Instance.GetBrushControllerAttachPoint().position) &&
                    PointInCollider(InputManager.m_Instance.GetWandControllerAttachPoint().position))
                {
                    return -1f;
                }
            }

            Vector3 vInvTransformedPos = transform.InverseTransformPoint(vControllerPos);
            Vector3 vSize = m_BoxCollider.size * 0.5f;
            vSize.x *= m_BoxCollider.transform.localScale.x;
            vSize.y *= m_BoxCollider.transform.localScale.y;
            vSize.z *= m_BoxCollider.transform.localScale.z;
            float xDiff = vSize.x - Mathf.Abs(vInvTransformedPos.x);
            float yDiff = vSize.y - Mathf.Abs(vInvTransformedPos.y);
            float zDiff = vSize.z - Mathf.Abs(vInvTransformedPos.z);
            if (xDiff > 0.0f && yDiff > 0.0f && zDiff > 0.0f)
            {
                return ((xDiff / vSize.x) * 0.333f) +
                    ((yDiff / vSize.y) * 0.333f) +
                    ((zDiff / vSize.z) * 0.333f);
            }
            return -1.0f;
        }

        public override string GetExportName()
        {
            return Path.GetFileNameWithoutExtension(m_SoundClip.AbsolutePath);
        }

        /// Returns audio settings for GLTF export, reading from the controller when initialized
        /// and falling back to m_InitialState or defaults otherwise.
        public (float volume, bool loop, float spatialBlend, float minDistance, float maxDistance) GetAudioExportSettings()
        {
            if (SoundClipController != null && SoundClipController.Initialized)
            {
                return (SoundClipController.Volume, SoundClipController.Loop,
                    SoundClipController.SpatialBlend, SoundClipController.MinDistance,
                    SoundClipController.MaxDistance);
            }
            if (m_InitialState != null)
            {
                return (m_InitialState.Volume, m_InitialState.Loop,
                    m_InitialState.SpatialBlend, m_InitialState.MinDistance,
                    m_InitialState.MaxDistance);
            }
            return (1f, true, 0f, 1f, 500f);
        }
    }
} // namespace TiltBrush
