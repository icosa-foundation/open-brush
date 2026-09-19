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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class SelectionTray : BaseTray
    {
        internal static SelectionTray Instance { get; private set; }

        [SerializeField] private OptionButton m_GroupButton;
        [SerializeField] private Texture2D m_ConvertToSdfTexture;
        [SerializeField] private Texture2D m_EditSdfTexture;
        [SerializeField] private GameObject m_SdfEditorPopupPrefab;

        private SketchControlsScript.GlobalCommands m_DefaultGroupCommand;
        private Texture2D m_DefaultGroupTexture;
        private string m_DefaultGroupDescription;
        private bool m_ContextActionInitialized;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            base.OnDestroy();
        }

        protected override void Start()
        {
            base.Start();
            m_DefaultGroupCommand = m_GroupButton.m_Command;
            m_DefaultGroupTexture = m_GroupButton.ButtonTexture;
            m_DefaultGroupDescription = m_GroupButton.Description;
            m_ContextActionInitialized = true;
            RefreshContextAction();
        }

        protected override void OnSelectionChanged()
        {
            RefreshContextAction();
        }

        private void RefreshContextAction()
        {
            if (!m_ContextActionInitialized || m_GroupButton == null ||
                SelectionManager.m_Instance == null)
            {
                return;
            }

            bool guidesOnly = SelectionManager.m_Instance.SelectionContainsOnlyGuides;
            bool singleSdf = SelectionManager.m_Instance.SelectedSdfGuide != null;
            m_GroupButton.SetContextCommand(
                singleSdf
                    ? SketchControlsScript.GlobalCommands.EditSelectedSdf
                    : guidesOnly
                    ? SketchControlsScript.GlobalCommands.ConvertSelectionToSdf
                    : m_DefaultGroupCommand,
                singleSdf && m_EditSdfTexture != null
                    ? m_EditSdfTexture
                    : guidesOnly && m_ConvertToSdfTexture != null
                    ? m_ConvertToSdfTexture
                    : m_DefaultGroupTexture,
                singleSdf
                    ? "Edit SDF"
                    : guidesOnly ? "Convert to SDF" : m_DefaultGroupDescription);
            m_GroupButton.UpdateVisuals();
        }

        public void OpenSdfEditor()
        {
            SdfStencil stencil = SelectionManager.m_Instance.SelectedSdfGuide;
            BasePanel panel = m_Manager?.GetPanelForPopUps();
            if (stencil == null || panel == null || m_SdfEditorPopupPrefab == null)
            {
                return;
            }

            GameObject popupObject = panel.CreatePopUp(
                m_SdfEditorPopupPrefab, Vector3.zero,
                explicitPosition: false, transition: true,
                sDelayedText: "SDF Components");
            SdfEditorPopup controller = popupObject.AddComponent<SdfEditorPopup>();
            controller.Initialize(stencil);
        }

        public void RepaintSelected()
        {
            var pm = PointerManager.m_Instance;
            SketchMemoryScript.m_Instance.RepaintSelected(
                pm.RebrushOn,
                pm.RecolorOn,
                pm.ResizeOn,
                pm.JitterOn
            );
        }
    }

} // namespace TiltBrush
