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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class MirrorOptionsPopUpWindow : OptionsPopUpWindow
    {
        public TextActionButton m_ButtonShowPointControls;
        public TextActionButton m_ButtonShowWallpaperControls;
        public TextActionButton m_ButtonShowOptionsControls;

        public GameObject m_PointSymmetryControls;
        public GameObject m_WallpaperSymmetryControls;
        public GameObject m_OptionsControls;
        public AdvancedSlider m_PointSymmetryOrderSlider;
        public AdvancedSlider m_WallpaperRepeatXSlider;
        public AdvancedSlider m_WallpaperRepeatYSlider;
        public AdvancedSlider m_WallpaperScaleSlider;
        public AdvancedSlider m_WallpaperScaleSliderX;
        public AdvancedSlider m_WallpaperScaleSliderY;
        public AdvancedSlider m_WallpaperSkewSliderX;
        public AdvancedSlider m_WallpaperSkewSliderY;

        public TextActionButton m_OptionsButtonHue;
        public TextActionButton m_OptionsButtonSaturation;
        public TextActionButton m_OptionsButtonBrightness;
        public AdvancedSlider m_OptionsSliderAmp;
        public AdvancedSlider m_OptionsSliderFreq;
        public TextActionButton m_OptionsButtonSine;
        public TextActionButton m_OptionsButtonTriangle;
        public TextActionButton m_OptionsButtonSawtooth;
        public TextActionButton m_OptionsButtonSquare;
        public TextActionButton m_OptionsButtonNoise;

        public GameObject m_ColorPreview;
        public Transform m_ColorPreviewSwatch;

        public ActionButton m_ButtonWallpaperRepeats;
        public ActionButton m_ButtonWallpaperScale;
        public ActionButton m_ButtonWallpaperSkew;

        public Transform m_WallpaperRepeatsControls;
        public Transform m_WallpaperScaleControls;
        public Transform m_WallpaperSkewControls;

        private static int m_LastActiveTab;
        private bool m_MirrorState;

        [NonSerialized] public PointerManager.ColorShiftComponent m_currentSelectedColorComponent;
        public enum SymmetryTransformAxis { X, Y, Z };
        public enum SymmetryTransformType { Position, Rotation };
        public SymmetryTransformAxis m_CurrentSymmetryTransformAxis;
        public SymmetryTransformType m_CurrentSymmetryTransformType;
        public AdvancedSlider m_SymmetryTransformValueSlider;

        public override void Init(GameObject rParent, string sText)
        {
            base.Init(rParent, sText);
            // Store mirror state as the long press button misbehaves sometimes
            m_MirrorState = GetParentButton().IsButtonActive();

            switch (m_LastActiveTab)
            {
                case 0:
                    HandleShowPointSymmetry();
                    break;
                case 1:
                    HandleShowWallpaperSymmetry();
                    break;
                case 2:
                    HandleShowOptions();
                    break;
            }
            SetCurrentMirrorTypeButtonState(true);

            m_PointSymmetryOrderSlider.SetInitialValueAndUpdate(PointerManager.m_Instance.m_PointSymmetryOrder);
            m_WallpaperScaleSlider.SetInitialValueAndUpdate(PointerManager.m_Instance.m_WallpaperSymmetryScale);
            m_WallpaperRepeatXSlider.SetInitialValueAndUpdate(PointerManager.m_Instance.m_WallpaperSymmetryX);
            m_WallpaperRepeatYSlider.SetInitialValueAndUpdate(PointerManager.m_Instance.m_WallpaperSymmetryY);
            m_WallpaperScaleSliderX.SetInitialValueAndUpdate(PointerManager.m_Instance.m_WallpaperSymmetryScaleX);
            m_WallpaperScaleSliderY.SetInitialValueAndUpdate(PointerManager.m_Instance.m_WallpaperSymmetryScaleY);
            m_WallpaperSkewSliderX.SetInitialValueAndUpdate(PointerManager.m_Instance.m_WallpaperSymmetrySkewX);
            m_WallpaperSkewSliderY.SetInitialValueAndUpdate(PointerManager.m_Instance.m_WallpaperSymmetrySkewY);
        }

        public OptionButton GetParentButton()
        {
            return m_ParentPanel.GetComponentsInChildren<LongPressButton>().First(
                b => b.m_Command == SketchControlsScript.GlobalCommands.MultiMirror
            );
        }

        public override bool RequestClose(bool bForceClose = false)
        {
            bool close = base.RequestClose(bForceClose);
            if (close)
            {
                // Restore mirror state as the long press button misbehaves sometimes
                if (GetParentButton().IsButtonActive() != m_MirrorState)
                {
                    SketchControlsScript.m_Instance.IssueGlobalCommand(SketchControlsScript.GlobalCommands.MultiMirror);
                }
            }
            return close;
        }

        public void HandleWallpaperControlsRepeatsButton()
        {
            m_WallpaperRepeatsControls.gameObject.SetActive(true);
            m_WallpaperScaleControls.gameObject.SetActive(false);
            m_WallpaperSkewControls.gameObject.SetActive(false);

            m_ButtonWallpaperRepeats.SetButtonSelected(true);
            m_ButtonWallpaperScale.SetButtonSelected(false);
            m_ButtonWallpaperSkew.SetButtonSelected(false);
        }

        public void HandleWallpaperControlsScaleButton()
        {
            m_WallpaperRepeatsControls.gameObject.SetActive(false);
            m_WallpaperScaleControls.gameObject.SetActive(true);
            m_WallpaperSkewControls.gameObject.SetActive(false);

            m_ButtonWallpaperRepeats.SetButtonSelected(false);
            m_ButtonWallpaperScale.SetButtonSelected(true);
            m_ButtonWallpaperSkew.SetButtonSelected(false);
        }

        public void HandleWallpaperControlsSkewButton()
        {
            m_WallpaperRepeatsControls.gameObject.SetActive(false);
            m_WallpaperScaleControls.gameObject.SetActive(false);
            m_WallpaperSkewControls.gameObject.SetActive(true);

            m_ButtonWallpaperRepeats.SetButtonSelected(false);
            m_ButtonWallpaperScale.SetButtonSelected(false);
            m_ButtonWallpaperSkew.SetButtonSelected(true);
        }

        private void SetCurrentMirrorTypeButtonState(bool state)
        {
            GameObject parent;
            MirrorTypeButton[] btns;
            MirrorTypeButton currentBtn;
            switch (PointerManager.m_Instance.m_CustomSymmetryType)
            {
                case PointerManager.CustomSymmetryType.Point:
                    parent = m_PointSymmetryControls;
                    btns = parent.GetComponentsInChildren<MirrorTypeButton>();
                    currentBtn = btns.First(b => b.m_PointSymmetryFamily == PointerManager.m_Instance.m_PointSymmetryFamily);
                    currentBtn.SetButtonSelected(state);
                    break;
                case PointerManager.CustomSymmetryType.Wallpaper:
                    parent = m_WallpaperSymmetryControls;
                    btns = parent.GetComponentsInChildren<MirrorTypeButton>();
                    currentBtn = btns.First(b => b.m_WallpaperSymmetryGroup == PointerManager.m_Instance.m_WallpaperSymmetryGroup);
                    currentBtn.SetButtonSelected(state);
                    break;
            }

        }

        public void HandleChangeMirrorTypeButton(MirrorTypeButton btn)
        {
            SetCurrentMirrorTypeButtonState(false);
            PointerManager.m_Instance.m_CustomSymmetryType = btn.m_CustomSymmetryType;
            switch (btn.m_CustomSymmetryType)
            {
                case PointerManager.CustomSymmetryType.Point:
                    PointerManager.m_Instance.m_PointSymmetryFamily = btn.m_PointSymmetryFamily;
                    break;
                case PointerManager.CustomSymmetryType.Wallpaper:
                    PointerManager.m_Instance.m_WallpaperSymmetryGroup = btn.m_WallpaperSymmetryGroup;
                    UpdateWallpaperSettingControls();
                    break;
            }
            SetCurrentMirrorTypeButtonState(true);
            // Regenerate
            PointerManager.m_Instance.CalculateMirrors();
        }

        private void UpdateWallpaperSettingControls()
        {
            switch (PointerManager.m_Instance.m_WallpaperSymmetryGroup)
            {
                // Skew and 2 size DOFs
                case SymmetryGroup.R.p1:
                case SymmetryGroup.R.p2:

                    m_ButtonWallpaperRepeats.SetButtonAvailable(true);
                    m_ButtonWallpaperScale.SetButtonAvailable(true);
                    m_ButtonWallpaperSkew.SetButtonAvailable(true);

                    break;

                // Just 2 size DOFs
                case SymmetryGroup.R.pg:
                case SymmetryGroup.R.pm:
                case SymmetryGroup.R.cm:
                case SymmetryGroup.R.pmm:

                    m_ButtonWallpaperRepeats.SetButtonAvailable(true);
                    m_ButtonWallpaperScale.SetButtonAvailable(true);
                    m_ButtonWallpaperSkew.SetButtonAvailable(false);

                    m_WallpaperSkewControls.gameObject.SetActive(false);

                    break;

                // Just 1 size DOF
                case SymmetryGroup.R.p6:
                case SymmetryGroup.R.p6m:
                case SymmetryGroup.R.p3:
                case SymmetryGroup.R.p3m1:
                case SymmetryGroup.R.p31m:
                case SymmetryGroup.R.p4:
                case SymmetryGroup.R.p4m:
                case SymmetryGroup.R.p4g:
                case SymmetryGroup.R.pgg:
                case SymmetryGroup.R.pmg:
                case SymmetryGroup.R.cmm:

                    m_ButtonWallpaperRepeats.SetButtonAvailable(true);
                    m_ButtonWallpaperScale.SetButtonAvailable(false);
                    m_ButtonWallpaperSkew.SetButtonAvailable(false);

                    m_WallpaperScaleControls.gameObject.SetActive(false);
                    m_WallpaperSkewControls.gameObject.SetActive(false);

                    break;
            }
        }

        public void HandleTransformOptionButton(TextActionButton btn)
        {
            switch (btn.m_ButtonLabel)
            {
                case "X":
                    m_CurrentSymmetryTransformAxis = SymmetryTransformAxis.X;
                    break;
                case "Y":
                    m_CurrentSymmetryTransformAxis = SymmetryTransformAxis.Y;
                    break;
                case "Z":
                    m_CurrentSymmetryTransformAxis = SymmetryTransformAxis.Z;
                    break;
                case "Position":
                    m_CurrentSymmetryTransformType = SymmetryTransformType.Position;
                    break;
                case "Rotation":
                    m_CurrentSymmetryTransformType = SymmetryTransformType.Rotation;
                    break;
            }
            SymmetryTransformOptionChanged();
        }

        public void HandleTransformEachAfter(ActionButton btn)
        {
            PointerManager.m_Instance.m_SymmetryTransformEachAfter = !PointerManager.m_Instance.m_SymmetryTransformEachAfter;
            SymmetryTransformOptionChanged();
        }

        private void SymmetryTransformOptionChanged()
        {
            TrTransform transformEach = PointerManager.m_Instance.m_SymmetryTransformEach;
            float value = 0;
            switch (m_CurrentSymmetryTransformType)
            {
                case SymmetryTransformType.Position:
                    m_SymmetryTransformValueSlider.m_safeMin = -10;
                    m_SymmetryTransformValueSlider.m_safeMax = 10;
                    m_SymmetryTransformValueSlider.m_unsafeMin = -50;
                    m_SymmetryTransformValueSlider.m_unsafeMax = 50;
                    m_SymmetryTransformValueSlider.HandleChangeLimits();
                    value = transformEach.translation[(int)m_CurrentSymmetryTransformAxis];
                    break;
                case SymmetryTransformType.Rotation:
                    var currentEuler = transformEach.rotation.eulerAngles;
                    m_SymmetryTransformValueSlider.m_safeMin = -180;
                    m_SymmetryTransformValueSlider.m_safeMax = 180;
                    m_SymmetryTransformValueSlider.m_unsafeMin = -360;
                    m_SymmetryTransformValueSlider.m_unsafeMax = 360;
                    m_SymmetryTransformValueSlider.HandleChangeLimits();
                    value = currentEuler[(int)m_CurrentSymmetryTransformAxis];
                    break;
            }
            m_SymmetryTransformValueSlider.UpdateValueAbsolute(value);
        }


        public void HandleTransformSlider(Vector3 value)
        {
            TrTransform transformEach = PointerManager.m_Instance.m_SymmetryTransformEach;

            switch (m_CurrentSymmetryTransformType)
            {
                case SymmetryTransformType.Position:
                    transformEach.translation[(int)m_CurrentSymmetryTransformAxis] = value.z;
                    break;
                case SymmetryTransformType.Rotation:
                    var currentEuler = transformEach.rotation.eulerAngles;
                    currentEuler[(int)m_CurrentSymmetryTransformAxis] = value.z;
                    transformEach.rotation = Quaternion.Euler(currentEuler);
                    break;
            }
            PointerManager.m_Instance.m_SymmetryTransformEach = transformEach;
            // Regenerate
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangeTransformXY(Vector3 value)
        {
            int typeParam = Mathf.FloorToInt(value.x);
            var valueXY = new Vector2(value.y, value.z);
            TrTransform transformEach = PointerManager.m_Instance.m_SymmetryTransformEach;
            switch (typeParam)
            {
                case 0:
                    transformEach.translation = new Vector3(0, valueXY.x, valueXY.y);
                    break;
                case 1:
                    transformEach.rotation = Quaternion.Euler(new Vector3(valueXY.x, valueXY.y, 0));
                    break;
                case 2:
                    transformEach.scale = valueXY.x;
                    break;
            }
            PointerManager.m_Instance.m_SymmetryTransformEach = transformEach;
            // Regenerate
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangePointSymmetryOrder(Vector3 value)
        {
            PointerManager.m_Instance.m_PointSymmetryOrder = Mathf.FloorToInt(value.z);
            // Regenerate
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangeWallpaperSymmetryX(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryX = Mathf.FloorToInt(value.z);
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangeWallpaperSymmetryY(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryY = Mathf.FloorToInt(value.z);
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangeWallpaperSymmetryScale(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScale = value.z;
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangeWallpaperSymmetryScaleX(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScaleX = value.z;
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangeWallpaperSymmetryScaleY(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScaleY = value.z;
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangeWallpaperSymmetrySkewX(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetrySkewX = value.z;
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangeWallpaperSymmetrySkewY(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetrySkewY = value.z;
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleShowPointSymmetry()
        {
            m_LastActiveTab = 0;
            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Point;
            m_PointSymmetryControls.SetActive(true);
            m_WallpaperSymmetryControls.SetActive(false);
            m_OptionsControls.SetActive(false);
            PointerManager.m_Instance.CalculateMirrors();
            m_ButtonShowPointControls.SetButtonSelected(true);
            m_ButtonShowWallpaperControls.SetButtonSelected(false);
            m_ButtonShowOptionsControls.SetButtonSelected(false);
        }

        public void HandleShowWallpaperSymmetry()
        {
            m_LastActiveTab = 1;
            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Wallpaper;
            m_PointSymmetryControls.SetActive(false);
            m_WallpaperSymmetryControls.SetActive(true);
            m_OptionsControls.SetActive(false);
            PointerManager.m_Instance.CalculateMirrors();
            m_ButtonShowPointControls.SetButtonSelected(false);
            m_ButtonShowWallpaperControls.SetButtonSelected(true);
            m_ButtonShowOptionsControls.SetButtonSelected(false);
        }

        public void HandleShowOptions()
        {
            m_LastActiveTab = 2;
            m_PointSymmetryControls.SetActive(false);
            m_WallpaperSymmetryControls.SetActive(false);
            m_OptionsControls.SetActive(true);
            PointerManager.m_Instance.CalculateMirrors();
            UpdateColorPreview();
            UpdateOptionsControlsToMatchValues();
            m_ButtonShowPointControls.SetButtonSelected(false);
            m_ButtonShowWallpaperControls.SetButtonSelected(false);
            m_ButtonShowOptionsControls.SetButtonSelected(true);
        }

        private void UpdateOptionsControlsToMatchValues()
        {
            var currentSettings = m_currentSelectedColorComponent switch
            {
                PointerManager.ColorShiftComponent.Hue => PointerManager.m_Instance.m_SymmetryColorShiftSettingHue,
                PointerManager.ColorShiftComponent.Saturation => PointerManager.m_Instance.m_SymmetryColorShiftSettingSaturation,
                PointerManager.ColorShiftComponent.Brightness => PointerManager.m_Instance.m_SymmetryColorShiftSettingBrightness,
                _ => throw new ArgumentOutOfRangeException()
            };
            m_OptionsSliderAmp.UpdateValueAbsolute(currentSettings.amp);
            m_OptionsSliderFreq.UpdateValueAbsolute(currentSettings.freq);
            m_OptionsButtonSine.SetButtonSelected(currentSettings.mode == PointerManager.ColorShiftMode.SineWave);
            m_OptionsButtonSquare.SetButtonSelected(currentSettings.mode == PointerManager.ColorShiftMode.SquareWave);
            m_OptionsButtonTriangle.SetButtonSelected(currentSettings.mode == PointerManager.ColorShiftMode.TriangleWave);
            m_OptionsButtonSawtooth.SetButtonSelected(currentSettings.mode == PointerManager.ColorShiftMode.SawtoothWave);
            m_OptionsButtonNoise.SetButtonSelected(currentSettings.mode == PointerManager.ColorShiftMode.Noise);
        }

        public void HandleColorComponentButtons(TextActionButton btn)
        {
            switch (btn.m_ButtonLabel)
            {
                case "Hue":
                    m_currentSelectedColorComponent = PointerManager.ColorShiftComponent.Hue;
                    break;
                case "Saturation":
                    m_currentSelectedColorComponent = PointerManager.ColorShiftComponent.Saturation;
                    break;
                case "Brightness":
                    m_currentSelectedColorComponent = PointerManager.ColorShiftComponent.Brightness;
                    break;
            }
            UpdateOptionsControlsToMatchValues();
        }

        public void HandleWaveformButtons(TextActionButton btn)
        {
            switch (btn.m_ButtonLabel.ToLower())
            {
                case "sine":
                    UpdateActiveColorShiftMode(PointerManager.ColorShiftMode.SineWave);
                    break;
                case "triangle":
                    UpdateActiveColorShiftMode(PointerManager.ColorShiftMode.TriangleWave);
                    break;
                case "sawtooth":
                    UpdateActiveColorShiftMode(PointerManager.ColorShiftMode.SawtoothWave);
                    break;
                case "square":
                    UpdateActiveColorShiftMode(PointerManager.ColorShiftMode.SquareWave);
                    break;
                case "noise":
                    UpdateActiveColorShiftMode(PointerManager.ColorShiftMode.Noise);
                    break;
            }
        }

        public void HandleChangeAmp(Vector3 value)
        {
            UpdateActiveColorShiftValues(freq: -1, amp: value.z);
        }

        public void HandleChangeFreq(Vector3 value)
        {
            UpdateActiveColorShiftValues(freq: value.z, amp: -1);
        }

        private void UpdateActiveColorShiftValues(float freq, float amp)
        {
            PointerManager.ColorShiftComponentSetting settings;
            switch (m_currentSelectedColorComponent)
            {
                case PointerManager.ColorShiftComponent.Hue:
                    settings = PointerManager.m_Instance.m_SymmetryColorShiftSettingHue;
                    settings.amp = amp != -1 ? amp : settings.amp;
                    settings.freq = freq != -1 ? freq : settings.freq;
                    PointerManager.m_Instance.m_SymmetryColorShiftSettingHue = settings;
                    break;
                case PointerManager.ColorShiftComponent.Saturation:
                    settings = PointerManager.m_Instance.m_SymmetryColorShiftSettingSaturation;
                    settings.amp = amp != -1 ? amp : settings.amp;
                    settings.freq = freq != -1 ? freq : settings.freq;
                    PointerManager.m_Instance.m_SymmetryColorShiftSettingSaturation = settings;
                    break;
                case PointerManager.ColorShiftComponent.Brightness:
                    settings = PointerManager.m_Instance.m_SymmetryColorShiftSettingBrightness;
                    settings.amp = amp != -1 ? amp : settings.amp;
                    settings.freq = freq != -1 ? freq : settings.freq;
                    PointerManager.m_Instance.m_SymmetryColorShiftSettingBrightness = settings;
                    break;
            }
            PointerManager.m_Instance.CalculateMirrors();
            UpdateColorPreview();
        }

        private void UpdateActiveColorShiftMode(PointerManager.ColorShiftMode mode)
        {
            switch (m_currentSelectedColorComponent)
            {
                case PointerManager.ColorShiftComponent.Hue:
                    PointerManager.m_Instance.m_SymmetryColorShiftSettingHue.mode = mode;
                    break;
                case PointerManager.ColorShiftComponent.Saturation:
                    PointerManager.m_Instance.m_SymmetryColorShiftSettingSaturation.mode = mode;
                    break;
                case PointerManager.ColorShiftComponent.Brightness:
                    PointerManager.m_Instance.m_SymmetryColorShiftSettingBrightness.mode = mode;
                    break;
            }
            PointerManager.m_Instance.CalculateMirrors();
            UpdateColorPreview();
            UpdateOptionsControlsToMatchValues();
        }

        private void UpdateColorPreview()
        {
            foreach (Transform swatch in m_ColorPreview.transform)
            {
                Destroy(swatch.gameObject);
            }
            var colors = PointerManager.m_Instance.SymmetryPointerColors;
            for (int i = 0; i < colors.Count; i++)
            {
                var instance = Instantiate(m_ColorPreviewSwatch, m_ColorPreview.transform);
                var sr = instance.GetComponent<SpriteRenderer>();
                sr.color = colors[i];
                sr.sortingOrder = i;
                float x = (float)i / colors.Count;
                Transform tr = instance.transform;
                var xPos = Mathf.Lerp(-.6f, .6f, x);
                tr.localPosition = new Vector3(xPos, 0, 0);
                tr.localScale = new Vector3(1.2f / colors.Count, .1f, 1);

            }
        }


    }
}
