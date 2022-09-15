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

namespace TiltBrush
{
    public class MirrorOptionsPopUpWindow : OptionsPopUpWindow
    {

        public GameObject m_PointSymmetryControls;
        public GameObject m_WallpaperSymmetryControls;
        public AdvancedSlider m_PointSymmetryOrderSlider;
        public AdvancedSlider m_WallpaperScaleSlider;
        public AdvancedSlider m_WallpaperRepeatXSlider;
        public AdvancedSlider m_WallpaperRepeatYSlider;
        public ActionToggleButton m_ToggleJitter;
        
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
            m_ToggleJitter.m_InitialToggleState = PointerManager.m_Instance.m_SymmetryRespectsJitter;
        }

        public OptionButton GetParentButton()
        {
            return m_ParentPanel.GetComponentsInChildren<LongPressButton>().First(
                b => b.m_Command == SketchControlsScript.GlobalCommands.SymmetryFour
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
                    SketchControlsScript.m_Instance.IssueGlobalCommand(SketchControlsScript.GlobalCommands.SymmetryFour);
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
                    break;
            }
            // Regenerate
            PointerManager.m_Instance.CalculateMirrorMatrices();
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
            // Regenerate
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }
        
        public void HandleChangeWallpaperSymmetryY(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryY = Mathf.FloorToInt(value.z);
            // Regenerate
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }
        
        public void HandleChangeWallpaperSymmetryScale(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScale = value.z;
            // Regenerate
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleShowPointSymmetry()
        {
            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Point;
            m_PointSymmetryControls.SetActive(true);
            m_WallpaperSymmetryControls.SetActive(false);
            // Regenerate
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleShowWallpaperSymmetry()
        {
            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Wallpaper;
            m_PointSymmetryControls.SetActive(false);
            m_WallpaperSymmetryControls.SetActive(true);
            // Regenerate
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        public void HandleToggleJitter(ActionToggleButton btn)
        {
            PointerManager.m_Instance.m_SymmetryRespectsJitter = btn.ToggleState;
        }
    }
}
