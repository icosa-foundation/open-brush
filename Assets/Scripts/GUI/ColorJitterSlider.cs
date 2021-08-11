// Copyright 2021 The Open Brush Authors
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
using UnityEngine;
namespace TiltBrush
{

    public class ColorJitterSlider : BaseSlider
    {

        private float pow = 1.5f;

        [Serializable]
        public enum JitterProperties
        {
            Hue,
            Saturation,
            Value
        }
        public JitterProperties JitterProperty;

        protected override void Awake()
        {

            float adjust(float val) { return Mathf.Pow(val * 2f, 1f / pow); }
            base.Awake();
            var jitter = PointerManager.m_Instance.colorJitter;
            switch (JitterProperty)
            {
                case JitterProperties.Hue:
                    m_CurrentValue = adjust(jitter.x);
                    break;
                case JitterProperties.Saturation:
                    m_CurrentValue = adjust(jitter.y);
                    break;
                case JitterProperties.Value:
                    m_CurrentValue = adjust(jitter.z);
                    break;
            }
            SetSliderPositionToReflectValue();
        }

        public override void UpdateValue(float fValue)
        {
            var jitter = PointerManager.m_Instance.colorJitter;
            float val = Mathf.Pow(fValue, pow) / 2f;  // Lower values are more interesting so square it
            switch (JitterProperty)
            {
                case JitterProperties.Hue:
                    jitter.x = val;
                    break;
                case JitterProperties.Saturation:
                    jitter.y = val;
                    break;
                case JitterProperties.Value:
                    jitter.z = val;
                    break;
            }
            PointerManager.m_Instance.colorJitter = jitter;
            m_CurrentValue = fValue;
            Debug.Log($"val {fValue}: {PointerManager.m_Instance.colorJitter} = {jitter}");
        }

        public override void ResetState()
        {
            base.ResetState();
        }
    }
} // namespace TiltBrush
