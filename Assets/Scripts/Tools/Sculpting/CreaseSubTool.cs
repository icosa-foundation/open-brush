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
public class CreaseSubTool : BaseSculptSubTool {

    void Awake() {
        m_SubToolIdentifier = SculptSubToolManager.SubTool.Crease;
        m_Collider = GetComponent<Collider>();
    }

    override public Vector3 ManipulateVertex(Vector3 vertex, bool bPushing, TrTransform canvasPose, Transform toolTransform, float toolSize, BatchSubset rGroup) {
        Vector3 vertToTool = vertex - (canvasPose.inverse * toolTransform.position);
        float strength = bPushing ? m_DefaultStrength : -m_DefaultStrength;

        if (vertToTool.magnitude <= toolSize / canvasPose.scale && IsInReach(canvasPose * vertex)) {

            return vertex + strength * -(vertex - rGroup.m_Bounds.center).normalized;
        }
        return vertex;
    }


    override protected bool IsInReach(Vector3 vertex) {
        return m_Collider.bounds.Contains(vertex);
    }

    // override public Vector3 CalculateDirection(Vector3 vertex, Transform toolTransform, TrTransform canvasPose, bool bPushing, BatchSubset rGroup) {
    //     return (bPushing ? 1 : -1) * -(vertex - rGroup.m_Bounds.center).normalized;
    // }
}

} // namespace TiltBrush
