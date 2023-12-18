// Copyright 2023 The Open Brush Authors
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

namespace TiltBrush.FrameAnimation
{
    public class TimelineSlider : BaseSlider
    {
        override protected void Awake()
        {
            base.Awake();
            SetSliderPositionToReflectValue();
        }

        public void SetSliderValue(float fValue)
        {
            float newVal = (fValue - 0.5f) * m_MeshScale.x;
            Vector3 vLocalPos = m_Nob.transform.localPosition;
            m_Nob.transform.localPosition = new Vector3(newVal, vLocalPos.y, vLocalPos.z);
        }

        override public void ButtonPressed(RaycastHit rHitInfo)
        {
            if (IsAvailable())
            {
                PositionSliderNob(rHitInfo.point);
                var uiManager = GetComponentInParent<AnimationUI_Manager>();
                uiManager.TimelineSlideDown(true);
            }
            SetDescriptionActive(true);
        }

        public override void ButtonReleased() {
            var uiManager = GetComponentInParent<AnimationUI_Manager>();
            uiManager.TimelineSlideDown(false);
        }

        public override void UpdateValue(float fValue)
        {
            base.UpdateValue(fValue);
            var uiManager = GetComponentInParent<AnimationUI_Manager>();
            uiManager.TimelineSlideDown(true);
            uiManager.TimelineSlide(fValue);
        }
    }
} // namespace TiltBrush.FrameAnimation
