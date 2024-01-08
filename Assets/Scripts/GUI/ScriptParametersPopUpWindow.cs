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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace TiltBrush
{
    public class ScriptParametersPopUpWindow : OptionsPopUpWindow
    {
        private LuaApiCategory m_ApiCategory;
        private List<Transform> m_Widgets;
        public Transform m_SliderPrefab;
        public Transform m_ColorPickerButtonPrefab;

        public override void SetPopupCommandParameters(int iCommandParam, int iCommandParam2)
        {
            m_ApiCategory = (LuaApiCategory)iCommandParam;
            var scriptName = LuaManager.Instance.GetActiveScriptName(m_ApiCategory);
            var widgetConfigs = LuaManager.Instance.GetWidgetConfigs(scriptName);
            if (m_Widgets != null)
            {
                m_Widgets.ForEach(Destroy);
            }
            m_Widgets = new List<Transform>();
            for (int i = 0; i < widgetConfigs.Count; i++)
            {
                var propertyName = widgetConfigs.Keys.ElementAt(i);
                var config = widgetConfigs[propertyName];
                var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
                var val = LuaManager.Instance.GetOrSetWidgetCurrentValue(script, propertyName, config);

                Transform instance = null;
                switch (config.type.ToLower())
                {
                    case var value when value == LuaNames.widgetTypeInt || value == LuaNames.widgetTypeFloat:
                        instance = Instantiate(m_SliderPrefab, transform);
                        var slider = instance.GetComponent<AdvancedSlider>();
                        slider.SliderType = value == LuaNames.widgetTypeInt ? SliderTypes.Int : SliderTypes.Float;
                        slider.m_Param1 = i;
                        slider.SetMin((float)config.min.Number);
                        slider.SetMax((float)config.max.Number);
                        slider.SetDescriptionText(config.label);
                        slider.SetInitialValueAndUpdate((float)val.Number);
                        break;
                    case var value when value == LuaNames.widgetTypeColor:
                        instance = Instantiate(m_ColorPickerButtonPrefab, transform);
                        var colorPickerButton = instance.GetComponent<ColorPickerButton>();
                        colorPickerButton.m_CommandParam = i;
                        colorPickerButton.ColorPropertyName = propertyName;
                        colorPickerButton.ChosenColor = ((ColorApiWrapper)val.ToObject())._Color;
                        colorPickerButton.SetDescriptionText(config.label);
                        break;
                }
                if (instance != null)
                {
                    instance.name = propertyName;
                    instance.transform.localPosition = new Vector3(0, 0.5f - (i * 0.3f), -0.03f);
                    instance.gameObject.SetActive(true);
                    m_Widgets.Add(instance);
                }
            }
        }

        public void OnSliderChanged(Vector3 sliderValue)
        {
            int sliderIndex = Mathf.FloorToInt(sliderValue.x);
            var slider = m_Widgets[sliderIndex];
            var paramName = slider.name;
            LuaManager.Instance.SetScriptFloatParamForActiveScript(m_ApiCategory, paramName, sliderValue.z);
        }

        public void OnColorChanged((string propertyName, Color color, ColorPickerButton btn) data)
        {
            var (propertyName, color, btn) = data;
            LuaManager.Instance.SetScriptColorParamForActiveScript(m_ApiCategory, propertyName, color);
        }
    }
} // namespace TiltBrush
