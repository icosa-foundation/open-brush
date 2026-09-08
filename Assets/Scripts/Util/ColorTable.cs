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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TiltBrush
{
    public class ColorTable : MonoBehaviour
    {
        [SerializeField] private float m_SecretDistance = 1.0f;

        // From http://samples.msdn.microsoft.com/workshop/samples/author/dhtml/colors/ColorTable.htm
        // Some names changed to paint-based description for clarity, string length, and to remove
        // a surprising amount of insensitive names.
        private Dictionary<Color32, string> m_Colors = new Dictionary<Color32, string>
        {
            { new Color32(240, 248, 255, 255), "COLOR_COOL_WHITE" },
            { new Color32(250, 235, 215, 255), "COLOR_ANTIQUE_WHITE" },
            { new Color32(127, 255, 212, 255), "COLOR_LIGHT_CYAN" },
            { new Color32(240, 255, 255, 255), "COLOR_WHITE" },
            { new Color32(245, 245, 220, 255), "COLOR_BEIGE" },
            { new Color32(255, 228, 196, 255), "COLOR_BISQUE" },
            { new Color32(0,   0,   0,   255), "COLOR_BLACK" },
            { new Color32(141, 141, 141, 255), "COLOR_MIDDLE_GREY" },
            { new Color32(255, 235, 205, 255), "COLOR_BONE_WHITE" },
            { new Color32(0,   0,   255, 255), "COLOR_BLUE" },
            { new Color32(138, 43,  226, 255), "COLOR_BLUE_VIOLET" },
            { new Color32(165, 42,  42,  255), "COLOR_BROWN" },
            { new Color32(222, 184, 135, 255), "COLOR_DEEP_BEIGE" },
            { new Color32(95,  158, 160, 255), "COLOR_ASH_BLUE" },
            { new Color32(127, 255, 0,   255), "COLOR_CHARTREUSE" },
            { new Color32(210, 105, 30,  255), "COLOR_RAW_SIENNA" },
            { new Color32(100, 149, 237, 255), "COLOR_CORNFLOWER_BLUE" },
            { new Color32(255, 248, 220, 255), "COLOR_CORNSILK" },
            { new Color32(220, 20,  60,  255), "COLOR_CRIMSON" },
            { new Color32(0,   255, 255, 255), "COLOR_CYAN" },
            { new Color32(0,   0,   139, 255), "COLOR_DARK_BLUE" },
            { new Color32(0,   139, 139, 255), "COLOR_DARK_TEAL" },
            { new Color32(184, 134, 11,  255), "COLOR_DARK_OCHRE" },
            { new Color32(0,   100, 0,   255), "COLOR_DARK_GREEN" },
            { new Color32(189, 183, 107, 255), "COLOR_DARK_KHAKI" },
            { new Color32(139, 0,   139, 255), "COLOR_DARK_MAGENTA" },
            { new Color32(85,  107, 47,  255), "COLOR_DARK_OLIVE_GREEN" },
            { new Color32(255, 140, 0,   255), "COLOR_DARK_ORANGE" },
            { new Color32(153, 50,  204, 255), "COLOR_DARK_ORCHID" },
            { new Color32(139, 0,   0,   255), "COLOR_DARK_RED" },
            { new Color32(233, 150, 122, 255), "COLOR_DARK_SALMON" },
            { new Color32(143, 188, 139, 255), "COLOR_DARK_SEA_GREEN" },
            { new Color32(72,  61,  139, 255), "COLOR_DARK_SLATE_BLUE" },
            { new Color32(47,  79,  79,  255), "COLOR_NEUTRAL_GREY" },
            { new Color32(0,   206, 209, 255), "COLOR_DARK_TURQUOISE" },
            { new Color32(148, 0,   211, 255), "COLOR_DARK_VIOLET" },
            { new Color32(255, 20,  147, 255), "COLOR_DEEP_PINK" },
            { new Color32(0,   191, 255, 255), "COLOR_DEEP_SKY_BLUE" },
            { new Color32(105, 105, 105, 255), "COLOR_DIM_GREY" },
            { new Color32(30,  144, 255, 255), "COLOR_CERULEAN_BLUE" },
            { new Color32(178, 34,  34,  255), "COLOR_CARMINE" },
            { new Color32(255, 250, 240, 255), "COLOR_FLORAL_WHITE" },
            { new Color32(34,  139, 34,  255), "COLOR_FOREST_GREEN" },
            { new Color32(220, 220, 220, 255), "COLOR_SILVER" },
            { new Color32(248, 248, 255, 255), "COLOR_GHOST_WHITE" },
            { new Color32(255, 215, 0,   255), "COLOR_CADMIUM_YELLOW" },
            { new Color32(218, 165, 32,  255), "COLOR_OCHRE" },
            { new Color32(128, 128, 128, 255), "COLOR_GREY" },
            { new Color32(0,   128, 0,   255), "COLOR_GREEN" },
            { new Color32(173, 255, 47,  255), "COLOR_GREEN_YELLOW" },
            { new Color32(240, 255, 240, 255), "COLOR_HONEYDEW" },
            { new Color32(255, 105, 180, 255), "COLOR_HOT_PINK" },
            { new Color32(205, 92,  92,  255), "COLOR_RED_OXIDE" },
            { new Color32(75,  0,   130, 255), "COLOR_INDIGO" },
            { new Color32(255, 255, 240, 255), "COLOR_IVORY" },
            { new Color32(240, 230, 140, 255), "COLOR_KHAKI" },
            { new Color32(230, 230, 250, 255), "COLOR_LAVENDER" },
            { new Color32(255, 240, 245, 255), "COLOR_PALE_LAVENDER" },
            { new Color32(124, 252, 0,   255), "COLOR_LUMINOUS_GREEN" },
            { new Color32(255, 250, 205, 255), "COLOR_PALE_LEMON" },
            { new Color32(173, 216, 230, 255), "COLOR_LIGHT_BLUE" },
            { new Color32(240, 128, 128, 255), "COLOR_LIGHT_CORAL" },
            { new Color32(224, 255, 255, 255), "COLOR_LIGHT_CYAN" },
            { new Color32(250, 250, 210, 255), "COLOR_PALE_LIME" },
            { new Color32(144, 238, 144, 255), "COLOR_LIGHT_GREEN" },
            { new Color32(211, 211, 211, 255), "COLOR_LIGHT_GREY" },
            { new Color32(255, 182, 193, 255), "COLOR_LIGHT_PINK" },
            { new Color32(255, 160, 122, 255), "COLOR_LIGHT_SALMON" },
            { new Color32(32,  178, 170, 255), "COLOR_LIGHT_SEA_GREEN" },
            { new Color32(135, 206, 250, 255), "COLOR_LIGHT_SKY_BLUE" },
            { new Color32(119, 136, 153, 255), "COLOR_LIGHT_ASH_BLUE" },
            { new Color32(176, 196, 222, 255), "COLOR_LIGHT_STEEL_BLUE" },
            { new Color32(255, 255, 224, 255), "COLOR_LIGHT_YELLOW" },
            { new Color32(0,   255, 0,   255), "COLOR_LIME" },
            { new Color32(50,  205, 50,  255), "COLOR_LIME_GREEN" },
            { new Color32(250, 240, 230, 255), "COLOR_LINEN" },
            { new Color32(255, 0,   255, 255), "COLOR_MAGENTA" },
            { new Color32(128, 0,   0,   255), "COLOR_MAROON" },
            { new Color32(102, 205, 170, 255), "COLOR_MEDIUM_AQUAMARINE" },
            { new Color32(0,   0,   205, 255), "COLOR_ULTRAMARINE" },
            { new Color32(186, 85,  211, 255), "COLOR_MEDIUM_ORCHID" },
            { new Color32(147, 112, 219, 255), "COLOR_MEDIUM_PURPLE" },
            { new Color32(60,  179, 113, 255), "COLOR_MEDIUM_SEA_GREEN" },
            { new Color32(123, 104, 238, 255), "COLOR_MEDIUM_SLATE_BLUE" },
            { new Color32(0,   250, 154, 255), "COLOR_MEDIUM_SPRING_GREEN" },
            { new Color32(72,  209, 204, 255), "COLOR_MEDIUM_TURQUOISE" },
            { new Color32(199, 21,  133, 255), "COLOR_ROSE_VOILET" },
            { new Color32(25,  25,  112, 255), "COLOR_MIDNIGHT_BLUE" },
            { new Color32(245, 255, 250, 255), "COLOR_MINT_CREAM" },
            { new Color32(255, 228, 225, 255), "COLOR_MISTY_ROSE" },
            { new Color32(255, 228, 181, 255), "COLOR_NAPLES_YELLOW" },
            { new Color32(255, 222, 173, 255), "COLOR_TITAN_BUFF" },
            { new Color32(0,   0,   128, 255), "COLOR_NAVY" },
            { new Color32(253, 245, 230, 255), "COLOR_OLD_LACE" },
            { new Color32(128, 128, 0,   255), "COLOR_OLIVE" },
            { new Color32(107, 142, 35,  255), "COLOR_MOSS_GREEN" },
            { new Color32(255, 165, 0,   255), "COLOR_ORANGE_YELLOW" },
            { new Color32(255, 69,  0,   255), "COLOR_SCARLET" },
            { new Color32(218, 112, 214, 255), "COLOR_ORCHID" },
            { new Color32(238, 232, 170, 255), "COLOR_PALE_OCHRE" },
            { new Color32(152, 251, 152, 255), "COLOR_PALE_GREEN" },
            { new Color32(175, 238, 238, 255), "COLOR_PALE_TURQUOISE" },
            { new Color32(219, 112, 147, 255), "COLOR_PALE_VIOLET_RED" },
            { new Color32(255, 239, 213, 255), "COLOR_PAPAYA_WHIP" },
            { new Color32(255, 218, 185, 255), "COLOR_PEACH_PUFF" },
            { new Color32(205, 133, 63,  255), "COLOR_RAW_SIENNA" },
            { new Color32(255, 192, 203, 255), "COLOR_PINK" },
            { new Color32(221, 160, 221, 255), "COLOR_LILAC" },
            { new Color32(176, 224, 230, 255), "COLOR_POWDER_BLUE" },
            { new Color32(95,  0,   128, 255), "COLOR_PURPLE" },
            { new Color32(255, 0,   0,   255), "COLOR_RED" },
            { new Color32(188, 143, 143, 255), "COLOR_ROSY_BROWN" },
            { new Color32(65,  105, 225, 255), "COLOR_ROYAL_BLUE" },
            { new Color32(139, 69,  19,  255), "COLOR_BURNT_UMBER" },
            { new Color32(250, 128, 114, 255), "COLOR_SALMON" },
            { new Color32(244, 164, 96,  255), "COLOR_SANDY_BROWN" },
            { new Color32(46,  139, 87,  255), "COLOR_SEA_GREEN" },
            { new Color32(255, 245, 238, 255), "COLOR_SEASHELL" },
            { new Color32(160, 82,  45,  255), "COLOR_SIENNA" },
            { new Color32(192, 192, 192, 255), "COLOR_SILVER" },
            { new Color32(135, 206, 235, 255), "COLOR_SKY_BLUE" },
            { new Color32(106, 90,  205, 255), "COLOR_SLATE_BLUE" },
            { new Color32(112, 128, 144, 255), "COLOR_SLATE_GREY" },
            { new Color32(255, 250, 250, 255), "COLOR_SNOW" },
            { new Color32(0,   255, 127, 255), "COLOR_SPRING_GREEN" },
            { new Color32(70,  130, 180, 255), "COLOR_STEEL_BLUE" },
            { new Color32(210, 180, 140, 255), "COLOR_TAN" },
            { new Color32(0,   128, 128, 255), "COLOR_TEAL" },
            { new Color32(216, 191, 216, 255), "COLOR_PALE_LILAC" },
            { new Color32(255, 99,  71,  255), "COLOR_TOMATO" },
            { new Color32(64,  224, 208, 255), "COLOR_TURQUOISE" },
            { new Color32(238, 130, 238, 255), "COLOR_VIOLET" },
            { new Color32(245, 222, 179, 255), "COLOR_TITAN" },
            { new Color32(255, 255, 255, 255), "COLOR_BRIGHT_WHITE" },
            { new Color32(245, 245, 245, 255), "COLOR_WHITE" },
            { new Color32(255, 255, 0,   255), "COLOR_YELLOW" },
            { new Color32(154, 205, 50,  255), "COLOR_LEAF_GREEN" },
            { new Color32(201, 104, 0,   255), "COLOR_ORANGE" },
        };

        private Dictionary<Color32, string> m_SecretColors = new Dictionary<Color32, string>
        {
            { new Color32(27, 15, 253, 255), "Patrick's Favorite Color" },
            { new Color32(72, 9, 12, 255), "Mach's Favorite Color" },
            { new Color32(126, 71, 143, 255), "Joyce's Favorite Color" },
            { new Color32(66, 113, 120, 255), "Tim's Favorite Color" },
            { new Color32(14, 81, 53, 255), "Drew's Favorite Color" },
            { new Color32(255, 220, 202, 255), "Jeremy's Favorite Color" },
            { new Color32(16, 100, 173, 255), "Elisabeth's Favorite Color" },
            { new Color32(217, 255, 109, 255), "Ashley's Favorite Color" },
            { new Color32(255, 241, 27, 255), "Tory's Favorite Color" },
            { new Color32(29, 59, 93, 255), "Paul's Favorite Color" },
            { new Color32(238, 70, 153, 255), "Izzy's Favorite Color" },
            { new Color32(255, 127, 80, 255), "Jon's Favorite Color" },
            { new Color32(176, 25, 126, 255), "Gottlieb's Favorite Color" },
            { new Color32(11, 28, 92, 255), "Coco's Favorite Color" },
        };

        public static ColorTable m_Instance;

        float ColorDistance(Color32 colorA, Color32 colorB)
        {
            // From https://en.wikipedia.org/wiki/Color_difference
            float deltaR = (float)(colorA.r - colorB.r);
            float deltaG = (float)(colorA.g - colorB.g);
            float deltaB = (float)(colorA.b - colorB.b);
            float avgR = (float)(colorA.r + colorB.r) / 2.0f;
            float r = (2.0f + avgR / 256.0f) * deltaR * deltaR;
            float g = 4.0f * deltaG * deltaG;
            float b = (2.0f + (255.0f - avgR) / 256.0f) * deltaB * deltaB;
            return Mathf.Sqrt(r + g + b);
        }

        void Awake()
        {
            m_Instance = this;
        }

        public string NearestColorTo(Color color)
        {
            float dist = float.MaxValue;
            Color32? nearestColor = null;
            foreach (var col in m_SecretColors.Keys)
            {
                float newDist = ColorDistance(col, color);
                if (newDist < m_SecretDistance)
                {
                    return m_SecretColors[col];
                }
            }

            foreach (var col in m_Colors.Keys)
            {
                float newDist = ColorDistance(col, color);
                if (newDist < dist)
                {
                    dist = newDist;
                    nearestColor = col;
                }
            }
            return LocalizationSettings.StringDatabase.GetLocalizedString(m_Colors[nearestColor.Value]);
        }
    }
} // namespace TiltBrush
