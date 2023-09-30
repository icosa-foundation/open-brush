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
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TiltBrush
{
    public class LocalePopUpWindow : PagingPopUpWindow
    {
        private string m_CurrentPresetIdCode;
        private Locale[] m_Locales;

        protected override int m_DataCount
        {
            get { return m_Locales.Length; }
        }

        protected override void InitIcon(ImageIcon icon)
        {
            icon.m_Valid = true;
        }

        protected override void RefreshIcon(PagingPopUpWindow.ImageIcon icon, int iCatalog)
        {
            LocaleButton iconButton = icon.m_IconScript as LocaleButton;
            iconButton.SetPreset(m_Locales[iCatalog]);
            iconButton.SetButtonSelected(m_CurrentPresetIdCode == m_Locales[iCatalog].Identifier.Code);
        }

        override public void Init(GameObject rParent, string sText)
        {
            //build list of locale presets we're going to show
            Locale currentSelectedLocale = LocalizationSettings.SelectedLocale;

            m_Locales = App.Instance.m_Manifest.Locales;

            int iPresetIndex = -1;
            m_CurrentPresetIdCode = currentSelectedLocale.Identifier.Code;

            for (int i = 0; i < m_Locales.Length; ++i)
            {
                if (m_Locales[i].Identifier.Code == m_CurrentPresetIdCode)
                {
                    iPresetIndex = i;
                    break;
                }
            }

            if (iPresetIndex != -1)
            {
                if (m_Locales.Length > m_IconCountFullPage)
                {
                    m_RequestedPageIndex = iPresetIndex / m_IconCountNavPage;
                }
            }

            base.Init(rParent, sText);

            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        void OnSelectedLocaleChanged(Locale locale)
        {
            if (locale != null)
            {
                m_CurrentPresetIdCode = locale.Identifier.Code;
            }
            RefreshPage();
            RequestClose();
        }

        void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

    }
} // namespace TiltBrush
