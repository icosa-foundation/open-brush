// Copyright 2026 The Open Brush Authors
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

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class QuillLibraryPanel : ModalPanel
    {
        [Header("Quill Library")]
        [SerializeField] private TextMeshPro m_PanelText;
        [SerializeField] private string m_PanelTitle = "Quill Library";
        [SerializeField] private TextMeshPro m_InfoText;
        [SerializeField] private GameObject m_NoQuillData;
        [SerializeField] private GameObject m_NoImmData;
        [SerializeField] private GameObject m_RefreshingSpinner;
        [SerializeField] private GameObject m_ConfirmLoadPopUpPrefab;

        [Header("Selection & Action Controls")]
        [SerializeField] private GameObject m_ActionControlsContainer;
        [SerializeField] private ActionButton m_LoadButton;
        [SerializeField] private ActionButton m_MergeButton;
        [SerializeField] private GameObject m_ChapterControls;
        [SerializeField] private TextMeshPro m_ChapterLabel;
        [SerializeField] private QuillChapterNavButton m_PrevChapterButton;
        [SerializeField] private QuillChapterNavButton m_NextChapterButton;
        [SerializeField] private GameObject m_ChapterLoadingSpinner;

        private QuillFileCatalog m_Catalog;
        private QuillFileInfo m_SelectedFile;
        private bool m_IsDetectingChapters;

        private List<BaseButton> m_IconButtons;
        private QuillFileButton[] m_FileButtons;
        private int m_EnabledCount;

        protected override List<BaseButton> Icons => m_IconButtons;

        public override bool IsInButtonMode(ModeButton button)
        {
            QuillSourceButton sourceButton = button as QuillSourceButton;
            if (sourceButton == null || m_Catalog == null)
            {
                return false;
            }

            return (sourceButton.m_ButtonType == QuillSourceButton.Type.QuillProjects &&
                m_Catalog.CurrentSourceDirectory == QuillFileCatalog.SourceDirectory.QuillProjects) ||
                (sourceButton.m_ButtonType == QuillSourceButton.Type.Imm &&
                m_Catalog.CurrentSourceDirectory == QuillFileCatalog.SourceDirectory.Imm);
        }

        public void ButtonPressed(QuillSourceButton.Type buttonType, QuillSourceButton button)
        {
            if (m_Catalog == null)
            {
                return;
            }

            switch (buttonType)
            {
                case QuillSourceButton.Type.QuillProjects:
                    m_Catalog.SetSourceDirectory(QuillFileCatalog.SourceDirectory.QuillProjects);
                    break;
                case QuillSourceButton.Type.Imm:
                    m_Catalog.SetSourceDirectory(QuillFileCatalog.SourceDirectory.Imm);
                    break;
            }

            // Clear selection when switching directories
            m_SelectedFile = null;
            ResetPageIndex();
            RefreshPage();
            button.UpdateVisuals();
        }

        /// <summary>
        /// Shows a confirmation popup before loading a Quill file that will clear the scene.
        /// Requires m_ConfirmLoadPopUpPrefab to be set in the inspector.
        /// </summary>
        public void ShowConfirmLoadPopUp()
        {
            if (m_ConfirmLoadPopUpPrefab == null)
            {
                Debug.LogError("QuillLibraryPanel: m_ConfirmLoadPopUpPrefab is not set.");
                return;
            }

            CreatePopUp(
                m_ConfirmLoadPopUpPrefab, Vector3.zero,
                false, false, 0, 0,
                SketchControlsScript.GlobalCommands.LoadQuillFile);
        }

        /// <summary>
        /// Selects a file for action. Called by QuillFileButton when clicked.
        /// </summary>
        public void SelectFile(QuillFileInfo file)
        {
            if (m_SelectedFile == file) return;

            // Clear previous selection visual state
            if (m_SelectedFile != null)
            {
                UpdateFileButtonSelection(m_SelectedFile, false);
            }

            m_SelectedFile = file;

            // Set new selection visual state
            if (m_SelectedFile != null)
            {
                UpdateFileButtonSelection(m_SelectedFile, true);
                StartCoroutine(DetectChaptersForSelectedFile());
            }

            RefreshActionControls();
        }

        private void UpdateFileButtonSelection(QuillFileInfo fileInfo, bool selected)
        {
            if (m_FileButtons == null) return;

            foreach (var button in m_FileButtons)
            {
                if (button.QuillFile != null &&
                    button.QuillFile.FullPath == fileInfo.FullPath)
                {
                    button.IsSelected = selected;
                    break;
                }
            }
        }

        private IEnumerator DetectChaptersForSelectedFile()
        {
            if (m_SelectedFile == null) yield break;

            m_IsDetectingChapters = true;
            if (m_ChapterLoadingSpinner != null)
                m_ChapterLoadingSpinner.SetActive(true);
            RefreshActionControls();

            // For IMM files, add a longer delay to allow UI to update first
            if (m_SelectedFile.SourceType == QuillSourceType.Imm)
            {
                yield return new WaitForSeconds(0.1f); // Let UI show spinner
            }
            else
            {
                yield return null; // Just yield one frame for Quill files
            }

            var startTime = System.DateTime.Now;

            try
            {
                // This triggers lazy chapter detection
                int chapterCount = m_SelectedFile.ChapterCount;
                var elapsed = (System.DateTime.Now - startTime).TotalMilliseconds;

                // Set default chapter if not already set
                if (m_SelectedFile.SelectedChapterIndex < 0)
                {
                    m_SelectedFile.SelectedChapterIndex = 0;
                }
            }
            catch (System.Exception ex)
            {
                var elapsed = (System.DateTime.Now - startTime).TotalMilliseconds;
                Debug.LogError($"Failed to detect chapters for {m_SelectedFile.DisplayName} after {elapsed:F0}ms: {ex}");
            }

            m_IsDetectingChapters = false;
            if (m_ChapterLoadingSpinner != null)
                m_ChapterLoadingSpinner.SetActive(false);
            RefreshActionControls();
        }

        private void RefreshActionControls()
        {
            bool hasSelection = m_SelectedFile != null;
            bool isDetecting = m_IsDetectingChapters;

            // Show/hide action controls container
            if (m_ActionControlsContainer != null)
                m_ActionControlsContainer.SetActive(hasSelection);

            if (!hasSelection) return;

            // Enable/disable action buttons
            if (m_LoadButton != null)
                m_LoadButton.SetButtonAvailable(!isDetecting);
            if (m_MergeButton != null)
                m_MergeButton.SetButtonAvailable(!isDetecting);

            // Show/hide chapter controls - use optimistic check first for better performance
            bool hasMultipleChapters = false;
            if (!isDetecting)
            {
                if (m_SelectedFile.SourceType == QuillSourceType.Quill)
                {
                    // Quill detection is fast
                    hasMultipleChapters = m_SelectedFile.HasMultipleChapters;
                }
                else
                {
                    // IMM detection is slow - use optimistic approach during detection
                    hasMultipleChapters = m_SelectedFile.HasMultipleChaptersOptimistic;
                }
            }

            if (m_ChapterControls != null)
                m_ChapterControls.SetActive(hasMultipleChapters);

            if (hasMultipleChapters)
            {
                RefreshChapterControls();
            }
        }

        private void RefreshChapterControls()
        {
            if (m_SelectedFile == null || !m_SelectedFile.HasMultipleChapters) return;

            int count = m_SelectedFile.ChapterCount;
            int current = m_SelectedFile.SelectedChapterIndex < 0 ? 0 : m_SelectedFile.SelectedChapterIndex;

            if (m_ChapterLabel != null)
                m_ChapterLabel.text = $"Chapter {current + 1} / {count}";

            // Enable/disable navigation buttons
            if (m_PrevChapterButton != null)
                m_PrevChapterButton.SetButtonAvailable(count > 1);
            if (m_NextChapterButton != null)
                m_NextChapterButton.SetButtonAvailable(count > 1);
        }

        // Action button handlers - called by UI buttons via inspector
        public void OnLoadButtonPressed()
        {
            if (m_SelectedFile == null) return;

            Quill.PendingLoadOptions = new Quill.QuillLoadOptions
            {
                Path = m_SelectedFile.FullPath,
                ChapterIndex = m_SelectedFile.SelectedChapterIndex,
            };
            SketchControlsScript.m_Instance.IssueGlobalCommand(
                SketchControlsScript.GlobalCommands.LoadQuillConfirmUnsaved, 0, 0);
        }

        public void OnMergeButtonPressed()
        {
            if (m_SelectedFile == null) return;

            try
            {
                Quill.Load(m_SelectedFile.FullPath, chapterIndex: m_SelectedFile.SelectedChapterIndex);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to merge '{m_SelectedFile.FullPath}': {ex}");
            }
        }

        public void OnPrevChapterPressed()
        {
            if (m_SelectedFile?.HasMultipleChapters != true) return;

            int count = m_SelectedFile.ChapterCount;
            int current = m_SelectedFile.SelectedChapterIndex < 0 ? 0 : m_SelectedFile.SelectedChapterIndex;
            m_SelectedFile.SelectedChapterIndex = (current - 1 + count) % count;
            RefreshChapterControls();
        }

        public void OnNextChapterPressed()
        {
            if (m_SelectedFile?.HasMultipleChapters != true) return;

            int count = m_SelectedFile.ChapterCount;
            int current = m_SelectedFile.SelectedChapterIndex < 0 ? 0 : m_SelectedFile.SelectedChapterIndex;
            m_SelectedFile.SelectedChapterIndex = (current + 1) % count;
            RefreshChapterControls();
        }

        protected override void Awake()
        {
            base.Awake();

            EnsureCatalog();
            if (m_Catalog != null)
            {
                m_Catalog.CatalogChanged += OnCatalogChanged;
            }
            else
            {
                Debug.LogError("QuillLibraryPanel requires a scene-level QuillFileCatalog instance.");
            }
        }

        private void OnDestroy()
        {
            if (m_Catalog != null)
            {
                m_Catalog.CatalogChanged -= OnCatalogChanged;
            }
        }

        protected override void OnStart()
        {
            m_FileButtons = m_Mesh.GetComponentsInChildren<QuillFileButton>(includeInactive: true);
            m_IconButtons = new List<BaseButton>(m_FileButtons.Length);
            foreach (var button in m_FileButtons)
            {
                m_IconButtons.Add(button);
                button.gameObject.SetActive(false);
            }

            // Initialize action controls state
            RefreshActionControls();

            ResetPageIndex();
            RefreshPage();
        }

        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();
            m_CurrentPageFlipState = PageFlipState.TransitionIn;

            if (m_EnabledCount > 0 && !App.PlatformConfig.UseFileSystemWatcher)
            {
                m_Catalog?.ForceCatalogScan();
            }

            m_EnabledCount++;
            RefreshPage();
        }

        private void Update()
        {
            BaseUpdate();
            PageFlipUpdate();

            if (m_RefreshingSpinner != null)
            {
                m_RefreshingSpinner.SetActive(m_Catalog != null && m_Catalog.IsScanning);
            }
        }

        protected override void RefreshPage()
        {
            if (m_PanelText != null)
            {
                string suffix = m_Catalog != null &&
                    m_Catalog.CurrentSourceDirectory == QuillFileCatalog.SourceDirectory.Imm
                    ? "IMM"
                    : "Quill";
                m_PanelText.text = $"{m_PanelTitle} - {suffix}";
            }

            if (m_FileButtons == null || m_FileButtons.Length == 0 || m_Catalog == null)
            {
                m_NumPages = 1;
                UpdateEmptyState();
                base.RefreshPage();
                return;
            }

            m_NumPages = GetPageCount();

            for (int i = 0; i < m_FileButtons.Length; ++i)
            {
                int catalogIndex = m_IndexOffset + i;
                bool visible = catalogIndex < m_Catalog.ItemCount;
                var button = m_FileButtons[i];
                if (visible)
                {
                    var fileInfo = m_Catalog.GetFileAtIndex(catalogIndex);
                    button.QuillFile = fileInfo;
                    button.SetButtonAvailable(true);

                    // Update selection visual state
                    button.IsSelected = (m_SelectedFile != null &&
                        m_SelectedFile.FullPath == fileInfo.FullPath);
                }
                button.gameObject.SetActive(visible);
            }

            UpdateEmptyState();

            if (m_InfoText != null)
            {
                m_InfoText.text = $"{m_Catalog.ItemCount} Files";
            }

            RefreshActionControls();
            base.RefreshPage();
        }

        private void UpdateEmptyState()
        {
            bool hasCatalog = m_Catalog != null;
            bool hasFiles = hasCatalog && m_Catalog.ItemCount > 0;
            bool isImmSource = hasCatalog &&
                m_Catalog.CurrentSourceDirectory == QuillFileCatalog.SourceDirectory.Imm;

            if (m_NoQuillData != null)
            {
                m_NoQuillData.SetActive(!hasFiles && !isImmSource);
            }

            if (m_NoImmData != null)
            {
                m_NoImmData.SetActive(!hasFiles && isImmSource);
            }
        }

        private int GetPageCount()
        {
            if (m_Catalog == null || m_FileButtons == null || m_FileButtons.Length == 0)
            {
                return 1;
            }

            if (m_Catalog.ItemCount == 0)
            {
                return 1;
            }

            return ((m_Catalog.ItemCount - 1) / m_FileButtons.Length) + 1;
        }

        private void OnCatalogChanged()
        {
            // Verify selection is still valid after catalog refresh
            if (m_SelectedFile != null && m_Catalog != null)
            {
                bool fileStillExists = false;
                for (int i = 0; i < m_Catalog.ItemCount; i++)
                {
                    var file = m_Catalog.GetFileAtIndex(i);
                    if (file.FullPath == m_SelectedFile.FullPath)
                    {
                        fileStillExists = true;
                        // Update reference to refreshed file info
                        m_SelectedFile = file;
                        break;
                    }
                }

                if (!fileStillExists)
                {
                    m_SelectedFile = null;
                }
            }

            if (PageIndex > GetPageCount() - 1)
            {
                GotoPage(0);
            }

            RefreshPage();
        }

        private void EnsureCatalog()
        {
            m_Catalog = QuillFileCatalog.Instance;
        }

        public void SetInitialSearchText(KeyboardPopupButton btn)
        {
            KeyboardPopUpWindow.m_InitialText = QuillFileCatalog.Instance.SearchText;
        }
    }
}
