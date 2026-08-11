// Copyright 2026 The Open Brush Authors
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
    public static class StrokeSculptInfluence
    {
        /// Returns a smooth radial weight which is one at the tool centre and zero at or
        /// beyond its boundary. The zero derivatives at both ends avoid visible changes
        /// in velocity as a control point crosses either end of the falloff.
        public static float CalculateRadialWeight(float distance, float radius)
        {
            if (radius <= 0f || distance >= radius)
            {
                return 0f;
            }

            float normalizedDistance = Mathf.Clamp01(distance / radius);
            return 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);
        }
    }
} // namespace TiltBrush
