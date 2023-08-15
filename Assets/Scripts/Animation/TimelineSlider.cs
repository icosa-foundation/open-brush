// Copyright 2020 The Tilt Brush Authors
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
using System;


namespace TiltBrush.FrameAnimation
{

    public class TimelineSlider : BaseSlider
    {
        override protected void Awake()
        {
            base.Awake();
            // m_CurrentValue = PointerManager.m_Instance.FreePaintPointerAngle / 90.0f;
            SetSliderPositionToReflectValue();
        }

           // For @Animation
        public void setSliderValue(float fValue){
     
            float newVal = (fValue - 0.5f)* m_MeshScale.x;
     

             print("SLIDING ==" + fValue + "  newval==" + newVal + " Mesh scale==" + m_MeshScale.x);
            Vector3 vLocalPos = m_Nob.transform.localPosition;
            m_Nob.transform.localPosition = new Vector3(newVal,vLocalPos.y,vLocalPos.z);
            // UpdateValue(fValue);
            

        }

        override public void UpdateValue(float fValue)
        {
            base.UpdateValue(fValue);
            // PointerManager.m_Instance.FreePaintPointerAngle = fValue * 90.0f;

             var uiManager = GetComponentInParent<AnimationUI_Manager>();
            uiManager.timelineSlide(fValue);
        }

        public override void ResetState()
        {
            base.ResetState();
            // SetAvailable(!App.VrSdk.VrControls.LogitechPenIsPresent());
        }
    }
} // namespace TiltBrush
