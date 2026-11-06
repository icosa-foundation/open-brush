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

using UnityEngine;

namespace TiltBrush
{
    /// Opens the picker for standalone guides backed by SDF primitives.
    public sealed class SdfGuidePickerButton : BaseButton
    {
        [SerializeField] private GameObject m_PopupPrefab;

        protected override void OnButtonPressed()
        {
            BasePanel panel = m_Manager?.GetPanelForPopUps();
            if (panel == null || m_PopupPrefab == null)
            {
                return;
            }

            panel.CreatePopUp(
                m_PopupPrefab, Vector3.zero,
                explicitPosition: false, transition: true,
                sDelayedText: "SDF Guides");
            SketchControlsScript.m_Instance.EatGazeObjectInput();
        }
    }
} // namespace TiltBrush
