// Copyright 2023 The Open Brush Authors
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

using System.IO;
using UnityEngine;

namespace TiltBrush
{

    class SketchbookMenuPopUpWindow : MenuPopUpWindow
    {
        public Transform m_DropPortalButton;
        private int m_CommandParam;
        private SketchSetType m_SketchSetType;

        public override void SetPopupCommandParameters(int iCommandParam, int iCommandParam2)
        {
            m_CommandParam = iCommandParam;
            m_SketchSetType = (SketchSetType)iCommandParam2;
            bool isLinkableSketch = m_SketchSetType == SketchSetType.Curated || m_SketchSetType == SketchSetType.User;
            m_DropPortalButton.gameObject.SetActive(isLinkableSketch);

            OptionButton[] optionButtons = GetComponentsInChildren<OptionButton>();
            foreach (OptionButton button in optionButtons)
            {
                // The context menu button should only be enabled for valid sketches
                if (iCommandParam == -1)
                {
                    button.SetButtonAvailable(false);
                }
                else
                {
                    button.SetCommandParameters(iCommandParam, iCommandParam2);
                }
            }

            // The rename button should only be enabled for categories that support renaming
            var renameButton = GetComponentInChildren<KeyboardPopupButton>();
            SketchSetType sketchSetType = (SketchSetType)iCommandParam2;
            renameButton.SetButtonAvailable(sketchSetType == SketchSetType.User);
        }

        public void SetInitialKeyboardText(KeyboardPopupButton btn)
        {
            var sceneFileInfo = getSceneFileInfo(btn.m_CommandParam, m_SketchSetType);
            var currentName = Path.GetFileName(sceneFileInfo.FullPath);
            if (currentName.EndsWith(SaveLoadScript.TILT_SUFFIX))
            {
                currentName = currentName.Substring(0, currentName.Length - SaveLoadScript.TILT_SUFFIX.Length);
            }
            KeyboardPopUpWindow.m_InitialText = currentName;
        }

        private SceneFileInfo getSceneFileInfo(int commandParam, SketchSetType sketchSetType)
        {
            var sketchSet = SketchCatalog.m_Instance.GetSet(sketchSetType);
            return sketchSet.GetSketchSceneFileInfo(commandParam);
        }

        public void HandleDropPortalButton()
        {
            var sceneFileInfo = getSceneFileInfo(m_CommandParam, m_SketchSetType);
            if (sceneFileInfo == null)
            {
                Debug.LogWarning("HandleDropPortalButton called without a valid sketch.");
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneFileInfo.AssetId))
            {
                Debug.LogWarning($"Cannot create portal for sketch '{sceneFileInfo.HumanName}' because it has no Icosa asset id.");
                return;
            }

            var brushAttach = InputManager.m_Instance.GetBrushControllerAttachPoint();
            var spawnXf = TrTransform.TR(brushAttach.position, brushAttach.rotation);
            WidgetManager.m_Instance.CreatePortalWidget(spawnXf, sceneFileInfo.AssetId);

            SketchControlsScript.m_Instance.EatGazeObjectInput();
            SelectionManager.m_Instance.RemoveFromSelection(false);
            RequestClose(true);
        }
    }

} // namespace TiltBrush
