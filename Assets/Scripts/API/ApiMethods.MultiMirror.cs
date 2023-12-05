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
using System.Text.RegularExpressions;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("symmetry.type",
        description: "Sets the custom symmetry type (Currently either 'point' or 'wallpaper'",
        exampleUsage: "wallpaper")]
        public static void CustomSymmetryType(string type)
        {
            Enum.TryParse(type, ignoreCase: true, out PointerManager.CustomSymmetryType _type);
            PointerManager.m_Instance.m_CustomSymmetryType = _type;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.pointfamily",
        description: "Sets the custom point symmetry family (Any of Cn, Cnv, Cnh, Sn, Dn, Dnh, Dnd, T, Th, Td, O, Oh, I, Ih) Replace n with a number to also set the order.",
        exampleUsage: "C4v")]
        public static void PointSymmetryFamily(string family)
        {
            var digit = new Regex("\\d");
            var captures = digit.Match(family).Captures;
            if (captures.Count == 1)
            {
                family = digit.Replace(family, "n");
                var order = captures[0].ToString();
                PointerManager.m_Instance.m_PointSymmetryOrder = Int32.Parse(order);
            }
            Enum.TryParse(family, ignoreCase: true, out PointSymmetry.Family _family);
            PointerManager.m_Instance.m_PointSymmetryFamily = _family;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.wallpapergroup",
        description: "Sets the custom wallpaper symmetry group (Any of p1, pg, cm, pm, p6, p6m, p3, p3m1, p31m, p4, p4m, p4g, p2, pgg, pmg, pmm, cmm)",
        exampleUsage: "p6m")]
        public static void WallpaperSymmetryGroup(string group)
        {
            Enum.TryParse(group, ignoreCase: true, out SymmetryGroup.R _group);
            PointerManager.m_Instance.m_WallpaperSymmetryGroup = _group;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.pointorder",
        description: "Sets the custom point symmetry order",
        exampleUsage: "5")]
        public static void PointSymmetryOrder(int order)
        {
            PointerManager.m_Instance.m_PointSymmetryOrder = order;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.wallpaperrepeats",
        description: "Sets the custom wallpaper symmetry repeats",
        exampleUsage: "4,4")]
        public static void WallpaperSymmetryX(int x, int y)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryX = x;
            PointerManager.m_Instance.m_WallpaperSymmetryY = y;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.wallpaperscale",
        description: "Sets the custom wallpaper symmetry scale",
        exampleUsage: "0.5,1")]
        public static void WallpaperSymmetryScaleX(float x, float y)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScaleX = x;
            PointerManager.m_Instance.m_WallpaperSymmetryScaleY = y;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.wallpaperskew",
        description: "Sets the custom wallpaper symmetry skew",
        exampleUsage: "1,0.5")]
        public static void WallpaperSymmetrySkewX(float x, float y)
        {
            PointerManager.m_Instance.m_WallpaperSymmetrySkewX = x;
            PointerManager.m_Instance.m_WallpaperSymmetrySkewY = y;
            PointerManager.m_Instance.CalculateMirrors();
        }

        [ApiEndpoint("symmetry.colorshift.hue",
        description: "Sets the custom wallpaper color shift hue (mode is one of SineWave, SquareWave, SawtoothWave, TriangleWave, Noise)",
        exampleUsage: "Noise,1,2")]
        public static void SymmetryColorShiftHue(string mode, float amplitude, float frequency)
        {
            PointerManager.m_Instance.m_SymmetryColorShiftSettingHue = _SymmetryColorShift(mode, amplitude, frequency);
        }

        [ApiEndpoint("symmetry.colorshift.saturation",
        description: "Sets the custom wallpaper color shift saturation (mode is one of SineWave, SquareWave, SawtoothWave, TriangleWave, Noise)",
        exampleUsage: "SineWave,0.1,1")]
        public static void SymmetryColorShiftSaturation(string mode, float amplitude, float frequency)
        {
            PointerManager.m_Instance.m_SymmetryColorShiftSettingSaturation = _SymmetryColorShift(mode, amplitude, frequency);
        }

        [ApiEndpoint("symmetry.colorshift.brightness",
        description: "Sets the custom wallpaper color shift brightness (mode is one of SineWave, SquareWave, SawtoothWave, TriangleWave, Noise)",
        exampleUsage: "SquareWave,0.5,6")]
        public static void SymmetryColorShiftBrightness(string mode, float amplitude, float frequency)
        {
            PointerManager.m_Instance.m_SymmetryColorShiftSettingBrightness = _SymmetryColorShift(mode, amplitude, frequency);
        }

        private static PointerManager.ColorShiftComponentSetting _SymmetryColorShift(string mode, float amplitude, float frequency)
        {
            Enum.TryParse(mode, ignoreCase: true, out PointerManager.ColorShiftMode _mode);
            return new PointerManager.ColorShiftComponentSetting
            {
                mode = _mode,
                amp = amplitude,
                freq = frequency
            };
        }
    }
}
