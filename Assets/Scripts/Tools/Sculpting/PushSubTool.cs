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
public class PushSubTool : BaseSculptSubTool {

  void Awake() {
    m_SubToolIdentifier = SculptSubToolManager.SubTool.Push;
  }

  override public Vector3 ManipulateVertex(Vector3 vertex, bool bPushing, TrTransform canvasPose, Transform toolTransform, float toolSize, BatchSubset rGroup) {
    Vector3 vertToTool = vertex - (canvasPose.inverse * toolTransform.position);
    if (vertToTool.magnitude <= toolSize / canvasPose.scale) {
      float strength = m_DefaultStrength;
      
      if (!bPushing) { // special calculation to reduce spikyness
        strength = -m_DefaultStrength * Mathf.Pow(vertToTool.magnitude, 2) / toolSize;
      }

      return vertex + strength * vertToTool.normalized;
    }
    return vertex;
  }
}

}// namespace TiltBrush
