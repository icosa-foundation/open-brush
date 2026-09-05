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
    public abstract class BaseSculptSubTool : MonoBehaviour
    {

        protected float m_DefaultStrength = 0.1f;

        public abstract SculptSubToolManager.SubTool SubToolIdentifier { get; }

        protected Collider m_Collider;

        /// Whether CalculateStrength returns a per-update displacement that should be normalized
        /// to elapsed time. Subtools that return an absolute positional correction can opt out.
        public virtual bool UsesContinuousStrength => true;


        /// For sculpting tools with an interactor that limits the sculpting tool's
        /// sphere of influence. If the interactor doesn't exist or shouldn't limit things, this is ignored.
        public virtual bool IsInReach(Vector3 vertex, TrTransform canvasPose)
        {
            return true;
        }

        public virtual float CalculateStrength(
            Vector3 vertex, float distance, float radius, TrTransform canvasPose, bool bPushing)
        {
            return m_DefaultStrength;
        }

        public virtual float CalculateInfluence(
            Vector3 vertex, Vector3 toolPosition, float radius, TrTransform canvasPose)
        {
            return StrokeSculptInfluence.CalculateRadialWeight(
                Vector3.Distance(vertex, toolPosition), radius);
        }

        public virtual float ConstrainDisplacement(
            float displacement, float distance, bool bPushing)
        {
            return displacement;
        }

        /// Scales one reference update's displacement for an elapsed number of reference updates.
        /// Constant-speed tools use linear scaling; proportional tools override this.
        public virtual float ScaleDisplacementForReferenceUpdates(
            Vector3 vertex, float displacement, float referenceUpdates,
            TrTransform canvasPose, bool bPushing)
        {
            return displacement * referenceUpdates;
        }

        public abstract Vector3 CalculateDirection(Vector3 vertex, Transform toolTransform, TrTransform canvasPose, bool bPushing, BatchSubset rGroup);
    }
} //namespace TiltBrush
