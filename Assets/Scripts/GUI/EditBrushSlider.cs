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
        public int? VectorComponent;

        public string GenerateDescription(float unscaledValue)
        {
            // Strip the leading underscore
            string description = FloatPropertyName.Substring(1);
            // For vector parameters we want to append xyzw
            description = VectorComponent.HasValue ?
                $"{description}{"XYZW".ToCharArray()[VectorComponent.Value]}" :
                description;
            // Append the current value
            description = $"{description}={unscaledValue:0.##}";
            return description;
        }
        
        public void UpdateValueIgnoreParent(float unscaledValue)
        {
            float unitValue = UnscaledValueToUnitValue(unscaledValue);
            SetDescriptionText(GenerateDescription(unscaledValue));
            base.UpdateValue(unitValue);
        }
        
        public override void UpdateValue(float unitValue)
        {
            float unscaledValue = UnitValueToUnscaledValue(unitValue);
            ParentPanel.SliderChanged(FloatPropertyName, unscaledValue, VectorComponent);
            SetDescriptionText(GenerateDescription(unscaledValue));
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
