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
        public InspectorTabButton m_InitialTabButton;

        public SelectionType CurrentSelectionType => m_CurrentSelectionType;
        private SelectionType m_CurrentSelectionType;

        public TrTransform  CurrentSelectionTr => m_CurrentSelectionTr;
        private TrTransform m_CurrentSelectionTr;

        public int CurrentSelectionCount => m_CurrentSelectionCount;
        private int m_CurrentSelectionCount;

        public List<ImageWidget> SelectedImages { get; private set; }
        public List<VideoWidget> SelectedVideos { get; private set; }
        public List<ModelWidget> SelectedModels { get; private set; }
        public List<StencilWidget> SelectedGuides { get; private set; }

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

        private void OnSelectionPoseChanged(TrTransform prev, TrTransform current)
        {
            foreach (var tab in AllTabs)
            {
                tab.OnSelectionPoseChanged();
            }
        }

        private void OnSelectionChanged()
        {
            if (!SelectionManager.m_Instance.HasSelection)
            {
                m_CurrentSelectionType = SelectionType.Nothing;
                m_CurrentSelectionCount = 0;
            }
            else
            {
                m_CurrentSelectionTr = SelectionManager.m_Instance.SelectionTransform;
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
                    SelectedImages = selectedWidgets.OfType<ImageWidget>().ToList();
                    SelectedVideos = selectedWidgets.OfType<VideoWidget>().ToList();
                    SelectedModels = selectedWidgets.OfType<ModelWidget>().ToList();
                    SelectedGuides = selectedWidgets.OfType<StencilWidget>().ToList();

                    bool multipleTypes = (SelectedImages.Count > 0 ? 1 : 0) +
                        (SelectedVideos.Count > 0 ? 1 : 0) +
                        (SelectedModels.Count > 0 ? 1 : 0) +
                        (SelectedGuides.Count > 0 ? 1 : 0) > 1;

                    if (multipleTypes)
                    {
                        m_CurrentSelectionType = SelectionType.Mixed;
                        m_CurrentSelectionCount = selectedWidgets.Count;
                    }
                    else if (SelectedImages.Count > 0)
                    {
                        m_CurrentSelectionType = SelectionType.Image;
                        m_CurrentSelectionCount = SelectedImages.Count;
                    }
                    else if (SelectedVideos.Count > 0)
                    {
                        m_CurrentSelectionType = SelectionType.Video;
                        m_CurrentSelectionCount = SelectedVideos.Count;
                    }
                    else if (SelectedModels.Count > 0)
                    {
                        m_CurrentSelectionType = SelectionType.Model;
                        m_CurrentSelectionCount = SelectedModels.Count;
                    }
                    else if (SelectedGuides.Count > 0)
                    {
                        m_CurrentSelectionType = SelectionType.Guide;
                        m_CurrentSelectionCount = SelectedGuides.Count;
                    }
                }
                else
                {
                    Debug.LogError($"Unexpected selection state");
                    m_CurrentSelectionType = SelectionType.Nothing;
                    m_CurrentSelectionCount = 0;
                }
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
            App.Scene.SelectionCanvas.PoseChanged += OnSelectionPoseChanged;
        }

        void OnDestroy()
        {
            App.Switchboard.SelectionChanged -= OnSelectionChanged;
            App.Scene.SelectionCanvas.PoseChanged -= OnSelectionPoseChanged;
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
                t.TabButton = btn;
            }
        }
    }
}
