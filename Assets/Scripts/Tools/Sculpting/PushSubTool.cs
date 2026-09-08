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
    public class PushSubTool : BaseSculptSubTool
    {

        public override SculptSubToolManager.SubTool SubToolIdentifier =>
            SculptSubToolManager.SubTool.Push;

        public override float CalculateStrength(
            Vector3 vertex, float distance, float radius, TrTransform canvasPose, bool bPushing)
        {
            return m_DefaultStrength * radius;
        }

        public override float ConstrainDisplacement(
            float displacement, float distance, bool bPushing)
        {
            return bPushing ? displacement : Mathf.Min(displacement, distance);
        }

        public override Vector3 CalculateDirection(Vector3 vertex, Transform toolTransform, TrTransform canvasPose, bool bPushing, BatchSubset rGroup)
        {
            return (bPushing ? 1 : -1) * (vertex - canvasPose.inverse * toolTransform.position).normalized;
        }
    }

} // namespace TiltBrush
