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

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("mirror.type", "")]
        public static void CustomSymmetryType(string type)
        {
            Enum.TryParse(type, ignoreCase: true, out PointerManager.CustomSymmetryType _type);
            PointerManager.m_Instance.m_CustomSymmetryType = _type;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        [ApiEndpoint("mirror.pointfamily", "")]
        public static void PointSymmetryFamily(string family)
        {
            Enum.TryParse(family, ignoreCase: true, out PointSymmetry.Family _family);
            PointerManager.m_Instance.m_PointSymmetryFamily = _family;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        [ApiEndpoint("mirror.wallpapergroup", "")]
        public static void WallpaperSymmetryGroup(string group)
        {
            Enum.TryParse(group, ignoreCase: true, out SymmetryGroup.R _group);
            PointerManager.m_Instance.m_WallpaperSymmetryGroup = _group;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        [ApiEndpoint("mirror.pointorder", "")]
        public static void PointSymmetryOrder(int order)
        {
            PointerManager.m_Instance.m_PointSymmetryOrder = order;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        [ApiEndpoint("mirror.wallpaperrepeats", "")]
        public static void WallpaperSymmetryX(int x, int y)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryX = x;
            PointerManager.m_Instance.m_WallpaperSymmetryY = y;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        [ApiEndpoint("mirror.wallpaperscale", "")]
        public static void WallpaperSymmetryScaleX(float x, float y)
        {
            PointerManager.m_Instance.m_WallpaperSymmetryScaleX = x;
            PointerManager.m_Instance.m_WallpaperSymmetryScaleY = y;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        [ApiEndpoint("mirror.wallpaperskew", "")]
        public static void WallpaperSymmetrySkewX(float x, float y)
        {
            PointerManager.m_Instance.m_WallpaperSymmetrySkewX = x;
            PointerManager.m_Instance.m_WallpaperSymmetrySkewY = y;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }

        [ApiEndpoint("mirror.symmetryjitter", "")]
        public static void SymmetryRespectsJitter(bool jitter)
        {
            PointerManager.m_Instance.m_SymmetryRespectsJitter = jitter;
            PointerManager.m_Instance.CalculateMirrorMatrices();
        }
    }
}
