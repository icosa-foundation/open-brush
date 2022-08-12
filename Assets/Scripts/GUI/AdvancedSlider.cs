// Copyright 2022 The Open Brush Authors
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
using UnityEngine.Serialization;

namespace TiltBrush
{
    [Serializable]
    public class sliderEvent : UnityEvent<Vector3> { }

    [Serializable]
    public enum SliderTypes
    {
        Int,
        Float
    }

    public class AdvancedSlider : BaseSlider
    {
        [FormerlySerializedAs("opIndex")] public int m_Param1;
        [FormerlySerializedAs("paramIndex")] public int m_Param2;

        private float m_safeMin;
        private float m_safeMax;
        private float m_unsafeMin;
        private float m_unsafeMax;

        public bool m_SafeLimits = true;

        public float Min => m_SafeLimits ? m_safeMin : m_unsafeMin;
        public float Max => m_SafeLimits ? m_safeMax : m_unsafeMax;

        public void SetMax(float safeMax, float unsafeMax)
        {
            maxText.text = FormatValue(safeMax);
            m_safeMax = safeMax;
            m_unsafeMax = unsafeMax;
            m_SafeLimits = true;

        }

        public void SetMin(float safeMin, float unsafeMin)
        {
            minText.text = FormatValue(safeMin);
            m_safeMin = safeMin;
            m_unsafeMin = unsafeMin;
            m_SafeLimits = true;
        }

        [SerializeField] private TextMeshPro minText;
        [SerializeField] private TextMeshPro maxText;
        [SerializeField] private TextMeshPro valueText;
        public SliderTypes SliderType;

        [SerializeField] public sliderEvent onUpdateValue;

        public float CurrentValueAbsolute => Mathf.Lerp(Min, Max, m_CurrentValue);

        float remap(float s, float a1, float a2, float b1, float b2)
        {
            return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
        }

        override protected void Awake()
        {
            base.Awake();
            UpdateValue(0.5f);
            minText.text = FormatValue(Min);
            maxText.text = FormatValue(Max);
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
            var val = remap(fValue, 0, 1, Min, Max);
            _UpdateValueAbsolute(val);
        }

        public void UpdateValueAbsolute(float fValue)
        {
            // Set m_SafeLimits on if we are within the range, off we are outside the range
            if (m_SafeLimits && (fValue < Min || fValue > Max)) { HandleChangeLimits(); }
            if (!m_SafeLimits && (fValue >= Min && fValue <= Max)) { HandleChangeLimits(); }
            _UpdateValueAbsolute(fValue);
        }

        private void _UpdateValueAbsolute(float fValue)
        {
            valueText.text = FormatValue(fValue);
            onUpdateValue.Invoke(new Vector3(m_Param1, m_Param2, fValue));
            m_CurrentValue = Mathf.InverseLerp(Min, Max, fValue);
            SetSliderPositionToReflectValue();
        }

        private float CalcIncDecAmount()
        {
            if (SliderType == SliderTypes.Int) return 1;
            float range = Max - Min;
            float magnitude = Mathf.Floor(Mathf.Log10(range));
            return Mathf.Pow(10, magnitude) / 10f;
        }

        public void HandleIncrement()
        {
            _UpdateValueAbsolute(CurrentValueAbsolute + CalcIncDecAmount());
        }

        public void HandleDecrement()
        {
            _UpdateValueAbsolute(CurrentValueAbsolute - CalcIncDecAmount());
        }

        public void HandleChangeLimits()
        {
            float previousValue = CurrentValueAbsolute;
            m_SafeLimits = !m_SafeLimits;
            if (previousValue < Min)
            {
                m_CurrentValue = 0;
                previousValue = Min;
            }
            if (previousValue > Max)
            {
                m_CurrentValue = 1;
                previousValue = Max;
            }
            minText.text = FormatValue(Min);
            maxText.text = FormatValue(Max);
            _UpdateValueAbsolute(previousValue);
        }
    }
} // namespace TiltBrush
