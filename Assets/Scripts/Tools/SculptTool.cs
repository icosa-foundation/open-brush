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
  public BaseSculptSubTool m_ActiveSubTool;

  override public void EnableTool(bool bEnable) {
    // Call this after setting up our tool's state.
    base.EnableTool(bEnable);
    HideTool(!bEnable);
  }

  override public void HideTool(bool bHide) {
    m_ActiveSubTool.gameObject.SetActive(!bHide);
    base.HideTool(bHide);
  }

  override protected bool IsOn() {
    return m_bIsPushing;
  }

  public void SetSubTool(BaseSculptSubTool subTool) {
    // Disable old subtool
    m_ActiveSubTool.gameObject.SetActive(false);
    m_ActiveSubTool = subTool;
  }
  
  public void FinalizeSculptingBatch() {
    m_AtLeastOneModificationMade = false;
  }


  override public void OnUpdateDetection() {
    if (!m_CurrentlyHot && m_ToolWasHot) {
      FinalizeSculptingBatch();
      ResetToolRotation();
      ClearGpuFutureLists();
    }

    if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleSculpt)) {
      if (m_ActiveSubTool.m_SubToolIdentifier != SculptSubToolManager.SubTool.Flatten) {
        m_bIsPushing = !m_bIsPushing;
        StartToggleAnimation();
      } 
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

    if (parentBatch == null || parentBatch.m_Geometry == null) { 
      // Shouldn't happen anymore
      Debug.LogWarning("Orphaned batch subset, skipping");
      return false;
    }
    parentBatch.m_Geometry.EnsureGeometryResident();
    
    var newVertices = parentBatch.m_Geometry.m_Vertices.GetRange(startIndex, vertLength);
    // Tool position adjusted by canvas transformations
    for (int i = 0; i < vertLength; i++) { // This loop is expensive

      Vector3 newVert = m_ActiveSubTool.ManipulateVertex(newVertices[i], m_bIsPushing, m_CurrentCanvas.Pose, m_ToolTransform, GetSize(), rGroup);

      // if the vertex pos changed
      if (Vector3.Distance(newVert, newVertices[i]) > Mathf.Epsilon) { 
        rGroup.m_Stroke.m_bWasSculpted = true;
        PlayModifyStrokeSound();
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);
        
        newVertices[i] = newVert;
      }
    }
    SketchMemoryScript.m_Instance.MemorizeStrokeSculpt(rGroup, newVertices, startIndex, !m_AtLeastOneModificationMade);
    
    m_AtLeastOneModificationMade = true;

    return true;
  }

  override public void AssignControllerMaterials(InputManager.ControllerName controller) {
    if (m_ActiveSubTool.m_SubToolIdentifier != SculptSubToolManager.SubTool.Flatten) {
      InputManager.Brush.Geometry.ShowSculptToggle(m_bIsPushing);
    }
  }

}

} // namespace TiltBrush



