// Copyright 2022 Chingiz Dadashov-Khandan
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

namespace TiltBrush {
public class SculptSubToolButton : BaseButton {

  [SerializeField]
  public SculptSubToolManager.SubTool m_SubTool;

  override public void UpdateVisuals() {
    base.UpdateVisuals();
    // Toggle buttons poll for status.
    if (m_ToggleButton && SculptSubToolManager.m_Instance != null) {
      bool bWasToggleActive = m_ToggleActive;
      m_ToggleActive = SculptSubToolManager.m_Instance.GetActiveSubtool() == m_SubTool;
      if (bWasToggleActive != m_ToggleActive) {
        SetButtonActivated(m_ToggleActive);
      }
    }
  }

  override protected void OnButtonPressed() { 
    SculptSubToolManager.m_Instance.SetSubTool(m_SubTool);
  }
}
} // namespace TiltBrush
