// Copyright 2022 The Tilt Brush Authors
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
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    public class ScriptParametersPopUpWindow : OptionsPopUpWindow
    {
        private LuaApiCategory m_ApiCategory;
        private List<AdvancedSlider> m_Widgets;

        public override void SetPopupCommandParameters(int iCommandParam, int iCommandParam2)
        {
            m_Widgets = GetComponentsInChildren<AdvancedSlider>().ToList();
            m_ApiCategory = (LuaApiCategory)iCommandParam;
            var scriptName = LuaManager.Instance.GetActiveScriptName(m_ApiCategory);
            var widgetConfigs = LuaManager.Instance.GetWidgetConfigs(scriptName);
            m_Widgets.ForEach(w => w.gameObject.SetActive(false));
            for (int i = 0; i < widgetConfigs.Count; i++)
            {
                var name = widgetConfigs.Keys.ElementAt(i);
                var config = widgetConfigs[name];
                var widget = m_Widgets[i];
                widget.gameObject.SetActive(true);
                widget.name = name;
                widget.SliderType = config.type.ToLower() switch
                {
                    "int" => SliderTypes.Int,
                    "float" => SliderTypes.Float,
                    _ => widget.SliderType
                };
                widget.SetMin(config.min);
                widget.SetMax(config.max);
                widget.SetDescriptionText(config.label);

                var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
                var val = LuaManager.Instance.GetOrSetWidgetCurrentValue(script, name, config);
                widget.SetInitialValueAndUpdate(val);
            }
        }

        public void OnSliderChanged(Vector3 sliderValue)
        {
            int sliderIndex = Mathf.FloorToInt(sliderValue.x);
            var slider = m_Widgets[sliderIndex];
            var paramName = slider.name;
            LuaManager.Instance.SetScriptParameterForActiveScript(m_ApiCategory, paramName, sliderValue.z);
        }
    }
} // namespace TiltBrush
