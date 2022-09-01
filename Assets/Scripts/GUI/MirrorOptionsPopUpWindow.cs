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

using UnityEngine;

namespace TiltBrush
{
    public class MirrorOptionsPopUpWindow : OptionsPopUpWindow
    {

        public GameObject m_PointSymmetryControls;
        public GameObject m_WallpaperSymmetryControls;

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
        }

        public void HandleChangePointSymmetryOrder(Vector3 value)
        {
            PointerManager.m_Instance.m_PointSymmetryOrder = Mathf.FloorToInt(value.z);
        }
        
        public void HandleChangeWallpaperSymmetryX(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryX = Mathf.FloorToInt(value.z);
        }
        
        public void HandleChangeWallpaperSymmetryY(Vector3 value)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryY = Mathf.FloorToInt(value.z);
        }

        public void HandleShowPointSymmetry()
        {
            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Point;
            m_PointSymmetryControls.SetActive(true);
            m_WallpaperSymmetryControls.SetActive(false);
        }

        public void HandleShowWallpaperSymmetry()
        {
            PointerManager.m_Instance.m_CustomSymmetryType = PointerManager.CustomSymmetryType.Wallpaper;
            m_PointSymmetryControls.SetActive(false);
            m_WallpaperSymmetryControls.SetActive(true);
        }

    }
}
