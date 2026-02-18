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
        private QuillFileCatalog m_Catalog;

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
                    button.QuillFile = m_Catalog.GetFileAtIndex(catalogIndex);
                    button.SetButtonAvailable(true);
                }
                button.gameObject.SetActive(visible);
            }

            UpdateEmptyState();

            if (m_InfoText != null)
            {
                m_InfoText.text = $"{m_Catalog.ItemCount} Files";
            }

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
    }
}
