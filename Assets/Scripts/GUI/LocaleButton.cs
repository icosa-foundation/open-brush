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

using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;

namespace TiltBrush
{
    public class LocaleButton : BaseButton
    {
        [Header("Locale Text")]
        private Locale m_Preset;
        [SerializeField] private TextMeshPro m_LocaleText;

        public void SetPreset(Locale rPreset)
        {
            m_Preset = rPreset;

            SetDescriptionText(m_Preset.LocaleName);

            m_LocaleText.text = m_Preset.Identifier.Code;
        }

        override protected void OnButtonPressed()
        {
            LocalizationSettings.SelectedLocale = m_Preset;
        }
    }
} // namespace TiltBrush
