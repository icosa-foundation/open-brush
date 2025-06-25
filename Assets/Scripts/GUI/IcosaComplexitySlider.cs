// Copyright 2024 The Open Brush Authors
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
    public class IcosaComplexitySlider : AdvancedSlider
    {
        public float m_SliderMaxCutoff = 0.95f;
        public int m_TriangleCountMin = 10;
        public int m_TriangleCountMax = 50000;

        private float TriCountToFloat01(int triCount)
        {
            return Mathf.InverseLerp(m_TriangleCountMin, m_TriangleCountMax, triCount);
        }

        protected override string FormatValue(float val)
        {
            if (val >= m_SliderMaxCutoff)
            {
                return "MAX";
            }
            return $"{Mathf.RoundToInt(val * 100)}%";
        }

        public int CurrentTriangleCount => CalcTriangleCount(CurrentValueAbsolute);

        public int CalcTriangleCount(float val)
        {
            if (val >= m_SliderMaxCutoff)
            {
                return Int32.MaxValue;
            }
            return Mathf.RoundToInt(Mathf.Lerp(m_TriangleCountMin, m_TriangleCountMax, val));
        }

        protected string FormatSliderLabelValue(float val)
        {
            if (val >= m_SliderMaxCutoff)
            {
                return "No limit";
            }
            return $"{CalcTriangleCount(val)} triangles";
        }

        public override void SetSliderPositionToReflectValue()
        {
            base.SetSliderPositionToReflectValue();
            valueText.text = FormatSliderLabelValue(CurrentValueAbsolute);
        }

        public void SetCurrentTriangles(int triCount)
        {
            SetInitialValueAndUpdate(TriCountToFloat01(triCount));
        }
    }
} // namespace TiltBrush
