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
        [SerializeField] private OptionButton m_GroupButton;
        [SerializeField] private Texture2D m_ConvertToSdfTexture;

        private SketchControlsScript.GlobalCommands m_DefaultGroupCommand;
        private Texture2D m_DefaultGroupTexture;
        private string m_DefaultGroupDescription;
        private bool m_ContextActionInitialized;

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
            m_GroupButton.SetContextCommand(
                guidesOnly
                    ? SketchControlsScript.GlobalCommands.ConvertSelectionToSdf
                    : m_DefaultGroupCommand,
                guidesOnly && m_ConvertToSdfTexture != null
                    ? m_ConvertToSdfTexture
                    : m_DefaultGroupTexture,
                guidesOnly ? "Convert to SDF" : m_DefaultGroupDescription);
            m_GroupButton.UpdateVisuals();
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
