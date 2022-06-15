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
public class PushSubtool : BaseSculptSubtool {

    override public float CalculateStrength(Vector3 vertex, float distance, bool bPushing) {
        if (!bPushing) { // special calculation to reduce spikyness
            return m_DefaultStrength * Mathf.Pow(distance, 2);
        } else {
            return m_DefaultStrength;
        }
    }

    override public Vector3 CalculateDirection(Vector3 vertex, Vector3 toolPos, TrTransform canvasPose, bool bPushing, BatchSubset rGroup) {
        return (bPushing ? 1 : -1) * (vertex - toolPos).normalized;
    }
}

}// namespace TiltBrush
