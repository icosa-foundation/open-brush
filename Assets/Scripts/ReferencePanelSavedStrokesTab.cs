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
using UnityEngine;

namespace TiltBrush
{
    public class ReferencePanelSavedStrokesTab : ReferencePanelTab
    {
        public class SavedStrokesIcon : ReferenceIcon
        {
            public ReferencePanel Parent { get; set; }
            public bool TextureAssigned { get; set; }

            public SavedStrokesButton SavedStrokesButton
            {
                get { return Button as SavedStrokesButton; }
            }

            public override void Refresh(int catalogIndex)
            {
                var savedStrokesFile = SavedStrokesCatalog.Instance.GetSavedStrokeFileAtIndex(catalogIndex);
                SavedStrokesButton.SavedStrokeFile = savedStrokesFile;
                var icon = savedStrokesFile.Thumbnail;
                if (icon == null)
                {
                    savedStrokesFile.ForceLoadThumbnail();
                    icon = savedStrokesFile.Thumbnail;
                }

                Button.SetButtonTexture(icon, 1);
                SavedStrokesButton.RefreshDescription();

                if (savedStrokesFile != null)
                {
                    Button.gameObject.SetActive(true);
                    TextureAssigned = false;
                }
                else
                {
                    Button.gameObject.SetActive(false);
                    TextureAssigned = true;
                }
            }
        }

        private bool m_AllIconTexturesAssigned;
        private Material m_PreviewMaterial;
        private bool m_TabActive;

        public override IReferenceItemCatalog Catalog
        {
            get { return SavedStrokesCatalog.Instance; }
        }
        public override ReferenceButton.Type ReferenceButtonType
        {
            get { return ReferenceButton.Type.SavedStrokes; }
        }
        protected override Type ButtonType
        {
            get { return typeof(SavedStrokesButton); }
        }
        protected override Type IconType
        {
            get { return typeof(SavedStrokesIcon); }
        }

        public override void OnTabEnable()
        {
            m_TabActive = true;
        }

        public override void OnTabDisable()
        {
            m_TabActive = false;
        }

        public override void RefreshTab(bool selected)
        {
            base.RefreshTab(selected);
            if (selected)
            {
                m_AllIconTexturesAssigned = false;
            }
            m_TabActive = selected;
        }

        public override void InitTab()
        {
            base.InitTab();
            foreach (var icon in m_Icons)
            {
                (icon as SavedStrokesIcon).Parent = GetComponentInParent<ReferencePanel>();
            }
            OnTabDisable();
        }

        public override void UpdateTab()
        {
            base.UpdateTab();
            if (!m_AllIconTexturesAssigned)
            {
                m_AllIconTexturesAssigned = true;

                //poll sketch catalog until icons have loaded
                for (int i = 0; i < m_Icons.Length; ++i)
                {
                    var imageIcon = m_Icons[i] as SavedStrokesIcon;
                    if (!imageIcon.TextureAssigned && imageIcon.Button.gameObject.activeSelf)
                    {
                        int catalogIndex = m_IndexOffset + i;

                        var savedSketchFile = SavedStrokesCatalog.Instance.GetSavedStrokeFileAtIndex(catalogIndex);
                        if (savedSketchFile != null)
                        {
                            imageIcon.Button.SetButtonTexture(savedSketchFile.Thumbnail);
                            imageIcon.TextureAssigned = true;
                        }
                        else
                        {
                            m_AllIconTexturesAssigned = false;
                        }
                    }
                }
            }
        }

        public override void OnUpdateGazeBehavior(Color panelColor, bool gazeActive, bool available)
        {
            base.OnUpdateGazeBehavior(panelColor, gazeActive, available);
            bool? buttonsGrayscale = null;
            if (!gazeActive)
            {
                buttonsGrayscale = true;
            }
            else if (available)
            {
                buttonsGrayscale = false;
            }
            else
            {
                // Don't mess with grayscale-ness
            }

            if (buttonsGrayscale != null)
            {
                foreach (var icon in m_Icons)
                {
                    icon.Button.SetButtonGrayscale(buttonsGrayscale.Value);
                }
            }
        }
    }
} // namespace TiltBrush
