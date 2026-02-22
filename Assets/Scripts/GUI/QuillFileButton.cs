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

using System;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class QuillFileButton : BaseButton
    {
        [SerializeField] private TextMeshPro m_LabelText;
        [SerializeField] private TextMeshPro m_DetailText;
        [SerializeField] private bool m_MergeMode;

        [Header("Selection Visual State")]
        [SerializeField] private GameObject m_SelectionBorder;
        [SerializeField] private Renderer m_BackgroundRenderer;
        [SerializeField] private Material m_DefaultMaterial;
        [SerializeField] private Material m_SelectedMaterial;

        private QuillFileInfo m_QuillFile;
        private bool m_IsSelected;

        public QuillFileInfo QuillFile
        {
            get => m_QuillFile;
            set
            {
                m_QuillFile = value;
                RefreshDescription();
            }
        }

        public bool IsSelected
        {
            get => m_IsSelected;
            set
            {
                if (m_IsSelected == value) return;
                m_IsSelected = value;
                UpdateSelectionVisuals();
            }
        }

        private void UpdateSelectionVisuals()
        {
            if (m_SelectionBorder != null)
                m_SelectionBorder.SetActive(m_IsSelected);

            if (m_BackgroundRenderer != null && m_DefaultMaterial != null && m_SelectedMaterial != null)
            {
                m_BackgroundRenderer.material = m_IsSelected ? m_SelectedMaterial : m_DefaultMaterial;
            }

            // Subtle scale effect for selection
            transform.localScale = m_IsSelected ? Vector3.one * 1.02f : Vector3.one;
        }

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();

            if (m_QuillFile == null || string.IsNullOrEmpty(m_QuillFile.FullPath))
            {
                return;
            }

            // New behavior: Select the file instead of loading it directly
            var libraryPanel = GetComponentInParent<QuillLibraryPanel>();
            if (libraryPanel != null)
            {
                libraryPanel.SelectFile(m_QuillFile);
            }
            else
            {
                Debug.LogWarning("QuillFileButton could not find parent QuillLibraryPanel for selection");
            }
        }

        public void RefreshDescription()
        {
            if (m_QuillFile == null)
            {
                SetLabelText(string.Empty);
                SetDetailText(string.Empty);
                SetDescriptionText(string.Empty);
                SetExtraDescriptionText(string.Empty);
                return;
            }

            SetLabelText(m_QuillFile.DisplayName);
            SetDetailText($"{m_QuillFile.DescriptionLabel}  {m_QuillFile.DetailLabel}");
            SetDescriptionText($"{m_QuillFile.DisplayName}");
            SetExtraDescriptionText($"{m_QuillFile.DescriptionLabel}\n{m_QuillFile.DetailLabel}");
        }

        private void SetLabelText(string value)
        {
            if (m_LabelText == null)
            {
                m_LabelText = GetComponentInChildren<TextMeshPro>();
            }

            if (m_LabelText != null)
            {
                m_LabelText.text = value;
            }
        }

        private void SetDetailText(string value)
        {
            if (m_DetailText != null)
            {
                m_DetailText.text = value;
            }
        }
    }
}
