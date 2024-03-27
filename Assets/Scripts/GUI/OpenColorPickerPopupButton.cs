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

using UnityEngine;
using UnityEngine.Events;

namespace TiltBrush
{
    public class OpenColorPickerPopupButton : OptionButton
    {
        public string ColorPropertyName;
        public Transform m_ColorPickerPopup;
        private Color m_chosenColor;
        [SerializeField] private UnityEvent<(string propertyName, Color color)> m_Action;

        public Color ChosenColor
        {
            get
            {
                return m_chosenColor;
            }
            set
            {
                GetComponent<Renderer>().material.color = value;
                m_chosenColor = value;
            }
        }

        override protected void OnButtonPressed()
        {
            BasePanel panel = m_Manager.GetPanelForPopUps();
            if (panel != null)
            {
                float zOffset = App.Config.m_SdkMode == SdkMode.Monoscopic ? 0.3f : -0.3f;
                panel.CreatePopUp(
                    m_ColorPickerPopup.gameObject,
                    transform.position + Vector3.forward * zOffset,
                    true, true
                );
                ResetState();
            }

            var popup = panel.PanelPopUp as ColorPickerPopUpWindow;
            if (popup != null)
            {
                popup.ColorPicker.Controller.CurrentColor = ChosenColor;
                // Init must be called after all popup.ColorPicked actions have been assigned.
                popup.ColorPicker.ColorPicked += OnColorPicked;
                popup.ColorPicker.Controller.CurrentColor = SceneSettings.m_Instance.SkyColorA;
                popup.ColorPicker.ColorFinalized += ColorFinalized;
            }
        }

        public override void SetColor(Color color)
        {
            // Override this and ignore the color changes from the UI system
            base.SetColor(ChosenColor);
        }

        public override void GazeRatioChanged(float gazeRatio)
        {
            GetComponent<Renderer>().material.SetFloat("_Distance", gazeRatio);
        }

        void OnColorPicked(Color color)
        {
            m_Action.Invoke((ColorPropertyName, color));
            ChosenColor = color;
        }

        void ColorFinalized()
        {
            Debug.Log($"ColorFinalized");
        }
    }
} // namespace TiltBrush
