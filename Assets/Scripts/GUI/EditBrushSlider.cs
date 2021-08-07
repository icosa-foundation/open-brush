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
using UnityEngine;

namespace TiltBrush
{

    public class EditBrushSlider : BaseSlider
    {

        [NonSerialized] public EditBrushPanel ParentPanel;
        public Vector2 Range = Vector2.up; // 0 to 1
        public string FloatPropertyName;

        public void UpdateValueFromUnscaled(float unscaledValue)
        {
            UpdateValue(UnscaledValueToUnitValue(unscaledValue));
        }
        
        public override void UpdateValue(float unitValue)
        {
            float unscaledValue = UnitValueToUnscaledValue(unitValue);
            ParentPanel.SliderChanged(FloatPropertyName, unscaledValue);
            SetDescriptionText($"{FloatPropertyName.Substring(1)}={unscaledValue:0.##}");
            base.UpdateValue(unitValue);
        }
        
        public float UnscaledValueToUnitValue(float unscaledValue)
        {
            return Mathf.InverseLerp(Range.x, Range.y, unscaledValue);
        }
        
        public float UnitValueToUnscaledValue(float unitValue)
        {
            return Mathf.Lerp(Range.x, Range.y, unitValue);
        }        

    }
} // namespace TiltBrush
