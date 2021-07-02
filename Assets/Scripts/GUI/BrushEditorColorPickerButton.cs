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
    public class BrushEditorColorPickerButton : OptionButton
    {
        [NonSerialized] public EditBrushPanel ParentPanel;
        public string ColorPropertyName;
        [SerializeField] private GameObject[] m_ObjectsToHideBehindPopups;
        private Color m_chosenColor;
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
            for (int i = 0; i < m_ObjectsToHideBehindPopups.Length; ++i)
            {
                m_ObjectsToHideBehindPopups[i].SetActive(false);
            }
        
            var popupText = m_Description;
            BasePanel panel = m_Manager.GetPanelForPopUps();
            panel.CreatePopUp(m_Command, m_CommandParam, m_CommandParam2, m_PopupOffset, m_PopupText);

            var popup = (panel.PanelPopUp as ColorPickerPopUpWindow);
            if (popup != null)
            {
                popup.ColorPicker.Controller.CurrentColor = ChosenColor;
                // Init must be called after all popup.ColorPicked actions have been assigned.
                popup.ColorPicker.ColorPicked += OnColorPicked;
                popup.ColorPicker.Controller.CurrentColor = SceneSettings.m_Instance.SkyColorA;
                popup.ColorPicker.ColorFinalized += ColorFinalized;
                popup.CustomColorPalette.ColorPicked += OnColorPickedAsFinal;
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
        
        void OnPopUpClose()
        {
            for (int i = 0; i < m_ObjectsToHideBehindPopups.Length; ++i)
            {
                m_ObjectsToHideBehindPopups[i].SetActive(true);
            }
        }
        
        void OnColorPicked(Color color)
        {
            ParentPanel.ColorChanged(ColorPropertyName, color, this);
            ChosenColor = color;
        }

        void ColorFinalized()
        {
            Debug.Log($"ColorFinalized");
        }
        
        void OnColorPickedAsFinal(Color color)
        {
            // Not used?
            Debug.Log($"OnColorPickedAsFinal");
        }
    }
} // namespace TiltBrush
