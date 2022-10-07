// Copyright 2022 The Open Brush Authors
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
    public static partial class ApiMethods
    {
        [ApiEndpoint("symmetry.type", "")]
        public static void CustomSymmetryType(string type)
        {
            Enum.TryParse(type, ignoreCase: true, out PointerManager.CustomSymmetryType _type);
            PointerManager.m_Instance.m_CustomSymmetryType = _type;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.pointfamily", "")]
        public static void PointSymmetryFamily(string family)
        {
            Enum.TryParse(family, ignoreCase: true, out PointSymmetry.Family _family);
            PointerManager.m_Instance.m_PointSymmetryFamily = _family;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.wallpapergroup", "")]
        public static void WallpaperSymmetryGroup(string group)
        {
            Enum.TryParse(group, ignoreCase: true, out SymmetryGroup.R _group);
            PointerManager.m_Instance.m_WallpaperSymmetryGroup = _group;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.pointorder", "")]
        public static void PointSymmetryOrder(int order)
        {
            PointerManager.m_Instance.m_PointSymmetryOrder = order;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.wallpaperrepeats", "")]
        public static void WallpaperSymmetryX(int x, int y)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryX = x;
            PointerManager.m_Instance.m_WallpaperSymmetryY = y;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.wallpaperscale", "")]
        public static void WallpaperSymmetryScaleX(float x, float y)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScaleX = x;
            PointerManager.m_Instance.m_WallpaperSymmetryScaleY = y;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.wallpaperskew", "")]
        public static void WallpaperSymmetrySkewX(float x, float y)
        {
            PointerManager.m_Instance.m_WallpaperSymmetrySkewX = x;
            PointerManager.m_Instance.m_WallpaperSymmetrySkewY = y;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.colorshift.enabled", "")]
        public static void SymmetryColorShiftEnabled(bool enabled)
        {
            PointerManager.m_Instance.m_SymmetryColorShiftEnabled = enabled;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.colorshift.hue", "")]
        public static void SymmetryColorShiftHue(string mode, float amplitude, float frequency)
        {
            PointerManager.m_Instance.m_SymmetryColorShiftSettingHue = _SymmetryColorShift(mode, amplitude, frequency);
        }

        [ApiEndpoint("symmetry.colorshift.saturation", "")]
        public static void SymmetryColorShiftSaturation(string mode, float amplitude, float frequency)
        {
            PointerManager.m_Instance.m_SymmetryColorShiftSettingSaturation = _SymmetryColorShift(mode, amplitude, frequency);
        }

        [ApiEndpoint("symmetry.colorshift.brightness", "")]
        public static void SymmetryColorShiftBrightness(string mode, float amplitude, float frequency)
        {
            PointerManager.m_Instance.m_SymmetryColorShiftSettingBrightness = _SymmetryColorShift(mode, amplitude, frequency);
        }

        private static PointerManager.ColorShiftComponentSetting _SymmetryColorShift(string mode, float amplitude, float frequency)
        {
            Enum.TryParse(mode, ignoreCase: true, out PointerManager.ColorShiftMode _mode);
            return new PointerManager.ColorShiftComponentSetting{
                mode = _mode,
                amp = amplitude,
                freq = frequency
            };
        }
    }
}
