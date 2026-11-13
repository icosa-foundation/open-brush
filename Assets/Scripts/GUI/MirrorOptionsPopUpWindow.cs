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
using UnityEngine.Serialization;
using UnityEngine.Localization.Settings;

namespace TiltBrush
{
    public class MirrorOptionsPopUpWindow : OptionsPopUpWindow
    {
        public TextActionButton m_ButtonShowSimpleControls;
        public TextActionButton m_ButtonShowPointControls;
        public TextActionButton m_ButtonShowWallpaperControls;
        public TextActionButton m_ButtonShowColorControls;
        public TextActionButton m_ButtonShowPenroseControls;
        public TextActionButton m_ButtonShowLineGroupControls;
        public TextActionButton m_ButtonShowSpaceGroupControls;
        public TextActionButton m_ButtonShowHelicalControls;
        [SerializeField] private ActionButton m_UnlinkAllButton;
        [SerializeField] private ActionToggleButton m_HideExtraPreviewGizmosButton;
        private float m_NextPointerLimitNotice;
        private bool m_RefreshRejectedCount;

        public GameObject m_SimpleControls;

        public GameObject m_PointSymmetryControls;
        public AdvancedSlider m_PointSymmetryOrderSlider;

        public GameObject m_WallpaperSymmetryControls;
        public AdvancedSlider m_WallpaperRepeatXSlider;
        public AdvancedSlider m_WallpaperRepeatYSlider;
        public AdvancedSlider m_WallpaperScaleSlider;
        public AdvancedSlider m_WallpaperScaleSliderX;
        public AdvancedSlider m_WallpaperScaleSliderY;
        public AdvancedSlider m_WallpaperSkewSliderX;
        public AdvancedSlider m_WallpaperSkewSliderY;
        public ActionButton m_ButtonWallpaperRepeats;
        public ActionButton m_ButtonWallpaperScale;
        public ActionButton m_ButtonWallpaperSkew;
        public Transform m_WallpaperRepeatsControls;
        public Transform m_WallpaperScaleControls;
        public Transform m_WallpaperSkewControls;

        public GameObject m_LineGroupControls;
        public TMPro.TextMeshPro m_LineFamilyLabel;
        public AdvancedSlider m_LineOrderSlider;
        public AdvancedSlider m_LineRepeatsSlider;
        public AdvancedSlider m_LineAdvanceSlider;
        public AdvancedSlider m_LineAngleSlider;

        public GameObject m_SpaceGroupControls;
        public TMPro.TextMeshPro m_SpaceGroupSystemLabel;
        public TMPro.TextMeshPro m_SpaceGroupPresetLabel;
        public AdvancedSlider m_SpaceGroupRepeatXSlider;
        public AdvancedSlider m_SpaceGroupRepeatYSlider;
        public AdvancedSlider m_SpaceGroupRepeatZSlider;
        public AdvancedSlider m_SpaceGroupCellSizeSlider;

        public GameObject m_PenroseControls;
        public TMPro.TextMeshPro m_PenroseTilesLabel;
        public AdvancedSlider m_PenroseSubdivisionsSlider;
        public AdvancedSlider m_PenroseRadiusSlider;

        public GameObject m_HelicalControls;
        public AdvancedSlider m_HelicalCopiesSlider;
        public AdvancedSlider m_HelicalAngleSlider;
        public AdvancedSlider m_HelicalAdvanceSlider;

        public GameObject m_OptionsControls;
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

        private enum MirrorTab
        {
            Simple,
            Point,
            Wallpaper,
            Helical,
            SpaceGroup,
            LineGroup,
            Penrose
        }

        private static MirrorTab m_LastActiveTab = MirrorTab.Simple;

        [NonSerialized] public PointerManager.ColorShiftComponent m_currentSelectedColorComponent;
        public enum SymmetryTransformAxis { X, Y, Z };
        public enum SymmetryTransformType { Position, Rotation };
        public SymmetryTransformAxis m_CurrentSymmetryTransformAxis;
        public SymmetryTransformType m_CurrentSymmetryTransformType;
        public AdvancedSlider m_SymmetryTransformValueSlider;

        public override void Init(GameObject rParent, string sText)
        {
            base.Init(rParent, sText);
            RefreshMirrorControls();
            RefreshCellsLabel();
            LocalizationSettings.SelectedLocaleChanged += RefreshCellsLabel;
            LocalizationSettings.SelectedLocaleChanged += RefreshModeLabels;
            SketchMemoryScript.m_Instance.CommandUndo += RefreshMirrorControlsAfterHistory;
            SketchMemoryScript.m_Instance.CommandRedo += RefreshMirrorControlsAfterHistory;
            foreach (var slider in GetComponentsInChildren<AdvancedSlider>(true))
            {
                slider.InteractionEnded += SymmetryMirrors.EndSettingsEdit;
            }
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= RefreshCellsLabel;
            LocalizationSettings.SelectedLocaleChanged -= RefreshModeLabels;
            foreach (var slider in GetComponentsInChildren<AdvancedSlider>(true))
            {
                slider.InteractionEnded -= SymmetryMirrors.EndSettingsEdit;
            }
            if (SketchMemoryScript.m_Instance == null) { return; }
            SketchMemoryScript.m_Instance.CommandUndo -= RefreshMirrorControlsAfterHistory;
            SketchMemoryScript.m_Instance.CommandRedo -= RefreshMirrorControlsAfterHistory;
        }

        private void RefreshMirrorControlsAfterHistory(BaseCommand command)
        {
            SymmetryMirrors.EndSettingsEdit();
            RefreshMirrorControls();
        }

        private SymmetryMirror m_DisplayedMirror;

        protected override void BaseUpdate()
        {
            base.BaseUpdate();
            if (m_RefreshRejectedCount)
            {
                m_RefreshRejectedCount = false;
                RefreshMirrorControls();
            }
            // Also follow recall/undo performed outside this popup.
            if (!ReferenceEquals(m_DisplayedMirror, SymmetryMirrors.Showing))
            {
                RefreshMirrorControls();
            }
        }

        public void HandleNewMirror()
        {
            SymmetryMirrors.NewMirror();
            RefreshMirrorControls();
        }

        public void HandleToggleExtraPreviewGizmos()
        {
            PointerManager.m_Instance.SymmetryWidget.HideExtraPreviewGizmos =
                m_HideExtraPreviewGizmosButton.ToggleState;
        }

        public void HandleUnlinkAll()
        {
            SymmetryMirrors.UnlinkAll();
            RefreshMirrorControls();
        }

        public void HandleNextMirror()
        {
            SymmetryMirrors.Cycle(1);
            RefreshMirrorControls();
        }

        public void HandlePreviousMirror()
        {
            SymmetryMirrors.Cycle(-1);
            RefreshMirrorControls();
        }

        private void RefreshMirrorControls()
        {
            m_DisplayedMirror = SymmetryMirrors.Showing;
            if (m_UnlinkAllButton != null) { m_UnlinkAllButton.SetButtonAvailable(m_DisplayedMirror != null); }

            var pm = PointerManager.m_Instance;
            if (m_HideExtraPreviewGizmosButton != null)
            {
                m_HideExtraPreviewGizmosButton.ToggleState = pm.SymmetryWidget.HideExtraPreviewGizmos;
            }
            var mode = pm.MirrorModeEnabled ? pm.CurrentSymmetryMode : pm.LastMirrorMode;

            if (mode == PointerManager.SymmetryMode.SinglePlane)
            {
                m_LastActiveTab = MirrorTab.Simple;
            }
            else
            {
                switch (PointerManager.m_Instance.m_CustomSymmetryType)
                {
                    case PointerManager.CustomSymmetryType.Penrose: m_LastActiveTab = MirrorTab.Penrose; break;
                    case PointerManager.CustomSymmetryType.LineGroup: m_LastActiveTab = MirrorTab.LineGroup; break;
                    case PointerManager.CustomSymmetryType.SpaceGroup: m_LastActiveTab = MirrorTab.SpaceGroup; break;
                    case PointerManager.CustomSymmetryType.Helical: m_LastActiveTab = MirrorTab.Helical; break;
                    case PointerManager.CustomSymmetryType.Wallpaper: m_LastActiveTab = MirrorTab.Wallpaper; break;
                    default: m_LastActiveTab = MirrorTab.Point; break;
                }
            }
            // Reflect restored settings without running tab-click handlers, which change
            // symmetry settings and can record commands while undo/redo is in progress.
            bool showingColorOptions = m_OptionsControls.activeSelf;
            HideAllControls();
            switch (m_LastActiveTab)
            {
                case MirrorTab.Simple:
                    m_SimpleControls?.SetActive(true);
                    m_ButtonShowSimpleControls?.SetButtonSelected(true);
                    break;
                case MirrorTab.Point:
                    m_PointSymmetryControls.SetActive(true);
                    m_ButtonShowPointControls?.SetButtonSelected(true);
                    UpdatePointSettingControls();
                    break;
                case MirrorTab.Wallpaper:
                    m_WallpaperSymmetryControls.SetActive(true);
                    m_ButtonShowWallpaperControls?.SetButtonSelected(true);
                    break;
                case MirrorTab.Helical:
                    m_HelicalControls.SetActive(true);
                    m_ButtonShowHelicalControls?.SetButtonSelected(true);
                    break;
                case MirrorTab.SpaceGroup:
                    m_SpaceGroupControls.SetActive(true);
                    m_ButtonShowSpaceGroupControls?.SetButtonSelected(true);
                    RefreshSpaceGroupControls();
                    break;
                case MirrorTab.LineGroup:
                    m_LineGroupControls.SetActive(true);
                    m_ButtonShowLineGroupControls?.SetButtonSelected(true);
                    RefreshLineGroupControls();
                    break;
                case MirrorTab.Penrose:
                    m_PenroseControls.SetActive(true);
                    m_ButtonShowPenroseControls?.SetButtonSelected(true);
                    RefreshPenroseControls();
                    break;
            }
            m_OptionsControls.SetActive(showingColorOptions);
            m_ButtonShowColorControls?.SetButtonSelected(showingColorOptions);
            if (showingColorOptions)
            {
                UpdateColorPreview();
                UpdateOptionsControlsToMatchValues();
            }
            foreach (var button in GetComponentsInChildren<MirrorTypeButton>(true))
            {
                button.SetButtonSelected(false);
            }
            if (PointerManager.m_Instance.CurrentSymmetryMode == PointerManager.SymmetryMode.MultiMirror)
            {
                SetCurrentMirrorTypeButtonState(true);
            }
            UpdateWallpaperSettingControls();

            RefreshSliderValues();
        }

        private bool TrySetCountSetting<T>(ref T setting, T value)
        {
            var pm = PointerManager.m_Instance;
            var previous = setting;
            long previousCount = pm.GetCustomSymmetryPointerCount();
            setting = value;
            if (!CheckPointerLimit(previousCount))
            {
                setting = previous;
                RefreshMirrorControls();
                return false;
            }
            pm.CalculateMirrors();
            return true;
        }

        private void RefreshCellsLabel(UnityEngine.Localization.Locale locale = null)
        {
            var label = LocalizationSettings.StringDatabase.GetLocalizedString(
                "Strings", "MULTIMIRROR_PANEL_CELLS_LABEL");
            if (m_ButtonWallpaperRepeats is TextActionButton button) { button.m_ButtonLabel = label; }
            m_ButtonWallpaperRepeats.GetComponentInChildren<TMPro.TextMeshPro>(true).text = label;
        }

        private bool CheckPointerLimit(long previousCount)
        {
            var pm = PointerManager.m_Instance;
            long count = pm.GetCustomSymmetryPointerCount();
            // Allow reductions of an already oversized configuration loaded from elsewhere,
            // so it can be brought back under the limit one control at a time.
            if (count > pm.SymmetryPointerCapacity && count >= previousCount)
            {
                if (Time.unscaledTime >= m_NextPointerLimitNotice)
                {
                    var message = LocalizationSettings.StringDatabase.GetLocalizedString(
                        "Strings", "MULTIMIRROR_PANEL_TOO_MANY_POINTERS");
                    OutputWindowScript.m_Instance.CreateInfoCardAtController(
                        InputManager.ControllerName.Brush, message, 0.5f, false);
                    m_NextPointerLimitNotice = Time.unscaledTime + 1f;
                }
                // AdvancedSlider updates its visual value after invoking its callback.
                // Restore the accepted value on the next frame as well.
                m_RefreshRejectedCount = true;
                return false;
            }
            return true;
        }

        private void RefreshSliderValues()
        {
            m_HelicalCopiesSlider.SetInitialValueAndUpdate(PointerManager.m_Instance.m_HelicalCopies);
            m_HelicalAngleSlider.SetInitialValueAndUpdate(PointerManager.m_Instance.m_HelicalAngle);
            m_HelicalAdvanceSlider.SetInitialValueAndUpdate(PointerManager.m_Instance.m_HelicalAdvance);
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
            var pm = PointerManager.m_Instance;
            long previousCount = pm.GetCustomSymmetryPointerCount();
            var previousType = pm.m_CustomSymmetryType;
            var previousPoint = pm.m_PointSymmetryFamily;
            var previousWallpaper = pm.m_WallpaperSymmetryGroup;
            SetCurrentMirrorTypeButtonState(false);
            PointerManager.m_Instance.m_CustomSymmetryType = btn.m_CustomSymmetryType;
            switch (btn.m_CustomSymmetryType)
            {
                case PointerManager.CustomSymmetryType.Point:
                    PointerManager.m_Instance.m_PointSymmetryFamily = btn.m_PointSymmetryFamily;
                    break;
                case PointerManager.CustomSymmetryType.Wallpaper:
                    PointerManager.m_Instance.m_WallpaperSymmetryGroup = btn.m_WallpaperSymmetryGroup;
                    break;
            }
            if (!CheckPointerLimit(previousCount))
            {
                pm.m_CustomSymmetryType = previousType;
                pm.m_PointSymmetryFamily = previousPoint;
                pm.m_WallpaperSymmetryGroup = previousWallpaper;
                RefreshMirrorControls();
                return;
            }
            UpdatePointSettingControls();
            UpdateWallpaperSettingControls();
            SetCurrentMirrorTypeButtonState(true);
            PointerManager.m_Instance.SymmetryWidget.SetAppearance();
            PointerManager.m_Instance.CalculateMirrors();
        }

        private void UpdatePointSettingControls()
        {
            var family = PointerManager.m_Instance.m_PointSymmetryFamily;
            bool usesOrder = family == PointSymmetry.Family.Cn ||
                family == PointSymmetry.Family.Cnv || family == PointSymmetry.Family.Cnh ||
                family == PointSymmetry.Family.Sn || family == PointSymmetry.Family.Dn ||
                family == PointSymmetry.Family.Dnh || family == PointSymmetry.Family.Dnd;
            m_PointSymmetryOrderSlider.gameObject.SetActive(usesOrder);
        }

        private void UpdateWallpaperSettingControls()
        {
            var group = PointerManager.m_Instance.m_WallpaperSymmetryGroup;
            bool supportsSkew = group == SymmetryGroup.R.p1 || group == SymmetryGroup.R.p2;
            bool supportsHeight = supportsSkew || group == SymmetryGroup.R.pg ||
                group == SymmetryGroup.R.pm || group == SymmetryGroup.R.cm ||
                group == SymmetryGroup.R.pmm || group == SymmetryGroup.R.pmg ||
                group == SymmetryGroup.R.pgg || group == SymmetryGroup.R.cmm;

            m_ButtonWallpaperRepeats.SetButtonAvailable(true);
            m_ButtonWallpaperScale.SetButtonAvailable(true);
            m_ButtonWallpaperSkew.SetButtonAvailable(supportsSkew);
            m_ButtonWallpaperSkew.gameObject.SetActive(supportsSkew);
            m_WallpaperScaleSliderY.gameObject.SetActive(supportsHeight);

            if (!supportsSkew && m_WallpaperSkewControls.gameObject.activeSelf)
            {
                HandleWallpaperControlsRepeatsButton();
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
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_PointSymmetryOrder, Mathf.Max(1, Mathf.FloorToInt(value.z)));
        }

        public void HandleChangeWallpaperSymmetryX(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_WallpaperSymmetryX, Mathf.Max(1, Mathf.FloorToInt(value.z)));
        }

        public void HandleChangeWallpaperSymmetryY(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_WallpaperSymmetryY, Mathf.Max(1, Mathf.FloorToInt(value.z)));
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

        private bool SetCustomSymmetryType(PointerManager.CustomSymmetryType type)
        {
            var pm = PointerManager.m_Instance;
            long previousCount = pm.GetCustomSymmetryPointerCount();
            var previousType = pm.m_CustomSymmetryType;
            pm.m_CustomSymmetryType = type;
            if (!CheckPointerLimit(previousCount))
            {
                pm.m_CustomSymmetryType = previousType;
                RefreshMirrorControls();
                return false;
            }
            pm.SetSymmetryMode(PointerManager.SymmetryMode.MultiMirror);
            return true;
        }

        private void UpdateSymmetry()
        {
            PointerManager.m_Instance.CalculateMirrors();
            PointerManager.m_Instance.SymmetryWidget.SetAppearance();
        }

        private void HideAllControls()
        {
            m_SimpleControls?.SetActive(false);
            m_PointSymmetryControls.SetActive(false);
            m_WallpaperSymmetryControls.SetActive(false);
            m_HelicalControls.SetActive(false);
            m_SpaceGroupControls.SetActive(false);
            m_LineGroupControls.SetActive(false);
            m_PenroseControls.SetActive(false);
            m_OptionsControls.SetActive(false);

            m_ButtonShowSimpleControls?.SetButtonSelected(false);
            m_ButtonShowPointControls?.SetButtonSelected(false);
            m_ButtonShowWallpaperControls?.SetButtonSelected(false);
            m_ButtonShowHelicalControls?.SetButtonSelected(false);
            m_ButtonShowSpaceGroupControls?.SetButtonSelected(false);
            m_ButtonShowLineGroupControls?.SetButtonSelected(false);
            m_ButtonShowPenroseControls?.SetButtonSelected(false);
            m_ButtonShowColorControls?.SetButtonSelected(false);
        }

        public void HandleShowSimpleSymmetry()
        {
            HideAllControls();
            m_LastActiveTab = MirrorTab.Simple;
            m_SimpleControls?.SetActive(true);
            PointerManager.m_Instance.SetSymmetryMode(PointerManager.SymmetryMode.SinglePlane);
            m_ButtonShowSimpleControls?.SetButtonSelected(true);
            PointerManager.m_Instance.SymmetryWidget.SetAppearance();
        }

        public void HandleShowPenroseSymmetry()
        {
            HideAllControls();
            m_LastActiveTab = MirrorTab.Penrose;
            m_PenroseControls.SetActive(true);
            if (!SetCustomSymmetryType(PointerManager.CustomSymmetryType.Penrose)) { return; }
            m_ButtonShowPenroseControls?.SetButtonSelected(true);
            UpdateSymmetry();
            RefreshPenroseControls();
        }

        private void RefreshPenroseControls()
        {
            var pm = PointerManager.m_Instance;
            m_PenroseTilesLabel.text = $"Tiles: {pm.m_PenroseTiles}";
            m_PenroseSubdivisionsSlider.SetInitialValueAndUpdate(pm.m_PenroseSubdivisions);
            m_PenroseRadiusSlider.SetInitialValueAndUpdate(pm.m_PenroseRadius);
        }

        public void HandleChangePenroseTiles(int direction)
        {
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_PenroseTiles, (PenroseTiling.TileSelection)(((int)pm.m_PenroseTiles + direction + 3) % 3));
            RefreshPenroseControls();
        }

        public void HandleChangePenroseSubdivisions(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_PenroseSubdivisions, Mathf.Clamp(Mathf.RoundToInt(value.z), 1, 7));
            RefreshPenroseControls();
        }

        public void HandleChangePenroseRadius(Vector3 value)
        {
            PointerManager.m_Instance.m_PenroseRadius = Mathf.Max(0.01f, value.z);
            PointerManager.m_Instance.CalculateMirrors();
            RefreshPenroseControls();
        }

        public void HandleShowLineGroupSymmetry()
        {
            HideAllControls();
            m_LastActiveTab = MirrorTab.LineGroup;
            m_LineGroupControls.SetActive(true);
            if (!SetCustomSymmetryType(PointerManager.CustomSymmetryType.LineGroup)) { return; }
            m_ButtonShowLineGroupControls?.SetButtonSelected(true);
            UpdateSymmetry();
            RefreshLineGroupControls();
        }

        private void RefreshModeLabels(UnityEngine.Localization.Locale locale = null)
        {
            var pm = PointerManager.m_Instance;
            if (m_LineGroupControls.activeSelf)
            {
                m_LineFamilyLabel.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                    "Strings", $"MULTIMIRROR_PANEL_LINE_FAMILY_{(int)pm.m_LineFamily}");
            }
            if (m_SpaceGroupControls.activeSelf)
            {
                var system = SpaceGroupCatalog.GetCrystalSystem(pm.m_SpaceGroupNumber);
                m_SpaceGroupSystemLabel.text = system.ToString();
                m_SpaceGroupPresetLabel.text = SpaceGroupCatalog.GetPresets(system)
                    .Any(preset => preset.number == pm.m_SpaceGroupNumber)
                    ? LocalizationSettings.StringDatabase.GetLocalizedString(
                        "Strings", $"MULTIMIRROR_PANEL_SPACE_PRESET_{pm.m_SpaceGroupNumber}")
                    : SpaceGroupCatalog.GetName(pm.m_SpaceGroupNumber);
            }
        }

        private void RefreshLineGroupControls()
        {
            var pm = PointerManager.m_Instance;
            RefreshModeLabels();
            m_LineOrderSlider.SetInitialValueAndUpdate(pm.m_LineOrder);
            m_LineRepeatsSlider.SetInitialValueAndUpdate(pm.m_LineRepeats);
            m_LineAdvanceSlider.SetInitialValueAndUpdate(pm.m_LineAdvance);
            m_LineAngleSlider.SetInitialValueAndUpdate(pm.m_LineAngle);
            var freeAngle = LineGroupSymmetry.UsesFreeAngle(pm.m_LineFamily);
            m_LineAngleSlider.gameObject.SetActive(freeAngle);
        }

        public void HandleChangeLineFamily(int direction)
        {
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_LineFamily, (LineGroupSymmetry.Family)(((int)pm.m_LineFamily - 1 + direction + 13) % 13 + 1));
            RefreshLineGroupControls();
        }

        public void HandleChangeLineOrder(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_LineOrder, Mathf.Max(1, Mathf.RoundToInt(value.z)));
            RefreshLineGroupControls();
        }

        public void HandleChangeLineRepeats(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_LineRepeats, Mathf.Max(1, Mathf.RoundToInt(value.z)));
            RefreshLineGroupControls();
        }

        public void HandleChangeLineAdvance(Vector3 value)
        {
            PointerManager.m_Instance.m_LineAdvance = Mathf.Max(0f, value.z);
            PointerManager.m_Instance.CalculateMirrors();
            RefreshLineGroupControls();
        }

        public void HandleChangeLineAngle(Vector3 value)
        {
            PointerManager.m_Instance.m_LineAngle = value.z;
            PointerManager.m_Instance.CalculateMirrors();
            RefreshLineGroupControls();
        }

        public void HandleShowSpaceGroupSymmetry()
        {
            HideAllControls();
            m_LastActiveTab = MirrorTab.SpaceGroup;
            m_SpaceGroupControls.SetActive(true);
            if (!SetCustomSymmetryType(PointerManager.CustomSymmetryType.SpaceGroup)) { return; }
            m_ButtonShowSpaceGroupControls?.SetButtonSelected(true);
            UpdateSymmetry();
            RefreshSpaceGroupControls();
        }

        private void RefreshSpaceGroupControls()
        {
            var pm = PointerManager.m_Instance;
            RefreshModeLabels();
            m_SpaceGroupRepeatXSlider.SetInitialValueAndUpdate(pm.m_SpaceGroupRepeats.x);
            m_SpaceGroupRepeatYSlider.SetInitialValueAndUpdate(pm.m_SpaceGroupRepeats.y);
            m_SpaceGroupRepeatZSlider.SetInitialValueAndUpdate(pm.m_SpaceGroupRepeats.z);
            m_SpaceGroupCellSizeSlider.SetInitialValueAndUpdate(pm.m_SpaceGroupCellSize);
        }

        public void HandleChangeSpaceGroupSystem(int direction)
        {
            var pm = PointerManager.m_Instance;
            var current = SpaceGroupCatalog.GetCrystalSystem(pm.m_SpaceGroupNumber);
            var next = (SpaceGroupCatalog.CrystalSystem)(((int)current + direction + 7) % 7);
            TrySetCountSetting(ref pm.m_SpaceGroupNumber, SpaceGroupCatalog.GetPresets(next)[0].number);
            RefreshSpaceGroupControls();
        }

        public void HandleChangeSpaceGroupPreset(int direction)
        {
            var pm = PointerManager.m_Instance;
            var presets = SpaceGroupCatalog.GetPresets(SpaceGroupCatalog.GetCrystalSystem(pm.m_SpaceGroupNumber));
            var next = 0;
            for (var i = 0; i < presets.Count; i++)
            {
                if (presets[i].number == pm.m_SpaceGroupNumber) { next = (i + direction + presets.Count) % presets.Count; break; }
            }
            TrySetCountSetting(ref pm.m_SpaceGroupNumber, presets[next].number);
            RefreshSpaceGroupControls();
        }

        public void HandleChangeSpaceGroupNumber(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_SpaceGroupNumber, Mathf.Clamp(Mathf.RoundToInt(value.z), 1, 230));
            RefreshSpaceGroupControls();
        }

        public void HandleChangeSpaceGroupRepeatX(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            var repeats = pm.m_SpaceGroupRepeats;
            repeats.x = Mathf.Max(1, Mathf.RoundToInt(value.z));
            TrySetCountSetting(ref pm.m_SpaceGroupRepeats, repeats);
            RefreshSpaceGroupControls();
        }

        public void HandleChangeSpaceGroupRepeatY(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            var repeats = pm.m_SpaceGroupRepeats;
            repeats.y = Mathf.Max(1, Mathf.RoundToInt(value.z));
            TrySetCountSetting(ref pm.m_SpaceGroupRepeats, repeats);
            RefreshSpaceGroupControls();
        }

        public void HandleChangeSpaceGroupRepeatZ(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            var repeats = pm.m_SpaceGroupRepeats;
            repeats.z = Mathf.Max(1, Mathf.RoundToInt(value.z));
            TrySetCountSetting(ref pm.m_SpaceGroupRepeats, repeats);
            RefreshSpaceGroupControls();
        }

        public void HandleChangeSpaceGroupCellSize(Vector3 value)
        {
            PointerManager.m_Instance.m_SpaceGroupCellSize = Mathf.Max(0.01f, value.z);
            PointerManager.m_Instance.CalculateMirrors();
            RefreshSpaceGroupControls();
        }

        public void HandleShowHelicalSymmetry()
        {
            HideAllControls();
            m_LastActiveTab = MirrorTab.Helical;
            m_HelicalControls.SetActive(true);
            if (!SetCustomSymmetryType(PointerManager.CustomSymmetryType.Helical)) { return; }
            m_ButtonShowHelicalControls?.SetButtonSelected(true);
            UpdateSymmetry();
        }

        public void HandleChangeHelicalCopies(Vector3 value)
        {
            var pm = PointerManager.m_Instance;
            TrySetCountSetting(ref pm.m_HelicalCopies, Mathf.Max(1, Mathf.FloorToInt(value.z)));
        }

        public void HandleChangeHelicalAngle(Vector3 value)
        {
            PointerManager.m_Instance.m_HelicalAngle = value.z;
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleChangeHelicalAdvance(Vector3 value)
        {
            PointerManager.m_Instance.m_HelicalAdvance = value.z;
            PointerManager.m_Instance.CalculateMirrors();
        }

        public void HandleShowPointSymmetry()
        {
            HideAllControls();
            m_LastActiveTab = MirrorTab.Point;
            m_PointSymmetryControls.SetActive(true);
            if (!SetCustomSymmetryType(PointerManager.CustomSymmetryType.Point)) { return; }
            m_ButtonShowPointControls?.SetButtonSelected(true);
            UpdatePointSettingControls();
            UpdateSymmetry();
        }

        public void HandleShowWallpaperSymmetry()
        {
            HideAllControls();
            m_LastActiveTab = MirrorTab.Wallpaper;
            m_WallpaperSymmetryControls.SetActive(true);
            if (!SetCustomSymmetryType(PointerManager.CustomSymmetryType.Wallpaper)) { return; }
            m_ButtonShowWallpaperControls?.SetButtonSelected(true);
            UpdateWallpaperSettingControls();
            UpdateSymmetry();
        }

        public void HandleShowColorOptions()
        {
            bool show = !m_OptionsControls.activeSelf;
            m_OptionsControls.SetActive(show);
            m_ButtonShowColorControls?.SetButtonSelected(show);
            if (show)
            {
                PointerManager.m_Instance.CalculateMirrors();
                UpdateColorPreview();
                UpdateOptionsControlsToMatchValues();
            }
        }

        private void UpdateOptionsControlsToMatchValues()
        {
            m_OptionsButtonHue.SetButtonSelected(m_currentSelectedColorComponent == PointerManager.ColorShiftComponent.Hue);
            m_OptionsButtonSaturation.SetButtonSelected(m_currentSelectedColorComponent == PointerManager.ColorShiftComponent.Saturation);
            m_OptionsButtonBrightness.SetButtonSelected(m_currentSelectedColorComponent == PointerManager.ColorShiftComponent.Brightness);
            var currentSettings = m_currentSelectedColorComponent switch
            {
                PointerManager.ColorShiftComponent.Hue => PointerManager.m_Instance.m_SymmetryColorShiftSettingHue,
                PointerManager.ColorShiftComponent.Saturation => PointerManager.m_Instance.m_SymmetryColorShiftSettingSaturation,
                PointerManager.ColorShiftComponent.Brightness => PointerManager.m_Instance.m_SymmetryColorShiftSettingBrightness,
                _ => throw new ArgumentOutOfRangeException()
            };
            m_OptionsSliderAmp.SetInitialValueAndUpdate(currentSettings.amp);
            m_OptionsSliderFreq.SetInitialValueAndUpdate(currentSettings.freq);
            m_OptionsButtonSine.SetButtonSelected(currentSettings.mode == WaveGenerator.Mode.SineWave);
            m_OptionsButtonSquare.SetButtonSelected(currentSettings.mode == WaveGenerator.Mode.SquareWave);
            m_OptionsButtonTriangle.SetButtonSelected(currentSettings.mode == WaveGenerator.Mode.TriangleWave);
            m_OptionsButtonSawtooth.SetButtonSelected(currentSettings.mode == WaveGenerator.Mode.SawtoothWave);
            m_OptionsButtonNoise.SetButtonSelected(currentSettings.mode == WaveGenerator.Mode.WhiteNoise);
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
                    UpdateActiveColorShiftMode(WaveGenerator.Mode.SineWave);
                    break;
                case "triangle":
                    UpdateActiveColorShiftMode(WaveGenerator.Mode.TriangleWave);
                    break;
                case "sawtooth":
                    UpdateActiveColorShiftMode(WaveGenerator.Mode.SawtoothWave);
                    break;
                case "square":
                    UpdateActiveColorShiftMode(WaveGenerator.Mode.SquareWave);
                    break;
                case "noise":
                    UpdateActiveColorShiftMode(WaveGenerator.Mode.WhiteNoise);
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

        private void UpdateActiveColorShiftMode(WaveGenerator.Mode mode)
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
