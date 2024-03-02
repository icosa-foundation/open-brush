// Copyright 2024 The Tilt Brush Authors
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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public enum SelectionType
    {
        Nothing,
        Stroke,
        Image,
        Video,
        Model,
        Guide,
        Mixed
    }

    public class InspectorPanel : BasePanel
    {
        public GrabWidget LastWidget { get; set; }
        public InspectorTabButton m_InitialTabButton;

        public SelectionType CurrentSelectionType => m_CurrentSelectionType;
        private SelectionType m_CurrentSelectionType;

        public int CurrentSelectionCount => m_CurrentSelectionCount;
        private int m_CurrentSelectionCount;

        private InspectorBaseTab[] m_Tabs;
        private InspectorTabButton[] m_TabButtons;

        private IEnumerable<InspectorBaseTab> AllTabs
        {
            get
            {
                {
                    if (m_Tabs == null)
                    {
                        m_Tabs = GetComponentsInChildren<InspectorBaseTab>(includeInactive: true);
                    }
                    return m_Tabs;
                }
            }
        }

        private IEnumerable<InspectorTabButton> AllTabButtons
        {
            get
            {
                {
                    if (m_TabButtons == null)
                    {
                        m_TabButtons = GetComponentsInChildren<InspectorTabButton>(includeInactive: true);
                    }
                    return m_TabButtons;
                }
            }
        }

        void Start()
        {
            HandleTabButtonPressed(m_InitialTabButton);
        }

        private void OnSelectionChanged()
        {

            if (SelectionManager.m_Instance.HasSelection)
            {
                var selectedWidgets = SelectionManager.m_Instance.GetValidSelectedWidgets();

                bool hasWidgets = selectedWidgets.Count > 0;
                bool hasStrokes = SelectionManager.m_Instance.SelectedStrokeCount > 0;


                if (hasStrokes && hasWidgets)
                {
                    m_CurrentSelectionType = SelectionType.Mixed;
                    m_CurrentSelectionCount = selectedWidgets.Count + SelectionManager.m_Instance.SelectedStrokeCount;
                }
                else if (hasStrokes)
                {
                    m_CurrentSelectionType = SelectionType.Stroke;
                    m_CurrentSelectionCount = SelectionManager.m_Instance.SelectedStrokeCount;
                }
                else if (hasWidgets)
                {
                    var selectedImages = selectedWidgets.Where(w => w is ImageWidget).ToList();
                    var selectedVideos = selectedWidgets.Where(w => w is VideoWidget).ToList();
                    var selectedModels = selectedWidgets.Where(w => w is ModelWidget).ToList();
                    var selectedGuides = selectedWidgets.Where(w => w is StencilWidget).ToList();

                    bool multipleTypes = (selectedImages.Count > 0 ? 1 : 0) +
                        (selectedVideos.Count > 0 ? 1 : 0) +
                        (selectedModels.Count > 0 ? 1 : 0) +
                        (selectedGuides.Count > 0 ? 1 : 0) > 1;

                    if (multipleTypes)
                    {
                        m_CurrentSelectionType = SelectionType.Mixed;
                        m_CurrentSelectionCount = selectedWidgets.Count;
                    }
                    else if (selectedImages.Count > 0)
                    {
                        m_CurrentSelectionType = SelectionType.Image;
                        m_CurrentSelectionCount = selectedImages.Count;
                    }
                    else if (selectedVideos.Count > 0)
                    {
                        m_CurrentSelectionType = SelectionType.Video;
                        m_CurrentSelectionCount = selectedVideos.Count;
                    }
                    else if (selectedModels.Count > 0)
                    {
                        m_CurrentSelectionType = SelectionType.Model;
                        m_CurrentSelectionCount = selectedModels.Count;
                    }
                    else if (selectedGuides.Count > 0)
                    {
                        m_CurrentSelectionType = SelectionType.Guide;
                        m_CurrentSelectionCount = selectedGuides.Count;
                    }
                }
                else
                {
                    Debug.LogError($"Unexpected selection state");
                    m_CurrentSelectionType = SelectionType.Nothing;
                    m_CurrentSelectionCount = 0;
                }
            }
            else
            {
                m_CurrentSelectionType = SelectionType.Nothing;
                m_CurrentSelectionCount = 0;
            }

            foreach (var tab in AllTabs)
            {
                tab.OnSelectionChanged();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            App.Switchboard.SelectionChanged += OnSelectionChanged;
        }

        void OnDestroy()
        {
            App.Switchboard.SelectionChanged -= OnSelectionChanged;
        }

        public void HandleTabButtonPressed(InspectorTabButton btn)
        {
            foreach (var b in AllTabButtons)
            {
                b.SetButtonSelected(false);
            }
            btn.SetButtonSelected(true);

            foreach (var t in AllTabs)
            {
                t.gameObject.SetActive(t == btn.Tab);
            }
        }
    }
}
