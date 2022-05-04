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

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;



namespace TiltBrush
{


    [Serializable]
    public class simpleSliderEvent : UnityEvent<float>
    {
    }

    public class SimpleSlider : BaseSlider
    {
        public float min;
        public float max;

        [SerializeField] private TextMeshPro minText;
        [SerializeField] private TextMeshPro maxText;
        [SerializeField] private TextMeshPro valueText;
        public SliderTypes SliderType;

        [SerializeField] public simpleSliderEvent onUpdateValue;


        float remap(float s, float a1, float a2, float b1, float b2)
        {
            return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
        }

        override protected void Awake()
        {
            base.Awake();
            m_CurrentValue = 0.5f;
            SetSliderPositionToReflectValue();
            minText.text = FormatValue(min);
            maxText.text = FormatValue(max);
            valueText.text = FormatValue(m_CurrentValue);
        }

        private string FormatValue(float val)
        {
            if (SliderType == SliderTypes.Int)
            {
                return Mathf.FloorToInt(val).ToString();
            }
            else if (SliderType == SliderTypes.Float)
            {
                return (Mathf.Round(val * 10) / 10).ToString();
            }

            return "";
        }

        override public void UpdateValue(float fValue)
        {
            var val = remap(fValue, 0, 1, min, max);
            UpdateValueAbsolute(val);
        }

        public void UpdateValueAbsolute(float fValue)
        {
            valueText.text = FormatValue(fValue);
            onUpdateValue.Invoke(fValue);
            m_CurrentValue = Mathf.InverseLerp(min, max, fValue);
            SetSliderPositionToReflectValue();
        }

        public override void ResetState()
        {
            base.ResetState();
        }
    }
} // namespace TiltBrush
