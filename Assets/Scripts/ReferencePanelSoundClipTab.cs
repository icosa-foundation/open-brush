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
        [SerializeField] private float m_SoundClipSkipTime = 10f;
        [SerializeField] private Texture2D m_ErrorTexture;
        [SerializeField] private Texture2D m_LoadingTexture;

        private bool m_AllIconTexturesAssigned;
        private SoundClipWidget m_SelectedSoundClipWidget;
        private Material m_PreviewMaterial;
        private bool m_TabActive;

        [System.Reflection.Obfuscation(Exclude = true)]
        public bool SelectedSoundClipIsPlaying
        {
            get
            {
                return (SelectedSoundClip != null)
                    ? SelectedSoundClip.Playing
                    : false;
            }
            set
            {
                if (SelectedSoundClip != null)
                {
                    SelectedSoundClip.Playing = !SelectedSoundClip.Playing;
                }
            }
        }

        [System.Reflection.Obfuscation(Exclude = true)]
        public float SelectedSoundClipVolume
        {
            get
            {
                return (SelectedSoundClip != null)
                    ? SelectedSoundClip.Volume
                    : 0f;
            }
            set
            {
                if (SelectedSoundClip != null)
                {
                    SelectedSoundClip.Volume = value;
                }
            }
        }

        public override IReferenceItemCatalog Catalog
        {
            get { return SoundClipCatalog.Instance; }
        }
        public override ReferenceButton.Type ReferenceButtonType
        {
            get { return ReferenceButton.Type.SoundClips; }
        }
        protected override Type ButtonType
        {
            get { return typeof(SoundClipButton); }
        }
        protected override Type IconType
        {
            get { return typeof(AudioIcon); }
        }

        protected SoundClip.SoundClipController SelectedSoundClip
        {
            get
            {
                return m_SelectedSoundClipWidget != null ? m_SelectedSoundClipWidget.SoundClipController : null;
            }
        }

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
                (icon as AudioIcon).Parent = GetComponentInParent<ReferencePanel>();
            }
            OnTabDisable();
            App.Switchboard.SoundClipWidgetActivated += OnSoundClipWidgetActivated;
            m_PreviewMaterial = m_Preview.GetComponent<Renderer>().material;
            m_PreviewMaterial.mainTexture = Texture2D.blackTexture;
        }

        public void OnSoundClipWidgetActivated(SoundClipWidget widget)
        {
            m_SelectedSoundClipWidget = widget;
            if (widget.SoundClipController != null)
            {
                m_PreviewMaterial.mainTexture = widget.SoundClip.Thumbnail;
            }
            m_Preview.transform.localScale = new Vector3(widget.SoundClip.Aspect, 1f, 1f);
            m_Scrubber.SoundClipWidget = widget;
            RefreshSoundClipControlsVisibility();
        }

        public override void UpdateTab()
        {
            base.UpdateTab();
            if (!m_AllIconTexturesAssigned)
            {
                m_AllIconTexturesAssigned = true;

                //poll sketch catalog until icons have loaded
                for (int i = 0; i < m_Icons.Length; ++i)
                {
                    var imageIcon = m_Icons[i] as AudioIcon;
                    if (!imageIcon.TextureAssigned && imageIcon.Button.gameObject.activeSelf)
                    {
                        int catalogIndex = m_IndexOffset + i;

                        var soundClip = SoundClipCatalog.Instance.GetSoundClipAtIndex(catalogIndex);
                        if (soundClip != null)
                        {
                            if (!string.IsNullOrEmpty(soundClip.Error))
                            {
                                imageIcon.Button.SetButtonTexture(m_ErrorTexture,
                                    m_ErrorTexture.width / m_ErrorTexture.height);
                                imageIcon.TextureAssigned = true;
                                imageIcon.SoundClipButton.SetDescriptionText(soundClip.HumanName, "Could not load sound clip.");
                                imageIcon.SoundClipButton.SetButtonAvailable(false);
                            }
                            else if (soundClip.IsInitialized)
                            {
                                imageIcon.Button.SetButtonTexture(soundClip.Thumbnail, soundClip.Aspect);
                                imageIcon.TextureAssigned = true;
                            }
                            else
                            {
                                imageIcon.Button.SetButtonTexture(m_LoadingTexture,
                                    m_LoadingTexture.width / m_LoadingTexture.height);
                                imageIcon.TextureAssigned = true;
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
            SelectedSoundClip.Time = Mathf.Clamp(SelectedSoundClip.Time - m_SoundClipSkipTime, 0, SelectedSoundClip.Length);
        }

        [System.Reflection.Obfuscation(Exclude = true)]
        public void SkipForward()
        {
            if (SelectedSoundClip == null)
            {
                return;
            }
            SelectedSoundClip.Time = Mathf.Clamp(SelectedSoundClip.Time + m_SoundClipSkipTime, 0, SelectedSoundClip.Length);
        }
    }
} // namespace TiltBrush
