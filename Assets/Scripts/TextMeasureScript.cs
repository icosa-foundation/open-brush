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

using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TiltBrush
{

    //This script is written to be attached to an unused, global TextMesh object for the
    //  purposes of measuring the real-world length of a rendered string.  In order to
    //  measure, we first need to render the string.  After a unique length query has
    //  been requested, the result is stored in a dictionary for easy, repeatable access.
    public class TextMeasureScript : MonoBehaviour
    {
        static public TextMeasureScript m_Instance;
        private TextMeshPro m_TextMesh;

        //dictionary key-- override IEquatable to cut down on GC and increase speed
        private struct TextParams : IEquatable<TextParams>
        {
            public float m_FontSize;
            public TMP_FontAsset m_Font;
            public string m_Text;

            public bool Equals(TextParams other)
            {
                return (m_FontSize == other.m_FontSize) &&
                    m_Text.Equals(other.m_Text) && m_Font.name.Equals(other.m_Font.name);
            }
            public override bool Equals(object other)
            {
                if (!(other is TextParams))
                {
                    return false;
                }
                return Equals((TextParams)other);
            }
            public override int GetHashCode()
            {
                return m_FontSize.GetHashCode() ^ m_Text.GetHashCode();
            }
            public static bool operator ==(TextParams a, TextParams b)
            {
                return a.m_FontSize == b.m_FontSize &&
                    a.m_Text.Equals(b.m_Text) && a.m_Font.name.Equals(b.m_Font.name);
            }
            public static bool operator !=(TextParams a, TextParams b)
            {
                return a.m_FontSize == b.m_FontSize &&
                    a.m_Text.Equals(b.m_Text) && a.m_Font.name.Equals(b.m_Font.name);
            }
        }
        private Dictionary<TextParams, Vector2> m_StringSizeMap;

        void Awake()
        {
            m_Instance = this;
            m_TextMesh = GetComponent<TextMeshPro>();
            m_StringSizeMap = new Dictionary<TextParams, Vector2>();
        }

        static public float GetTextWidth(TextMeshPro text)
        {
            return m_Instance.GetTextWidth(text.fontSize, text.font, text.text);
        }

        static public float GetTextHeight(TextMeshPro text)
        {
            return m_Instance.GetTextHeight(text.fontSize, text.font, text.text);
        }

        public float GetTextWidth(float fFontSize, TMP_FontAsset rFont, string sText)
        {
            //look for this string in the dictionary first
            TextParams rParams = new TextParams
            {
                m_FontSize = fFontSize,
                m_Font = rFont,
                m_Text = sText
            };

            if (m_StringSizeMap.ContainsKey(rParams))
            {
                return m_StringSizeMap[rParams].x;
            }

            //add new string to our map
            Vector2 vSize = AddNewString(rParams, fFontSize, rFont, sText);
            return vSize.x;
        }

        public float GetTextHeight(float fFontSize, TMP_FontAsset rFont, string sText)
        {
            //look for this string in the dictionary first
            TextParams rParams = new TextParams
            {
                m_FontSize = fFontSize,
                m_Font = rFont,
                m_Text = sText
            };

            if (m_StringSizeMap.ContainsKey(rParams))
            {
                return m_StringSizeMap[rParams].y;
            }

            //add new string to our map
            Vector2 vSize = AddNewString(rParams, fFontSize, rFont, sText);
            return vSize.y;
        }

        Vector2 AddNewString(TextParams rParams, float fFontSize, TMP_FontAsset rFont, string sText)
        {
            m_TextMesh.fontSize = fFontSize;
            m_TextMesh.font = rFont;
            m_TextMesh.text = sText;
            m_TextMesh.ForceMeshUpdate(true, true);
            Vector2 vSize = new Vector2(m_TextMesh.preferredWidth, m_TextMesh.preferredHeight);
            m_StringSizeMap.Add(rParams, vSize);
            return vSize;
        }
    }
} // namespace TiltBrush
