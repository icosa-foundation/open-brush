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

        private QuillFileInfo m_QuillFile;

        public QuillFileInfo QuillFile
        {
            get => m_QuillFile;
            set
            {
                m_QuillFile = value;
                RefreshDescription();
            }
        }

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();

            if (m_QuillFile == null || string.IsNullOrEmpty(m_QuillFile.FullPath))
            {
                return;
            }

            if (m_MergeMode)
            {
                // Merge: add Quill strokes to the current scene without clearing
                try
                {
                    Quill.Load(m_QuillFile.FullPath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to import '{m_QuillFile.FullPath}': {ex}");
                }
            }
            else
            {
                // Load: confirm unsaved changes, clear scene, then load
                Quill.PendingLoadPath = m_QuillFile.FullPath;
                SketchControlsScript.m_Instance.IssueGlobalCommand(
                    SketchControlsScript.GlobalCommands.LoadQuillConfirmUnsaved, 0, 0);
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
