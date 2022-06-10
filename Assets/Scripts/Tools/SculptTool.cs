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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
public class SculptTool : ToggleStrokeModificationTool
{
  /// Keeps track of the first sculpting change made while the trigger is held.
  private bool m_AtLeastOneModificationMade = false;
  /// Determines whether the tool is in push mode or pull mode.
  /// Corresponds to the On/Off state
  private bool m_bIsPushing = true;
  /// This holds a GameObject that represents the currently active sub-tool, inside
  /// the existing sculpting sphere. These can be used for further finetuning
  /// vertex interactions, and also just for visual representations for the
  /// user.
  [SerializeField]
  public BaseSculptSubtool m_ActiveSubtool;

  override public void Init() {
    base.Init();
  }

  override public void EnableTool(bool bEnable) {
    // Call this after setting up our tool's state.
    base.EnableTool(bEnable);
    // CTODO: change the material of all strokes to some wireframe shader.
    HideTool(!bEnable);
  }

  override public void HideTool(bool bHide) {
    m_ActiveSubtool.gameObject.SetActive(!bHide);
    base.HideTool(bHide);
  }

  override protected bool IsOn() {
    return m_bIsPushing;
  }

  public void SetSubtool(BaseSculptSubtool subTool)
  {
    // Disable old subtool
    m_ActiveSubtool.gameObject.SetActive(false);

    m_ActiveSubtool = subTool;
  }
  
  public void FinalizeSculptingBatch()
  {
    m_AtLeastOneModificationMade = false;
  }


  override public void OnUpdateDetection() {
    if (!m_CurrentlyHot && m_ToolWasHot) {
      FinalizeSculptingBatch();
      ResetToolRotation();
      ClearGpuFutureLists();
    }

    if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleSculpt)) {
      m_bIsPushing = !m_bIsPushing;
      StartToggleAnimation();
    }
  }

  override protected void OnAnimationSwitch() {
    // AudioManager.m_Instance.PlayToggleSelect(m_ToolTransform.position, true);
    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);
  }

  override protected bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup) {
    // Metadata of target stroke
    var stroke = rGroup.m_Stroke;
    Batch parentBatch = rGroup.m_ParentBatch;
    int startIndex = rGroup.m_StartVertIndex;
    int vertLength = rGroup.m_VertLength;

    // Copy the relevant portion of geometry to modify
    if (parentBatch == null)
    {
      Debug.LogWarning("Orphaned batch subset, skipping");
    }
    var newVertices = parentBatch.m_Geometry.m_Vertices.GetRange(startIndex, vertLength);
    // Tool position adjusted by canvas transformations
    var toolPos = m_CurrentCanvas.Pose.inverse * m_ToolTransform.position;

    for (int i = 0; i < vertLength; i++) {

      float strength = 0.1f; // CTODO: maybe make the subtools calculate this
      float distance = Vector3.Distance(newVertices[i], toolPos);

      if (distance <= GetSize() / m_CurrentCanvas.Pose.scale && m_ActiveSubtool.IsInReach(newVertices[i], m_CurrentCanvas.Pose)) {
        Vector3 direction = m_ActiveSubtool.CalculateDirection(newVertices[i], toolPos, m_bIsPushing, rGroup);

        newVertices[i] += direction * strength;

        rGroup.m_Stroke.m_bWasSculpted = true;
        PlayModifyStrokeSound();
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);
      }
    }
    SketchMemoryScript.m_Instance.MemorizeStrokeSculpt(rGroup, newVertices, startIndex, !m_AtLeastOneModificationMade);
    
    m_AtLeastOneModificationMade = true;

    return true;
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    InputManager.Brush.Geometry.ShowSculptToggle(m_bIsPushing);
  }

}

} // namespace TiltBrush



