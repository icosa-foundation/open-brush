// Copyright 2020 The Open Brush Authors
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
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using System;

namespace TiltBrush
{

    public class TutorialCard : MonoBehaviour
    {
        [SerializeField] private Transform m_Mesh;
        [SerializeField] private TextMeshPro m_Text;
        [SerializeField] private LocalizedString m_Description;

        private void Awake()
        {
            SetText();

            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        public void SetText()
        {
            m_Text.text = m_Description.GetLocalizedStringAsync().Result;

            // Measure length of button description by getting render bounds when mesh is axis-aligned.
            float fTextWidth = TextMeasureScript.m_Instance.GetTextWidth(
                m_Text.fontSize, m_Text.font, "     " + m_Text.text);

            Vector3 vBGScale = m_Mesh.localScale;
            vBGScale.x = fTextWidth;
            m_Mesh.localScale = vBGScale;
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            SetText();
        }




    }
} // namespace TiltBrush
