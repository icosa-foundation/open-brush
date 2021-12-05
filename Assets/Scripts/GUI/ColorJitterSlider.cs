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
            Value,
            Size,
            Position
        }
        public JitterProperties JitterProperty;

        protected override void Awake()
        {

            float adjust(float val) { return Mathf.Pow(val * 2f, 1f / pow); }
            base.Awake();
            Vector3 colorJitter = PointerManager.m_Instance.colorJitter;
            float sizeJitter = PointerManager.m_Instance.sizeJitter;
            float positionJitter = PointerManager.m_Instance.positionJitter;
            switch (JitterProperty)
            {
                case JitterProperties.Hue:
                    m_CurrentValue = adjust(colorJitter.x);
                    break;
                case JitterProperties.Saturation:
                    m_CurrentValue = adjust(colorJitter.y);
                    break;
                case JitterProperties.Value:
                    m_CurrentValue = adjust(colorJitter.z);
                    break;
                case JitterProperties.Size:
                    m_CurrentValue = adjust(sizeJitter);
                    break;
                case JitterProperties.Position:
                    m_CurrentValue = adjust(positionJitter);
                    break;
            }
            SetSliderPositionToReflectValue();
        }

        public override void UpdateValue(float fValue)
        {
            float val = Mathf.Pow(fValue, pow) / 2f;  // Lower values are more interesting so square it
            Vector3 colorJitter = PointerManager.m_Instance.colorJitter;
            switch (JitterProperty)
            {
                case JitterProperties.Hue:
                    colorJitter.x = val;
                    PointerManager.m_Instance.colorJitter = colorJitter;
                    break;
                case JitterProperties.Saturation:
                    colorJitter.y = val;
                    PointerManager.m_Instance.colorJitter = colorJitter;
                    break;
                case JitterProperties.Value:
                    colorJitter.z = val;
                    PointerManager.m_Instance.colorJitter = colorJitter;
                    break;
                case JitterProperties.Size:
                    PointerManager.m_Instance.sizeJitter = val;
                    break;
                case JitterProperties.Position:
                    PointerManager.m_Instance.positionJitter = val;
                    break;
            }
            m_CurrentValue = fValue;
        }

        public override void ResetState()
        {
            base.ResetState();
        }
    }
} // namespace TiltBrush
