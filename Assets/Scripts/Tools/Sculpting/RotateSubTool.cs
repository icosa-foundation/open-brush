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
  }
  
  override public float CalculateStrength(Vector3 vertex, float distance, TrTransform canvasPose,  bool bPushing) {
    return 0.05f;
  }

  // Adapted from https://answers.unity.com/questions/532297/rotate-a-vector-around-a-certain-point.html
  // CTODO: very broken
  override public Vector3 CalculateDirection(Vector3 vertex, Transform toolTransform, TrTransform canvasPose, bool bPushing, BatchSubset rGroup) {
    Vector3 toolPos = canvasPose.inverse * toolTransform.position;
    Vector3 direction = (vertex - canvasPose.inverse * GetComponent<Collider>().ClosestPoint(canvasPose * vertex)).normalized;
    // the normal of the point to the toolthing would be the closest point.
    // Debug.Log("tool rotation: " + " " + toolTransform.eulerAngles.x  + " " + toolTransform.eulerAngles.y  + " " + toolTransform.eulerAngles.z);
    direction = Quaternion.Euler(0, 0, (bPushing ? 1 : -1) * 90) * direction;
    return  direction;
  }
}

}// namespace TiltBrush
