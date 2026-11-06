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
using UnityEngine;
using UnityEngine.Serialization;

namespace TiltBrush
{
    public class ReferencePanelSoundClipTab : ReferencePanelTab
    {

        // Subclass used to display a SoundClip button within the reference tab.
        public class AudioIcon : ReferenceIcon
        {
            public ReferencePanel Parent { get; set; }
            public bool TextureAssigned { get; set; }

            public SoundClipButton SoundClipButton
            {
                get { return Button as SoundClipButton; }
            }

            public override void Refresh(int catalogIndex)
            {
                Button.SetButtonTexture(Parent.UnknownImageTexture, 1);

                var soundClip = SoundClipCatalog.Instance.GetSoundClipAtIndex(catalogIndex);
                SoundClipButton.SoundClip = soundClip;
                SoundClipButton.RefreshDescription();

                // init the icon according to availability of sound clip
                if (soundClip != null)
                {
                    Button.gameObject.SetActive(true);
                    TextureAssigned = false;
                }
                else
                {
                    Button.gameObject.SetActive(false);
                    TextureAssigned = true;
                }
            }
        }

        [SerializeField] private GameObject m_SoundClipControls;
        [SerializeField] private BoxCollider m_SoundClipControlsCollider;
        [SerializeField] private GameObject m_Preview;
        [SerializeField] private SoundClipPositionSlider m_Scrubber;
        [SerializeField] private ActionToggleButton m_LoopToggle;
        [SerializeField] private ActionToggleButton m_SpatialBlendToggle;
        [SerializeField] private float m_SoundClipSkipTime = 10f;
        [SerializeField] private Texture2D m_ErrorTexture;
        [SerializeField] private Texture2D m_LoadingTexture;

        private bool m_AllIconTexturesAssigned;
        private SoundClipWidget m_SelectedSoundClipWidget;
        private Material m_PreviewMaterial;
        private SoundClip.SoundClipController m_PreviewController;
        private bool m_PreviewReady;
        private bool m_PreviewLoading;
        private string m_PreviewError;
        private Texture2D m_PreviewWaveform;
        private bool m_TabActive;

        // Track authored edits at the UI boundary. Controller setters also serve loading,
        // duplication and undo, which must not create additional unsaved changes.
        [System.Reflection.Obfuscation(Exclude = true)]
        public bool SelectedSoundClipIsPlaying
        {
            get => (SelectedSoundClip != null)
                ? SelectedSoundClip.Playing
                : false;
            set
            {
                if (SelectedSoundClip != null)
                {
                    bool previous = SelectedSoundClip.Playing;
                    SelectedSoundClip.Playing = value;
                    if (SelectedSoundClip.Playing != previous)
                        SketchMemoryScript.m_Instance.MarkUnrecordedChange();
                }
            }
        }

        [System.Reflection.Obfuscation(Exclude = true)]
        public float SelectedSoundClipVolume
        {
            get => SelectedSoundClip?.Volume ?? 0f;
            set
            {
                if (SelectedSoundClip != null)
                {
                    float previous = SelectedSoundClip.Volume;
                    SelectedSoundClip.Volume = value;
                    if (SelectedSoundClip.Volume != previous)
                        SketchMemoryScript.m_Instance.MarkUnrecordedChange();
                }
            }
        }

        public void HandleLoopToggle(ActionToggleButton btn)
        {
            SoundClip.SoundClipController soundClip = SelectedSoundClip;
            if (soundClip != null)
            {
                var previous = soundClip.Loop;
                soundClip.Loop = btn.ToggleState;
                if (soundClip.Loop != previous)
                    SketchMemoryScript.m_Instance.MarkUnrecordedChange();
            }
        }

        public void HandleSpatialBlendToggle(ActionToggleButton btn)
        {
            SoundClip.SoundClipController soundClip = SelectedSoundClip;
            if (soundClip != null)
            {
                var previous = soundClip.SpatialBlend;
                soundClip.SpatialBlend = btn.ToggleState ? 1f : 0f;
                if (soundClip.SpatialBlend != previous)
                    SketchMemoryScript.m_Instance.MarkUnrecordedChange();
            }
        }

        public float SelectedSoundClipMinDistance
        {
            get => SelectedSoundClip?.MinDistance ?? 1f;
            set
            {
                if (SelectedSoundClip != null)
                {
                    float previous = SelectedSoundClip.MinDistance;
                    SelectedSoundClip.MinDistance = value;
                    if (SelectedSoundClip.MinDistance != previous)
                        SketchMemoryScript.m_Instance.MarkUnrecordedChange();
                }
            }
        }

        public float SelectedSoundClipMaxDistance
        {
            get => SelectedSoundClip?.MaxDistance ?? 500f;
            set
            {
                if (SelectedSoundClip != null)
                {
                    float previous = SelectedSoundClip.MaxDistance;
                    SelectedSoundClip.MaxDistance = value;
                    if (SelectedSoundClip.MaxDistance != previous)
                        SketchMemoryScript.m_Instance.MarkUnrecordedChange();
                }
            }
        }

        public override IReferenceItemCatalog Catalog => SoundClipCatalog.Instance;
        public override ReferenceButton.Type ReferenceButtonType => ReferenceButton.Type.SoundClips;
        protected override Type ButtonType => typeof(SoundClipButton);
        protected override Type IconType => typeof(AudioIcon);
        protected SoundClip.SoundClipController SelectedSoundClip => m_SelectedSoundClipWidget != null ? m_SelectedSoundClipWidget.SoundClipController : null;

        void RefreshSoundClipControlsVisibility()
        {
            if (m_SoundClipControls != null)
            {
                bool widgetActive = WidgetManager.m_Instance != null &&
                    WidgetManager.m_Instance.AnySoundClipWidgetActive;
                m_SoundClipControls.SetActive(m_TabActive && widgetActive);
            }
        }

        public override void OnTabEnable()
        {
            m_TabActive = true;
            RefreshSoundClipControlsVisibility();
        }

        public override void OnTabDisable()
        {
            m_TabActive = false;
            RefreshSoundClipControlsVisibility();
        }

        public override void RefreshTab(bool selected)
        {
            base.RefreshTab(selected);
            if (selected)
            {
                m_AllIconTexturesAssigned = false;
            }
            m_TabActive = selected;
            RefreshSoundClipControlsVisibility();
        }

        public override void InitTab()
        {
            base.InitTab();
            foreach (var icon in m_Icons)
            {
                ((AudioIcon)icon).Parent = GetComponentInParent<ReferencePanel>();
            }
            OnTabDisable();
            App.Switchboard.SoundClipWidgetActivated += OnSoundClipWidgetActivated;
            m_PreviewMaterial = m_Preview.GetComponent<Renderer>().material;
            m_PreviewMaterial.mainTexture = Texture2D.blackTexture;
        }

        public void OnSoundClipWidgetActivated(SoundClipWidget widget)
        {
            m_SelectedSoundClipWidget = widget;
            RefreshPlaybackPreview(force: true);
            m_Scrubber.SoundClipWidget = widget;
            RefreshPlaybackToggles();

            RefreshSoundClipControlsVisibility();
        }

        private void OnDestroy()
        {
            if (App.Switchboard != null)
                App.Switchboard.SoundClipWidgetActivated -= OnSoundClipWidgetActivated;
            if (m_PreviewWaveform != null) Destroy(m_PreviewWaveform);
        }

        private void RefreshPlaybackPreview(bool force = false)
        {
            var controller = SelectedSoundClip;
            bool ready = controller is { Initialized: true };
            bool loading = controller is { IsLoading: true };
            string error = controller?.Error;
            // Generate a waveform only on selection/readiness changes, not on every panel frame.
            if (!force && controller == m_PreviewController && ready == m_PreviewReady &&
                loading == m_PreviewLoading && error == m_PreviewError) return;
            m_PreviewController = controller;
            m_PreviewReady = ready;
            m_PreviewLoading = loading;
            m_PreviewError = error;
            if (m_PreviewWaveform != null)
            {
                Destroy(m_PreviewWaveform);
                m_PreviewWaveform = null;
            }
            if (error != null)
            {
                m_PreviewMaterial.mainTexture = m_ErrorTexture;
            }
            else if (loading)
            {
                m_PreviewMaterial.mainTexture = m_LoadingTexture;
            }
            else if (ready)
            {
                float aspect = m_Preview.transform.localScale.x / m_Preview.transform.localScale.y;
                m_PreviewWaveform = m_SelectedSoundClipWidget.SoundClip.GetWaveform(
                    0.8f, Color.white, controller.m_SoundClipAudioSource.clip, aspect);
                m_PreviewMaterial.mainTexture = m_PreviewWaveform != null
                    ? m_PreviewWaveform : m_SelectedSoundClipWidget.SoundClip.Thumbnail;
            }
            else
            {
                m_PreviewMaterial.mainTexture = Texture2D.blackTexture;
            }
        }

        private void RefreshPlaybackToggles()
        {
            var controller = SelectedSoundClip;
            m_LoopToggle.SetButtonAvailable(controller != null);
            m_SpatialBlendToggle.SetButtonAvailable(controller != null);
            bool loop = controller is { Loop: true };
            bool spatial = controller is { SpatialBlend: > 0f };
            if (m_LoopToggle.ToggleState != loop) m_LoopToggle.ToggleState = loop;
            if (m_SpatialBlendToggle.ToggleState != spatial) m_SpatialBlendToggle.ToggleState = spatial;
        }

        public override void UpdateTab()
        {
            base.UpdateTab();
            RefreshPlaybackPreview();
            RefreshPlaybackToggles();
            if (!m_AllIconTexturesAssigned)
            {
                m_AllIconTexturesAssigned = true;

                // Poll catalog until icons have loaded
                for (int i = 0; i < m_Icons.Length; ++i)
                {
                    var audioIcon = m_Icons[i] as AudioIcon;
                    if (audioIcon is { TextureAssigned: false } && audioIcon.Button.gameObject.activeSelf)
                    {
                        int catalogIndex = m_IndexOffset + i;

                        var soundClip = SoundClipCatalog.Instance.GetSoundClipAtIndex(catalogIndex);
                        if (soundClip != null)
                        {
                            if (!string.IsNullOrEmpty(soundClip.Error))
                            {
                                audioIcon.Button.SetButtonTexture(m_ErrorTexture,
                                    m_ErrorTexture.width / m_ErrorTexture.height);
                                audioIcon.TextureAssigned = true;
                                audioIcon.SoundClipButton.SetDescriptionText(soundClip.HumanName, "Could not load sound clip.");
                                audioIcon.SoundClipButton.SetButtonAvailable(false);
                            }
                            else if (soundClip.IsInitialized)
                            {
                                audioIcon.Button.SetButtonTexture(soundClip.Thumbnail, soundClip.Aspect);
                                audioIcon.TextureAssigned = true;
                            }
                            else
                            {
                                audioIcon.Button.SetButtonTexture(m_LoadingTexture,
                                    m_LoadingTexture.width / m_LoadingTexture.height);
                                audioIcon.TextureAssigned = true;
                            }
                        }
                        else
                        {
                            m_AllIconTexturesAssigned = false;
                        }
                    }
                }
            }
        }

        public override void OnUpdateGazeBehavior(Color panelColor, bool gazeActive, bool available)
        {
            base.OnUpdateGazeBehavior(panelColor, gazeActive, available);
            bool? buttonsGrayscale = null;
            if (!gazeActive)
            {
                buttonsGrayscale = true;
            }
            else if (available)
            {
                buttonsGrayscale = false;
            }
            else
            {
                // Don't mess with grayscale-ness
            }

            if (buttonsGrayscale != null)
            {
                foreach (var icon in m_Icons)
                {
                    icon.Button.SetButtonGrayscale(buttonsGrayscale.Value);
                }
            }
        }

        public override bool RaycastAgainstMeshCollider(Ray ray, out RaycastHit hitInfo, float dist)
        {
            if (base.RaycastAgainstMeshCollider(ray, out hitInfo, dist))
            {
                return true;
            }
            if (m_SoundClipControlsCollider == null)
            {
                return false;
            }
            return m_SoundClipControlsCollider.Raycast(ray, out hitInfo, dist);
        }

        [System.Reflection.Obfuscation(Exclude = true)]
        public void SkipBack()
        {
            if (SelectedSoundClip == null)
            {
                return;
            }
            float time = Mathf.Clamp(SelectedSoundClip.Time - m_SoundClipSkipTime, 0, SelectedSoundClip.Length);
            if (time == SelectedSoundClip.Time) return;
            SelectedSoundClip.Time = time;
            SketchMemoryScript.m_Instance.MarkUnrecordedChange();
        }

        [System.Reflection.Obfuscation(Exclude = true)]
        public void SkipForward()
        {
            if (SelectedSoundClip == null)
            {
                return;
            }
            float time = Mathf.Clamp(SelectedSoundClip.Time + m_SoundClipSkipTime, 0, SelectedSoundClip.Length);
            if (time == SelectedSoundClip.Time) return;
            SelectedSoundClip.Time = time;
            SketchMemoryScript.m_Instance.MarkUnrecordedChange();
        }
    }
} // namespace TiltBrush
