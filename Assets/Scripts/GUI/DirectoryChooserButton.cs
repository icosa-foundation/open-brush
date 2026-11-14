// Copyright 2024 The Open Brush Authors
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
using System.Globalization;
using UnityEngine;
using TMPro;

namespace TiltBrush
{
    // One row of the directory tree. Pressing the expand marker (or the indent to its left)
    // toggles the folder; pressing anywhere else navigates to it.
    class DirectoryChooserButton : BaseButton
    {
        // Oswald has no arrow glyphs, so stick to ASCII markers.
        private const string kExpandedMarker = "-";
        private const string kCollapsedMarker = "+";
        private const float kIndentPerLevelEm = 1.1f;
        private const float kMarkerWidthEm = 0.7f;
        private const string kCurrentColor = "#FFC94A";
        // Extra hit area to the right of the marker, as a fraction of the line height.
        private const float kMarkerHitPaddingEm = 0.6f;

        public TextMeshPro m_TextLabel;
        [NonSerialized] public PopUpWindow_DirectoryChooser m_Popup;
        [NonSerialized] public ReferencePanel m_Panel;

        private ReferenceDirectoryTree.Node m_Node;

        public void SetNode(ReferenceDirectoryTree.Node node)
        {
            m_Node = node;

            string marker = node.HasChildren
                ? (node.IsExpanded ? kExpandedMarker : kCollapsedMarker)
                : " ";
            // TMP parses backslash escapes as control characters, and the name itself must
            // not be parsed as rich text.
            string name = (node.Label ?? string.Empty).Replace("\\", "\\\\");
            string label = $"<noparse>{name}</noparse>";
            if (node.IsCurrent)
            {
                label = $"<b><color={kCurrentColor}>{label}</color></b>";
            }

            // Invariant culture: TMP tags need '.' decimal separators.
            string indent = (node.Depth * kIndentPerLevelEm).ToString("0.##", CultureInfo.InvariantCulture);
            string markerWidth = kMarkerWidthEm.ToString("0.##", CultureInfo.InvariantCulture);
            m_TextLabel.text = $"<indent={indent}em><mspace={markerWidth}em>{marker}</mspace> {label}";
        }

        public override void ButtonPressed(RaycastHit rHitInfo)
        {
            base.ButtonPressed(rHitInfo);
            if (m_Node == null)
            {
                return;
            }

            if (m_Node.HasChildren && IsMarkerHit(rHitInfo.point))
            {
                m_Popup.ToggleExpanded(m_Node.Path);
                return;
            }

            m_Panel.CloseActivePopUp(false);
            if (!m_Node.IsCurrent)
            {
                m_Panel.ChangeDirectoryForCurrentTab(m_Node.Path);
            }
        }

        private bool IsMarkerHit(Vector3 worldPoint)
        {
            var textInfo = m_TextLabel.textInfo;
            if (textInfo == null || textInfo.characterCount == 0)
            {
                return false;
            }
            // The marker is always the first rendered character.
            var markerInfo = textInfo.characterInfo[0];
            Vector3 local = m_TextLabel.transform.InverseTransformPoint(worldPoint);
            // characterInfo is in the label's local space, as is lineHeight.
            float padding = textInfo.lineInfo[0].lineHeight * kMarkerHitPaddingEm;
            return local.x <= markerInfo.topRight.x + padding;
        }

        protected override void SetMaterialColor(Color rColor)
        {
            // m_TextLabel.color = rColor;
        }
    }
}
