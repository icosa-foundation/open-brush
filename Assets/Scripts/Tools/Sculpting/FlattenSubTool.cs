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
    public class FlattenSubTool : BaseSculptSubTool
    {
        private BoxCollider m_BoxCollider;

        private void Awake()
        {
            m_SubToolIdentifier = SculptSubToolManager.SubTool.Flatten;
            m_BoxCollider = GetComponent<BoxCollider>();
            m_Collider = m_BoxCollider;
        }

        public override bool IsInReach(Vector3 vertex, TrTransform canvasPose)
        {
            Vector3 localPoint = m_BoxCollider.transform.InverseTransformPoint(canvasPose * vertex);
            Vector3 offset = localPoint - m_BoxCollider.center;
            Vector3 halfSize = m_BoxCollider.size * 0.5f;
            return Mathf.Abs(offset.x) <= halfSize.x && Mathf.Abs(offset.z) <= halfSize.z;
        }

        public override float CalculateStrength(
            Vector3 vertex, float distance, float radius, TrTransform canvasPose, bool bPushing)
        {
            Vector3 planeOffset = CalculateWorldPlaneOffset(vertex, canvasPose);
            return m_DefaultStrength * planeOffset.magnitude / canvasPose.scale;
        }

        public override float CalculateInfluence(
            Vector3 vertex, Vector3 toolPosition, float radius, TrTransform canvasPose)
        {
            Vector3 planePoint = canvasPose.inverse *
                m_BoxCollider.transform.TransformPoint(m_BoxCollider.center);
            Vector3 planeNormal = Quaternion.Inverse(canvasPose.rotation) *
                m_BoxCollider.transform.up;
            return StrokeSculptInfluence.CalculatePlaneWeight(
                vertex, planePoint, planeNormal, radius);
        }

        public override Vector3 CalculateDirection(Vector3 vertex, Transform toolTransform, TrTransform canvasPose, bool bPushing, BatchSubset rGroup)
        {
            Vector3 planeOffset = CalculateWorldPlaneOffset(vertex, canvasPose);
            return (Quaternion.Inverse(canvasPose.rotation) * planeOffset).normalized;
        }

        private Vector3 CalculateWorldPlaneOffset(Vector3 vertex, TrTransform canvasPose)
        {
            Vector3 planePoint = m_BoxCollider.transform.TransformPoint(m_BoxCollider.center);
            return StrokeSculptInfluence.CalculatePlaneOffset(
                canvasPose * vertex, planePoint, m_BoxCollider.transform.up);
        }
    }

} // namespace TiltBrush
