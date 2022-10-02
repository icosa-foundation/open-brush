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

using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace TiltBrush
{
    public class MirrorOptionsPopUpWindow : OptionsPopUpWindow
    {

        public GameObject m_PointSymmetryControls;
        public GameObject m_WallpaperSymmetryControls;
        public AdvancedSlider m_PointSymmetryOrderSlider;
        public AdvancedSlider m_WallpaperRepeatXSlider;
        public AdvancedSlider m_WallpaperRepeatYSlider;
        public AdvancedSlider m_WallpaperScaleSlider;
        public AdvancedSlider m_WallpaperScaleSliderX;
        public AdvancedSlider m_WallpaperScaleSliderY;
        public AdvancedSlider m_WallpaperSkewSliderX;
        public AdvancedSlider m_WallpaperSkewSliderY;
        public ActionToggleButton m_ToggleJitter;

        public ActionButton m_ButtonWallpaperRepeats;
        public ActionButton m_ButtonWallpaperScale;
        public ActionButton m_ButtonWallpaperSkew;

        public Transform m_WallpaperRepeatsControls;
        public Transform m_WallpaperScaleControls;
        public Transform m_WallpaperSkewControls;

        private bool m_MirrorState;
        
        private void Awake()
        {
            if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Point)
            {
                HandleShowPointSymmetry();
            }
            else if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Wallpaper)
            {
                HandleShowWallpaperSymmetry();
            }
            
            m_PointSymmetryOrderSlider.m_InitialValue = PointerManager.m_Instance.m_PointSymmetryOrder;
            m_WallpaperScaleSlider.m_InitialValue = PointerManager.m_Instance.m_WallpaperSymmetryScale;
            m_WallpaperRepeatXSlider.m_InitialValue = PointerManager.m_Instance.m_WallpaperSymmetryX;
            m_WallpaperRepeatYSlider.m_InitialValue = PointerManager.m_Instance.m_WallpaperSymmetryY;
            m_WallpaperScaleSliderX.m_InitialValue = PointerManager.m_Instance.m_WallpaperSymmetryScaleX;
            m_WallpaperScaleSliderY.m_InitialValue = PointerManager.m_Instance.m_WallpaperSymmetryScaleY;
            m_WallpaperSkewSliderX.m_InitialValue = PointerManager.m_Instance.m_WallpaperSymmetrySkewX;
            m_WallpaperSkewSliderY.m_InitialValue = PointerManager.m_Instance.m_WallpaperSymmetrySkewY;

            m_ToggleJitter.m_InitialToggleState = PointerManager.m_Instance.m_SymmetryRespectsJitter;
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

        public override void Init(GameObject rParent, string sText)
        {
            base.Init(rParent, sText);
            // Store mirror state as the long press button misbehaves sometimes
            m_MirrorState = GetParentButton().IsButtonActive();
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

        public void HandleChangeMirrorTypeButton(MirrorTypeButton btn)
        {
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
            // Regenerate
            PointerManager.m_Instance.CalculateMirrorMatrices();
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

        public void HandleChangePointSymmetryOrder(Vector3 value)
        {
            PointerManager.m_Instance.m_PointSymmetryOrder = Mathf.FloorToInt(value.z);
            // Regenerate
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }
        
        public void HandleChangeWallpaperSymmetryX(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryX = Mathf.FloorToInt(value.z);
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }
        
        public void HandleChangeWallpaperSymmetryY(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryY = Mathf.FloorToInt(value.z);
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }
        
        public void HandleChangeWallpaperSymmetryScale(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScale = value.z;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleChangeWallpaperSymmetryScaleX(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScaleX = value.z;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleChangeWallpaperSymmetryScaleY(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScaleY = value.z;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleChangeWallpaperSymmetrySkewX(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetrySkewX = value.z;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleChangeWallpaperSymmetrySkewY(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetrySkewY = value.z;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleShowPointSymmetry()
        {
            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Point;
            m_PointSymmetryControls.SetActive(true);
            m_WallpaperSymmetryControls.SetActive(false);
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleShowWallpaperSymmetry()
        {
            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Wallpaper;
            m_PointSymmetryControls.SetActive(false);
            m_WallpaperSymmetryControls.SetActive(true);
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleToggleJitter(ActionToggleButton btn)
        {
            PointerManager.m_Instance.m_SymmetryRespectsJitter = btn.ToggleState;
        }
    }
}
