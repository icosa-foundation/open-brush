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

namespace TiltBrush
{

    public class QualitySlider : BaseSlider
    {
        private float[] m_Steps;
        private bool m_HasAutomaticStep;
        private int m_DisplayedStep = -1;

        private int SelectedStep => m_HasAutomaticStep && QualityControls.m_Instance.AutomaticQualityEnabled
            ? 0
            : Mathf.Clamp(QualityControls.m_Instance.QualityLevel, 0,
                QualityControls.m_Instance.AppQualityLevels.Length - 1) + (m_HasAutomaticStep ? 1 : 0);

        override protected void Awake()
        {
            base.Awake();

            // Mobile supports dynamic quality. Its first position selects Auto,
            // followed by one position per manual level in the active app ladder.
            m_HasAutomaticStep = App.Config.IsMobileHardware;
            int iNumQualitySettings = Mathf.Max(QualityControls.m_Instance.AppQualityLevels.Length, 1) +
                (m_HasAutomaticStep ? 1 : 0);
            float fStepInterval = 1.0f / Mathf.Max(iNumQualitySettings - 1, 1);
            m_Steps = new float[iNumQualitySettings];
            for (int i = 0; i < iNumQualitySettings; ++i)
            {
                m_Steps[i] = i * fStepInterval;
            }

            //figure out where to initialize the position of the slider
            PositionNobAtCurrentQuality();
        }

        private void LateUpdate()
        {
            // API changes can switch mode or level while the panel is open.
            if (SelectedStep != m_DisplayedStep)
                PositionNobAtCurrentQuality();
        }

        void PositionNobAtCurrentQuality()
        {
            int selectedStep = SelectedStep;
            m_CurrentValue = m_Steps[selectedStep];
            Vector3 vLocalPos = m_Nob.transform.localPosition;
            vLocalPos.x = Mathf.Clamp(m_CurrentValue - 0.5f, -0.5f, 0.5f) * m_MeshScale.x;
            m_Nob.transform.localPosition = vLocalPos;
            if (selectedStep != m_DisplayedStep)
            {
                SetDescriptionText(m_DescriptionText, GetDescriptionExtraText());
                m_DisplayedStep = selectedStep;
            }
        }

        override public void UpdateValue(float fValue)
        {
            //find nearest step
            float fNearestDistance = 999.0f;
            int iNearestIndex = 0;
            for (int i = 0; i < m_Steps.Length; ++i)
            {
                float fAbsDiff = Mathf.Abs(fValue - m_Steps[i]);
                if (fAbsDiff < fNearestDistance)
                {
                    fNearestDistance = fAbsDiff;
                    iNearestIndex = i;
                }
            }

            //switch quality setting if needed
            if (iNearestIndex != SelectedStep)
            {
                if (!m_HasAutomaticStep)
                    iNearestIndex = Mathf.Clamp(iNearestIndex, SelectedStep - 1, SelectedStep + 1);
                var quality = QualityControls.m_Instance;
                if (m_HasAutomaticStep && iNearestIndex == 0)
                {
                    quality.AutomaticQualityEnabled = true;
                }
                else
                {
                    // A manual choice is held for this session until Auto is selected.
                    if (m_HasAutomaticStep) quality.AutomaticQualityEnabled = false;
                    quality.QualityLevel = iNearestIndex - (m_HasAutomaticStep ? 1 : 0);
                }
                AudioManager.m_Instance.PlaySliderSound(m_Nob.transform.position);
            }

            PositionNobAtCurrentQuality();
        }

        string GetDescriptionExtraText()
        {
            if (m_HasAutomaticStep)
            {
                return SelectedStep == 0 ? "Automatic" :
                    $"Manual level {SelectedStep} of {QualityControls.m_Instance.AppQualityLevels.Length}";
            }
            return QualitySettings.names[SelectedStep];
        }
    }
} // namespace TiltBrush
