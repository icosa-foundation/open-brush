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

    [SerializeField]
    public GameObject m_ToolInteractor;

    override public void Init()
    {
      base.Init();
    }

    override public void EnableTool(bool bEnable)
    {
      // Call this after setting up our tool's state.
      base.EnableTool(bEnable);
      // CTODO: change the material of all strokes to some wireframe shader.
      HideTool(!bEnable);
    }

    override protected bool IsOn() {
      return m_bIsPushing;
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

    //CTODO: This is an absolute mess.
    override protected bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup) {
      // Metadata of target stroke
      var stroke = rGroup.m_Stroke;
      Batch parentBatch = rGroup.m_ParentBatch;
      int startIndex = rGroup.m_StartVertIndex;
      int vertLength = rGroup.m_VertLength;

      var newVertices = parentBatch.m_Geometry.m_Vertices.GetRange(startIndex, vertLength);
      // Tool position adjusted by canvas transformations
      var toolPos = m_CurrentCanvas.Pose.inverse * m_ToolTransform.position;

      for (int i = 0; i < vertLength; i++) {
        newVertices[i] = CreaseTransform(newVertices[i], toolPos, rGroup);
      }

      SketchMemoryScript.m_Instance.MemorizeStrokeSculpt(rGroup, newVertices, startIndex, !m_AtLeastOneModificationMade);
      
      m_AtLeastOneModificationMade = true;

      return true;
    }

    override public void AssignControllerMaterials(InputManager.ControllerName controller) {
      InputManager.Brush.Geometry.ShowSculptToggle(m_bIsPushing);
    }

    //CTODO: some duplication between the functions
    Vector3 PushTransform(Vector3 vertex, Vector3 toolPos, BatchSubset rGroup) {
      // Distance from vertex to pointer's center.
      float distance = Vector3.Distance(vertex, toolPos);

      if (distance <= GetSize() / m_CurrentCanvas.Pose.scale && m_ToolInteractor.GetComponent<Renderer>().bounds.Contains(m_CurrentCanvas.Pose * vertex)) {
        float strength = 0.1f;

        Vector3 direction = (vertex - toolPos).normalized;

        direction *= m_bIsPushing ? 1 : -1; // push or pull based on current mode
        Vector3 newVert = vertex + direction * strength;
        
      
        rGroup.m_Stroke.m_bWasSculpted = true;
        PlayModifyStrokeSound();
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);

        return newVert;
      } else {
        return vertex;
      }
    }

    Vector3 CreaseTransform(Vector3 vertex, Vector3 toolPos, BatchSubset rGroup) {
      // Distance from vertex to pointer's center.
      float distance = Vector3.Distance(vertex, toolPos);

      if (distance <= GetSize() / m_CurrentCanvas.Pose.scale 
          && m_ToolInteractor.GetComponent<Renderer>().bounds.Contains(m_CurrentCanvas.Pose * vertex)) {
        float strength = 0.1f;
        Vector3 direction = -(vertex - rGroup.m_Bounds.center).normalized;

        direction *= m_bIsPushing ? 1 : -1; // push or pull based on current mode
        Vector3 newVert = vertex + direction * strength;
        
        rGroup.m_Stroke.m_bWasSculpted = true;
        PlayModifyStrokeSound();
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);

        return newVert;
      } else {
        return vertex;
      }
    }
  }



} // namespace TiltBrush
