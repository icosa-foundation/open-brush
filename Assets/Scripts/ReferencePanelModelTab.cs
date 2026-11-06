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
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class ReferencePanelModelTab : ReferencePanelTab
    {

        public class ModelIcon : ReferenceIcon
        {
            public ModelButton ModelButton
            {
                get { return Button as ModelButton; }
            }
            public override void Refresh(int iCatalog)
            {
                var model = ModelCatalog.m_Instance.GetModelAtIndex(iCatalog);
                ModelButton.SetPreset(model, iCatalog);
            }
        }

        [SerializeField] private GameObject m_ModelControls;
        [SerializeField] private BoxCollider m_ModelControlsCollider;
        [SerializeField] private ModelPositionSlider m_Scrubber;
        [SerializeField] private ActionToggleButton m_LoopToggle;
        [SerializeField] private ActionToggleButton m_SpatialBlendToggle;
        [SerializeField] private OpenListPickerPopupButton m_AnimationSelector;
        [SerializeField] private TextMeshPro m_PlaybackTime;
        [SerializeField] private GameObject m_ChapterControls;
        [SerializeField] private TextMeshPro m_ChapterLabel;
        [SerializeField] private GameObject m_ViewpointControls;
        [SerializeField] private TextMeshPro m_ViewpointLabel;
        [SerializeField] private ModelPathLabel m_ModelPath;
        [SerializeField] private GameObject m_PlaybackButtonsRoot;
        [SerializeField] private FloatPropertySlider m_VolumeSlider;
        [SerializeField] private float m_ModelSkipTime = 10f;

        private ModelWidget m_SelectedModelWidget;
        private BaseButton[] m_PlaybackButtons = new BaseButton[0];
        private bool m_TabActive;
        private float? m_PendingVolume;
        private bool m_AnimationClipsLoaded;
        private ModelPlaybackController SelectedPlayback => m_SelectedModelWidget != null &&
            m_SelectedModelWidget.gameObject.activeInHierarchy ? m_SelectedModelWidget.Playback : null;

        [System.Reflection.Obfuscation(Exclude = true)]
        public bool SelectedModelIsPlaying
        {
            get => SelectedPlayback != null && SelectedPlayback.Playing;
            set
            {
                var playback = SelectedPlayback;
                if (playback == null || !playback.Initialized || playback.IsRestoring) return;
                if (value) playback.Play(); else playback.Pause();
            }
        }

        [System.Reflection.Obfuscation(Exclude = true)]
        public float SelectedModelVolume
        {
            get => m_PendingVolume ?? SelectedPlayback?.Volume ?? 0;
            set
            {
                var playback = SelectedPlayback;
                if (playback == null || !playback.Initialized || !playback.HasAudio) return;
                m_PendingVolume = Mathf.Clamp01(value);
                ApplyPendingVolume();
            }
        }

        public override void InitTab()
        {
            base.InitTab();
            if (m_ModelControls != null)
                m_PlaybackButtons = m_ModelControls.GetComponentsInChildren<BaseButton>(true);
            App.Switchboard.ModelWidgetActivated += OnModelWidgetActivated;
            OnTabDisable();
        }

        private void OnDestroy()
        {
            if (App.Switchboard != null) App.Switchboard.ModelWidgetActivated -= OnModelWidgetActivated;
        }

        public void OnModelWidgetActivated(ModelWidget widget)
        {
            if (m_SelectedModelWidget == widget) return;
            m_SelectedModelWidget = widget;
            m_PendingVolume = null;
            m_AnimationClipsLoaded = false;
            if (m_Scrubber != null) m_Scrubber.ModelWidget = widget;
            RefreshModelControls();
        }

        public override void OnTabEnable()
        {
            base.OnTabEnable();
            m_TabActive = true;
            RefreshModelControls();
        }

        public override void OnTabDisable()
        {
            m_TabActive = false;
            RefreshModelControls();
        }

        public override void UpdateTab()
        {
            base.UpdateTab();
            ApplyPendingVolume();
            RefreshModelControls();
        }

        private void ApplyPendingVolume()
        {
            var playback = SelectedPlayback;
            if (!m_PendingVolume.HasValue || playback == null || !playback.Initialized || playback.IsRestoring) return;
            float volume = m_PendingVolume.Value;
            m_PendingVolume = null;
            playback.SetVolume(volume);
        }

        private void RefreshModelControls()
        {
            var playback = SelectedPlayback;
            bool supported = playback != null && playback.Initialized;
            bool available = supported && !playback.IsRestoring;
            bool brokenApart = m_SelectedModelWidget != null && m_SelectedModelWidget.gameObject.activeInHierarchy &&
                m_SelectedModelWidget.Model != null && !string.IsNullOrEmpty(m_SelectedModelWidget.Subtree);
            if (m_ModelControls != null) m_ModelControls.SetActive(m_TabActive && (supported || brokenApart));
            if (m_Scrubber != null) m_Scrubber.gameObject.SetActive(supported);
            if (m_PlaybackButtonsRoot != null) m_PlaybackButtonsRoot.SetActive(supported);
            if (m_ModelPath != null)
            {
                m_ModelPath.gameObject.SetActive(brokenApart);
                if (brokenApart) m_ModelPath.SetModel(m_SelectedModelWidget.Model, m_SelectedModelWidget.Subtree);
            }
            foreach (var button in m_PlaybackButtons)
                if (button != null) button.SetButtonAvailable(available);
            RefreshImmNavigation(playback, supported, available);
            RefreshAnimationSelector(playback, supported);
            RefreshPlaybackTime(playback, supported);
            if (m_VolumeSlider != null) m_VolumeSlider.gameObject.SetActive(supported && playback.HasAudio);
            if (m_SpatialBlendToggle != null)
            {
                m_SpatialBlendToggle.gameObject.SetActive(supported && playback.CanSpatializeAudio);
                if (supported && playback.CanSpatializeAudio &&
                    m_SpatialBlendToggle.ToggleState != (playback.SpatialBlend > 0))
                    m_SpatialBlendToggle.ToggleState = playback.SpatialBlend > 0;
            }
            if (m_LoopToggle != null)
            {
                m_LoopToggle.gameObject.SetActive(supported && playback.CanLoop);
                if (supported && playback.CanLoop && m_LoopToggle.ToggleState != playback.Loop)
                    m_LoopToggle.ToggleState = playback.Loop;
            }
        }

        private void RefreshImmNavigation(ModelPlaybackController playback, bool supported, bool available)
        {
            bool native = supported && playback.Document != null;
            if (m_ChapterControls != null) m_ChapterControls.SetActive(native && playback.ChapterCount > 1);
            if (native && m_ChapterLabel != null)
            {
                string label = $"Chapter {playback.Chapter + 1} / {playback.ChapterCount}";
                if (m_ChapterLabel.text != label) m_ChapterLabel.text = label;
            }
            int index = -1;
            int count = 0;
            if (native) playback.GetViewpointNavigationState(out index, out count);
            if (m_ViewpointControls != null) m_ViewpointControls.SetActive(native && count > 0);
            if (native && m_ViewpointLabel != null)
            {
                string label = $"Viewpoint {(index < 0 ? "-" : (index + 1).ToString())} / {count}";
                if (m_ViewpointLabel.text != label) m_ViewpointLabel.text = label;
            }
            foreach (var button in m_PlaybackButtons)
                if (button is ModelImmNavigationButton navigation && navigation.IsViewpoint)
                    button.SetButtonAvailable(available && count > 0 && (!navigation.MovesToViewpoint || index >= 0));
        }

        public void CycleChapter(int direction)
        {
            var playback = SelectedPlayback;
            if (playback == null || !playback.Initialized || playback.IsRestoring || playback.ChapterCount < 2) return;
            playback.SetChapter((playback.Chapter + direction + playback.ChapterCount) % playback.ChapterCount);
        }

        public void CycleViewpoint(int direction)
        {
            var playback = SelectedPlayback;
            if (playback == null || !playback.Initialized || playback.IsRestoring || playback.Document == null) return;
            playback.CycleViewpoint(direction);
            RefreshModelControls();
        }

        public void MoveToSelectedViewpoint()
        {
            var playback = SelectedPlayback;
            if (playback == null || !playback.Initialized || playback.IsRestoring || playback.Document == null) return;
            playback.GetViewpointNavigationState(out int index, out _);
            if (index < 0) return;
            try { playback.NavigateToSelectedViewpoint(); }
            catch (InvalidOperationException error) { OutputWindowScript.Error(error.Message); }
            RefreshModelControls();
        }

        private void RefreshPlaybackTime(ModelPlaybackController playback, bool supported)
        {
            if (m_PlaybackTime == null) return;
            m_PlaybackTime.gameObject.SetActive(supported);
            if (!supported) return;
            double length = playback.Length;
            double time = Math.Max(0, Math.Min(m_Scrubber != null ? m_Scrubber.TargetTime : playback.TargetTime, length));
            string label = $"{FormatPlaybackTime(time)} / {FormatPlaybackTime(length)}";
            if (m_PlaybackTime.text != label) m_PlaybackTime.text = label;
        }

        private static string FormatPlaybackTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 100}"
                : $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds / 100}";
        }

        private void RefreshAnimationSelector(ModelPlaybackController playback, bool supported)
        {
            if (m_AnimationSelector == null) return;
            if (!supported) m_AnimationClipsLoaded = false;
            if (supported && !m_AnimationClipsLoaded)
            {
                m_AnimationSelector.m_Items = new List<string>(playback.Clips);
                m_AnimationClipsLoaded = true;
            }
            bool visible = supported && m_AnimationSelector.m_Items.Count > 1;
            m_AnimationSelector.gameObject.SetActive(visible);
            if (!visible) return;
            int index = Math.Max(0, m_AnimationSelector.m_Items.IndexOf(playback.ClipName));
            m_AnimationSelector.ItemIndex = index;
            string label = m_AnimationSelector.m_Items[index];
            if (m_AnimationSelector.ButtonLabel != label) m_AnimationSelector.ButtonLabel = label;
        }

        public void HandleAnimationSelected(OpenListPickerPopupButton button)
        {
            var playback = SelectedPlayback;
            if (playback == null || !playback.Initialized || playback.IsRestoring ||
                button.ItemIndex < 0 || button.ItemIndex >= button.m_Items.Count) return;
            playback.SelectClip(button.m_Items[button.ItemIndex]);
            RefreshModelControls();
        }

        public override bool RaycastAgainstMeshCollider(Ray ray, out RaycastHit hitInfo, float dist)
        {
            if (base.RaycastAgainstMeshCollider(ray, out hitInfo, dist)) return true;
            return m_ModelControls != null && m_ModelControls.activeInHierarchy &&
                m_ModelControlsCollider != null && m_ModelControlsCollider.Raycast(ray, out hitInfo, dist);
        }

        [System.Reflection.Obfuscation(Exclude = true)]
        public void SkipBack() => Skip(-m_ModelSkipTime);

        [System.Reflection.Obfuscation(Exclude = true)]
        public void SkipForward() => Skip(m_ModelSkipTime);

        private void Skip(float seconds)
        {
            var playback = SelectedPlayback;
            if (playback == null || !playback.Initialized || playback.IsRestoring || m_Scrubber == null) return;
            m_Scrubber.RequestSeek(m_Scrubber.TargetTime + seconds);
        }

        public void HandleLoopToggle(ActionToggleButton button)
        {
            var playback = SelectedPlayback;
            if (playback != null && playback.Initialized && playback.CanLoop && !playback.IsRestoring)
                playback.SetLoop(button.ToggleState);
        }

        public void HandleSpatialBlendToggle(ActionToggleButton button)
        {
            var playback = SelectedPlayback;
            if (playback != null && playback.Initialized && playback.CanSpatializeAudio && !playback.IsRestoring)
                playback.SetSpatialBlend(button.ToggleState ? 1f : 0f);
        }

        private int m_LastPageIndexForLoad = -1;
        private string m_LastDirectoryForLoad;

        public override IReferenceItemCatalog Catalog
        {
            get { return ModelCatalog.m_Instance; }
        }
        public override ReferenceButton.Type ReferenceButtonType
        {
            get { return ReferenceButton.Type.Models; }
        }

        protected override Type ButtonType
        {
            get { return typeof(ModelButton); }
        }
        protected override Type IconType
        {
            get { return typeof(ModelIcon); }
        }

        public override void RefreshTab(bool selected)
        {
            base.RefreshTab(selected);
            m_TabActive = selected;
            RefreshModelControls();
            if (selected)
            {
                // Destroy previews so only the thumbnail is visible.
                // Only do this when the page changes, to avoid thrashing the game state.
                var currentDir = Catalog.GetCurrentDirectory();
                if (m_LastPageIndexForLoad != PageIndex || m_LastDirectoryForLoad != currentDir)
                {
                    m_LastPageIndexForLoad = PageIndex;
                    m_LastDirectoryForLoad = currentDir;
                    for (int i = 0; i < m_Icons.Length; i++)
                    {
                        (m_Icons[i].Button as ModelButton).DestroyModelPreview();
                    }
                }
            }
        }
    }
} // namespace TiltBrush
