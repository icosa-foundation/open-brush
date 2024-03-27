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

using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    public class ScriptParametersPopUpWindow : OptionsPopUpWindow
    {
        private LuaApiCategory m_ApiCategory;
        private List<Transform> m_Widgets;
        public Transform m_SliderPrefab;
        public Transform m_ColorPickerButtonPrefab;
        public Transform m_ToggleButtonPrefab;
        public Transform m_TextInputPrefab;
        public Transform m_ListButtonPrefab;
        public Transform m_ImagePickerButtonPrefab;
        public Transform m_ModelPickerButtonPrefab;

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
                        var colorPickerButton = instance.GetComponent<OpenColorPickerPopupButton>();
                        colorPickerButton.m_CommandParam = i;
                        colorPickerButton.ColorPropertyName = propertyName;
                        colorPickerButton.ChosenColor = ((ColorApiWrapper)val.ToObject())._Color;
                        colorPickerButton.SetDescriptionText(config.label);
                        break;
                    case var value when value == LuaNames.widgetTypeToggle:
                        instance = Instantiate(m_ToggleButtonPrefab, transform);
                        instance.gameObject.SetActive(true); // ToggleState fails unless active so do it early
                        var toggleButton = instance.GetComponent<ScriptParametersToggleButton>();
                        toggleButton.SetDescriptionText(config.label);
                        toggleButton.m_PropertyName = propertyName;
                        toggleButton.ToggleState = val.Boolean;
                        break;
                    case var value when value == LuaNames.widgetTypeText:
                        instance = Instantiate(m_TextInputPrefab, transform);
                        var textInputButton = instance.GetComponent<OpenTextInputPopupButton>();
                        textInputButton.SetDescriptionText(config.label);
                        textInputButton.ButtonLabel = val.String;
                        textInputButton.m_PropertyName = propertyName;
                        break;
                    case var value when value == LuaNames.widgetTypeList:
                        instance = Instantiate(m_ListButtonPrefab, transform);
                        var listPickerButton = instance.GetComponent<OpenListPickerPopupButton>();
                        listPickerButton.SetDescriptionText(config.label);
                        listPickerButton.m_PropertyName = propertyName;
                        listPickerButton.m_Items = config.items.Select(x => x.String).ToList();
                        listPickerButton.ButtonLabel = val.String;
                        listPickerButton.ItemIndex = listPickerButton.m_Items.IndexOf(val.String);
                        break;
                    case var value when value == LuaNames.widgetTypeLayer:
                        instance = Instantiate(m_ListButtonPrefab, transform);
                        var layerPickerButton = instance.GetComponent<OpenListPickerPopupButton>();
                        layerPickerButton.SetDescriptionText(config.label);
                        layerPickerButton.m_PropertyName = propertyName;
                        layerPickerButton.m_Items = App.Scene.LayerCanvases.Select(x => x.name).ToList();
                        break;
                    case var value when value == LuaNames.widgetTypeBrush:
                        instance = Instantiate(m_ListButtonPrefab, transform);
                        var brushPickerButton = instance.GetComponent<OpenListPickerPopupButton>();
                        brushPickerButton.SetDescriptionText(config.label);
                        brushPickerButton.m_PropertyName = propertyName;
                        brushPickerButton.m_Items = BrushCatalog.m_Instance.AllBrushes.Select(x => x.name).ToList();
                        break;
                    case var value when value == LuaNames.widgetTypeImage:
                        instance = Instantiate(m_ImagePickerButtonPrefab, transform);
                        var imagePickerButton = instance.GetComponent<OpenImagePickerPopupButton>();
                        imagePickerButton.SetDescriptionText(config.label);
                        imagePickerButton.m_PropertyName = propertyName;
                        string filename = val.String;
                        int itemIndex = ReferenceImageCatalog.m_Instance.FilenameToIndex(filename);
                        if (itemIndex != -1)
                        {
                            ReferenceImage image = ReferenceImageCatalog.m_Instance.IndexToImage(itemIndex);
                            imagePickerButton.UpdateValue(
                                image.Icon,
                                imagePickerButton.m_PropertyName,
                                itemIndex,
                                image.ImageAspect
                            );
                        }
                        break;
                    case var value when value == LuaNames.widgetTypeVideo:
                        instance = Instantiate(m_ImagePickerButtonPrefab, transform);
                        var videoPickerButton = instance.GetComponent<OpenImagePickerPopupButton>();
                        videoPickerButton.SetDescriptionText(config.label);
                        videoPickerButton.m_PropertyName = propertyName;
                        break;
                    case var value when value == LuaNames.widgetTypeModel:
                        instance = Instantiate(m_ModelPickerButtonPrefab, transform);
                        var modelPickerButton = instance.GetComponent<OpenImagePickerPopupButton>();
                        modelPickerButton.SetDescriptionText(config.label);
                        modelPickerButton.m_PropertyName = propertyName;
                        break;
                }
                if (instance != null)
                {
                    instance.name = propertyName;
                    instance.transform.localPosition = new Vector3(0, 0.45f - (i * 0.28f), -0.03f);
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

        public void HandleColorParameterChanged((string propertyName, Color color) data)
        {
            var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
            var colorWrapper = new ColorApiWrapper(data.color);
            LuaManager.Instance.SetScriptParam(script, data.propertyName, UserData.Create(colorWrapper));
        }

        public void HandleBoolParameterChanged(ScriptParametersToggleButton btn)
        {
            var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
            LuaManager.Instance.SetScriptParam(script, btn.m_PropertyName, DynValue.NewBoolean(btn.ToggleState));
        }

        public void HandleStringInputParameterChanged(OpenTextInputPopupButton btn)
        {
            var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
            LuaManager.Instance.SetScriptParam(script, btn.m_PropertyName, DynValue.NewString(KeyboardPopUpWindow.m_LastInput));
        }

        public void HandleStringPickerParameterChanged(OpenListPickerPopupButton btn)
        {
            var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
            string text = btn.m_Items[btn.ItemIndex];
            LuaManager.Instance.SetScriptParam(script, btn.m_PropertyName, DynValue.NewString(text));
        }

        public void HandleImagePickerParameterChanged(OpenImagePickerPopupButton btn)
        {
            var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
            string imageFileName = ReferenceImageCatalog.m_Instance.IndexToImage(btn.ImageIndex).FileName;
            LuaManager.Instance.SetScriptParam(script, btn.m_PropertyName, DynValue.NewString(imageFileName));
        }

        public void HandleLayerParameterChanged(OpenListPickerPopupButton btn)
        {
            var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
            var layer = App.Scene.LayerCanvases.ElementAt(btn.ItemIndex);
            var layerWrapper = new LayerApiWrapper(layer);
            LuaManager.Instance.SetScriptParam(script, btn.m_PropertyName, UserData.Create(layerWrapper));
        }
    }
} // namespace TiltBrush
