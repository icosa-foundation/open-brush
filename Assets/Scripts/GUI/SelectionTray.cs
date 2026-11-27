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
        [SerializeField] private GameObject m_NumericInputPopupPrefab;

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
            bool singleModel = SelectionManager.m_Instance.SelectionIsSingleModel;
            m_GroupButton.SetContextCommand(
                singleSdf
                    ? SketchControlsScript.GlobalCommands.EditSelectedSdf
                    : singleModel
                    ? SketchControlsScript.GlobalCommands.CreateGuideFromSelectedModel
                    : guidesOnly
                    ? SketchControlsScript.GlobalCommands.ConvertSelectionToSdf
                    : m_DefaultGroupCommand,
                singleSdf && m_EditSdfTexture != null
                    ? m_EditSdfTexture
                    : (singleModel || guidesOnly) && m_ConvertToSdfTexture != null
                    ? m_ConvertToSdfTexture
                    : m_DefaultGroupTexture,
                singleSdf
                    ? "Edit SDF"
                    : singleModel
                    ? "Create Guide from Model"
                    : guidesOnly ? "Convert to SDF" : m_DefaultGroupDescription);
            m_GroupButton.UpdateVisuals();
        }

        public void OpenSdfEditor(
            int componentIndex = 0, int dimensionIndex = 0, int page = 0,
            int transformValueIndex = 0)
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
            controller.Initialize(
                stencil, componentIndex, dimensionIndex, page, transformValueIndex);
        }

        internal NumericInputPopupWindow OpenNumericInput(string title)
        {
            BasePanel panel = m_Manager?.GetPanelForPopUps();
            if (panel == null || m_NumericInputPopupPrefab == null)
            {
                return null;
            }

            GameObject popupObject = panel.CreatePopUp(
                m_NumericInputPopupPrefab, Vector3.zero,
                explicitPosition: false, transition: true,
                sDelayedText: title);
            return popupObject.GetComponent<NumericInputPopupWindow>();
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
