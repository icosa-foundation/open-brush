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
public class RotateSubTool : BaseSculptSubTool {

  void Awake() {
    m_SubToolIdentifier = SculptSubToolManager.SubTool.Rotate;
    m_Collider = GetComponent<Collider>();
  }
  override public Vector3 ManipulateVertex(Vector3 vertex, bool bPushing, TrTransform canvasPose, Transform toolTransform, float toolSize, BatchSubset rGroup) {
    Vector3 vertToTool = vertex - (canvasPose.inverse * toolTransform.position);
    if (vertToTool.magnitude <= toolSize / canvasPose.scale) {
      Vector3 vertToPivot = (vertex - canvasPose.inverse * m_Collider.ClosestPoint(canvasPose * vertex));
      float strength = vertToPivot.magnitude * 0.05f;
      
      Quaternion oldRotation = toolTransform.rotation;
      
      toolTransform.rotation *= Quaternion.Inverse(canvasPose.rotation);
      // Adapted from https://answers.unity.com/questions/532297/rotate-a-vector-around-a-certain-point.html
      Vector3 rotation = Quaternion.AngleAxis((bPushing ? 1 : -1) * -90, (toolTransform).forward) * vertToPivot.normalized;
      toolTransform.rotation = oldRotation;
      
      return vertex + strength * rotation;

    }
    return vertex;
  }
}

}// namespace TiltBrush
