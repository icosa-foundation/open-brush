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
using UnityEngine;

namespace TiltBrush
{

    public class InspectorPanel : BasePanel
    {
        public Bounds SelectionBounds { get; set; }
        public GrabWidget LastWidget { get; set; }
        public InspectorTabButton m_InitialTabButton;

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

        void OnSelectionPoseChanged(TrTransform _, TrTransform __)
        {
            OnSelectionPoseChanged();
        }

        void OnSelectionPoseChanged()
        {
        }

        protected override void Awake()
        {
            base.Awake();
            App.Scene.SelectionCanvas.PoseChanged += OnSelectionPoseChanged;
            App.Switchboard.SelectionChanged += OnSelectionChanged;
        }

        void OnDestroy()
        {
            App.Scene.SelectionCanvas.PoseChanged -= OnSelectionPoseChanged;
            App.Switchboard.SelectionChanged -= OnSelectionChanged;
        }

        void Start()
        {
            HandleTabButtonPressed(m_InitialTabButton);
        }

        private void OnSelectionChanged()
        {
            SelectionBounds = App.Scene.SelectionCanvas.GetCanvasBoundingBox();
            OnSelectionPoseChanged();
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
