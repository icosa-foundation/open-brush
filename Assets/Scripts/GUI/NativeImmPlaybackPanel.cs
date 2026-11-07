// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using TMPro;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>Scene-level playback controls for an IMM opened as the current sketch.</summary>
    public class NativeImmPlaybackPanel : BasePanel
    {
        [SerializeField] private TMP_Text m_ChapterLabel;
        [SerializeField] private TMP_Text m_ViewpointLabel;
        [SerializeField] private QuillChapterNavButton m_PrevChapterButton;
        [SerializeField] private QuillChapterNavButton m_NextChapterButton;
        [SerializeField] private QuillChapterNavButton m_PrevViewpointButton;
        [SerializeField] private QuillChapterNavButton m_NextViewpointButton;
        [SerializeField] private QuillChapterNavButton m_ContinueButton;

        /// <summary>Open from the wand using the normal floating-panel placement.</summary>
        public static bool Show()
        {
            if (!NativeImmImport.IsActive || NativeImmImport.IsLoading ||
                PanelManager.m_Instance == null || App.VrSdk == null ||
                !App.VrSdk.IsHmdInitialized() || App.Config.m_SdkMode == SdkMode.Monoscopic ||
                InputManager.m_Instance == null) return false;
            var wand = InputManager.m_Instance.GetController(InputManager.ControllerName.Wand);
            if (wand == null) return false;
            SketchControlsScript.m_Instance.OpenPanelOfType(PanelType.NativeImmPlayback,
                TrTransform.FromTransform(wand));
            return true;
        }

        private void Update()
        {
            BaseUpdate();
            bool navigating = NativeImmImport.IsChangingChapter;
            bool ready = NativeImmImport.IsActive && !NativeImmImport.IsLoading && !navigating;
            bool chapters = ready && NativeImmImport.ChapterCount > 1;
            m_PrevChapterButton.SetButtonAvailable(chapters);
            m_NextChapterButton.SetButtonAvailable(chapters);
            bool viewpoints = ready && NativeImmImport.ViewpointCount > 1;
            m_PrevViewpointButton.SetButtonAvailable(viewpoints);
            m_NextViewpointButton.SetButtonAvailable(viewpoints);
            m_ContinueButton.RefreshPlaybackState();

            m_ChapterLabel.text = NativeImmImport.IsLoading ? "Loading IMM…"
                : navigating ? "Changing chapter…"
                : $"Chapter {(NativeImmImport.CurrentChapterIndex ?? 0) + 1} / {NativeImmImport.ChapterCount}";
            int index = NativeImmImport.CurrentViewpointIndex;
            m_ViewpointLabel.text = index >= 0
                ? $"Viewpoint {index + 1} / {NativeImmImport.ViewpointCount}"
                : $"Viewpoint — / {NativeImmImport.ViewpointCount}";
        }
    }
}
