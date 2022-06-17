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
  
  override public float CalculateStrength(Vector3 vertex, float distance, TrTransform canvasPose,  bool bPushing) {
    return distance * 0.05f;
  }

  // Adapted from https://answers.unity.com/questions/532297/rotate-a-vector-around-a-certain-point.html
  // CTODO: very broken
  override public Vector3 CalculateDirection(Vector3 vertex, Transform toolTransform, TrTransform canvasPose, bool bPushing, BatchSubset rGroup) {
    Vector3 toolPos = canvasPose.inverse * toolTransform.position;
    Vector3 direction = (vertex - canvasPose.inverse * m_Collider.ClosestPoint(canvasPose * vertex));
    // the normal of the point to the toolthing would be the closest point.
    // Debug.Log("tool rotation: " + " " + toolTransform.eulerAngles.x  + " " + toolTransform.eulerAngles.y  + " " + toolTransform.eulerAngles.z);
    // direction = Quaternion.Euler(canvasPose.rotation.x + toolTransform.eulerAngles.x, canvasPose.rotation.y + toolTransform.eulerAngles.y, canvasPose.rotation.z + toolTransform.eulerAngles.z + (bPushing ? 1 : -1) * 90) * direction.normalized;
    Quaternion oldRotation = toolTransform.rotation;
    toolTransform.rotation *= Quaternion.Inverse(canvasPose.rotation);
    //ugly way to ignore z component
    // toolTransform.rotation = Quaternion.Euler(toolTransform.rotation.eulerAngles.x, toolTransform.rotation.eulerAngles.y, oldRotation.eulerAngles.z);
    direction = Quaternion.AngleAxis((bPushing ? 1 : -1) * -90, (toolTransform).forward) * direction.normalized;
    toolTransform.rotation = oldRotation;
    return  direction;
  }
}

}// namespace TiltBrush
