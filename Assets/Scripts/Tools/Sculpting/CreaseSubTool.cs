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
        Vector3 closestPoint = m_Collider.ClosestPoint(canvasPose * vertex);
        bool bInSubTool = Vector3.Distance(closestPoint, m_Collider.bounds.center) >= (canvasPose * vertex - (m_Collider.bounds.center)).magnitude;

        if (vertToTool.magnitude <= toolSize / canvasPose.scale && bInSubTool) {
            return vertex + strength * -(vertex - rGroup.m_Bounds.center).normalized;
        }
        return vertex;
    }
}

} // namespace TiltBrush
