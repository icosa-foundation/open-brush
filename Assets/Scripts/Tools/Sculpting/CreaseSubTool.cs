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
    public class CreaseSubTool : BaseSculptSubTool
    {
        private BoxCollider m_BoxCollider;

        public override SculptSubToolManager.SubTool SubToolIdentifier =>
            SculptSubToolManager.SubTool.Pinch;

        private void Awake()
        {
            m_BoxCollider = GetComponent<BoxCollider>();
            m_Collider = m_BoxCollider;
        }

        public override bool IsInReach(Vector3 vertex, TrTransform canvasPose)
        {
            Vector3 localPoint = m_BoxCollider.transform.InverseTransformPoint(canvasPose * vertex);
            Vector3 offset = localPoint - m_BoxCollider.center;
            float halfLength = m_BoxCollider.size.z * 0.5f;
            return Mathf.Abs(offset.z) <= halfLength;
        }

        public override float CalculateStrength(
            Vector3 vertex, float distance, float radius, TrTransform canvasPose, bool bPushing)
        {
            return m_DefaultStrength * CalculateWorldLineOffset(vertex, canvasPose).magnitude /
                canvasPose.scale;
        }

        public override float CalculateInfluence(
            Vector3 vertex, Vector3 toolPosition, float radius, TrTransform canvasPose)
        {
            Vector3 linePoint = canvasPose.inverse *
                m_BoxCollider.transform.TransformPoint(m_BoxCollider.center);
            Vector3 lineDirection = Quaternion.Inverse(canvasPose.rotation) *
                m_BoxCollider.transform.forward;
            return StrokeSculptInfluence.CalculateLineWeight(
                vertex, linePoint, lineDirection, radius);
        }

        public override Vector3 CalculateDirection(Vector3 vertex, Transform toolTransform, TrTransform canvasPose, bool bPushing, BatchSubset rGroup)
        {
            Vector3 lineOffset = CalculateWorldLineOffset(vertex, canvasPose);
            Vector3 direction = Quaternion.Inverse(canvasPose.rotation) * lineOffset;
            return (bPushing ? 1f : -1f) * direction.normalized;
        }

        public override float ScaleDisplacementForReferenceUpdates(
            Vector3 vertex, float displacement, float referenceUpdates,
            TrTransform canvasPose, bool bPushing)
        {
            float distanceToLine =
                CalculateWorldLineOffset(vertex, canvasPose).magnitude / canvasPose.scale;
            return StrokeSculptInfluence.ScaleProportionalDisplacement(
                distanceToLine, displacement, referenceUpdates, towardTarget: bPushing);
        }

        private Vector3 CalculateWorldLineOffset(Vector3 vertex, TrTransform canvasPose)
        {
            Vector3 linePoint = m_BoxCollider.transform.TransformPoint(m_BoxCollider.center);
            return StrokeSculptInfluence.CalculateLineOffset(
                canvasPose * vertex, linePoint, m_BoxCollider.transform.forward);
        }
    }

} // namespace TiltBrush
