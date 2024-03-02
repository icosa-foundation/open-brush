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
using TMPro;
using UnityEngine;

namespace TiltBrush
{

    public class InspectorPanel : BasePanel
    {
        public Bounds SelectionBounds { get; set; }
        public GrabWidget LastWidget { get; set; }

        private InspectorBaseTab[] m_Tabs;

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

        private void OnSelectionChanged()
        {
            SelectionBounds = App.Scene.SelectionCanvas.GetCanvasBoundingBox();
            OnSelectionPoseChanged();
        }

        public void HandleTabButtonPressed(InspectorTabButton btn)
        {
            foreach (var t in AllTabs)
            {
                if (t == btn.Tab)
                {
                    t.gameObject.SetActive(true);
                }
                else
                {
                    t.gameObject.SetActive(false);
                }
            }
        }
    }
}
