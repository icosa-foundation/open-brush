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
        private LuaManager.ApiCategory m_ApiCategory;
        private Table m_WidgetConfigs;
        private List<AdvancedSlider> m_Widgets;

        public override void SetPopupCommandParameters(int iCommandParam, int iCommandParam2)
        {
            m_Widgets = GetComponentsInChildren<AdvancedSlider>().ToList();
            m_ApiCategory = (LuaManager.ApiCategory)iCommandParam;
            var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
            m_WidgetConfigs = LuaManager.Instance.GetWidgetConfigs(script);
            int index = 0;
            foreach (var widget in m_Widgets)
            {
                var kvs = m_WidgetConfigs.Pairs.ToList();
                if (index < kvs.Count)
                {
                    widget.gameObject.SetActive(true);
                    var config = kvs[index];
                    widget.name = config.Key.String;
                    widget.SliderType = config.Value.Table.Get("type").String.ToLower() switch
                    {
                        "int" => SliderTypes.Int,
                        "float" => SliderTypes.Float,
                        _ => widget.SliderType
                    };
                    widget.SetMin((float)config.Value.Table.Get("min").Number);
                    widget.SetMax((float)config.Value.Table.Get("max").Number);
                    widget.SetDescriptionText(config.Value.Table.Get("label").String);

                    var val = LuaManager.Instance.GetOrSetWidgetCurrentValue(script, config);
                    widget.SetInitialValueAndUpdate(val);
                }
                else
                {
                    widget.gameObject.SetActive(false);
                }
                index++;
            }
        }

        public void OnSliderChanged(Vector3 sliderValue)
        {
            int sliderIndex = Mathf.FloorToInt(sliderValue.x);
            if (m_WidgetConfigs == null) return;
            var config = m_WidgetConfigs.Pairs.ToList()[sliderIndex];
            var paramName = config.Key.String;
            LuaManager.Instance.SetScriptParameterForActiveScript(m_ApiCategory, paramName, sliderValue.z);
        }
    }
} // namespace TiltBrush
