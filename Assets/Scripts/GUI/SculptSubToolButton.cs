// Copyright 2022 Chingiz Dadashov-Khandan
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

using UnityEngine;

namespace TiltBrush
{
    public class SculptSubToolButton : BaseButton
    {

        [SerializeField]
        public SculptSubToolManager.SubTool m_SubTool;
        private FlattenSubTool.InfluenceMode? m_LastFlattenInfluenceMode;

        override protected void Start()
        {
            base.Start();
            RefreshSelection();
            RefreshFlattenInfluenceDescription();
        }

        override protected void OnButtonPressed()
        {
            bool toggleFlattenMode =
                m_SubTool == SculptSubToolManager.SubTool.Flatten &&
                SculptSubToolManager.m_Instance.ActiveSubTool == m_SubTool;
            if (toggleFlattenMode)
            {
                SculptSubToolManager.m_Instance.TryToggleFlattenInfluenceMode();
            }
            else
            {
                SculptSubToolManager.m_Instance.SetSubTool(m_SubTool);
            }
            foreach (SculptSubToolButton button in
                transform.parent.GetComponentsInChildren<SculptSubToolButton>(true))
            {
                button.RefreshSelection();
                button.RefreshFlattenInfluenceDescription();
            }
        }

        override public void UpdateVisuals()
        {
            base.UpdateVisuals();
            RefreshFlattenInfluenceDescription();
        }

        private void RefreshSelection()
        {
            if (SculptSubToolManager.m_Instance != null)
            {
                SetButtonSelected(
                    SculptSubToolManager.m_Instance.ActiveSubTool == m_SubTool);
            }
        }

        private void RefreshFlattenInfluenceDescription()
        {
            if (m_SubTool != SculptSubToolManager.SubTool.Flatten ||
                SculptSubToolManager.m_Instance == null)
            {
                return;
            }

            FlattenSubTool.InfluenceMode mode =
                SculptSubToolManager.m_Instance.FlattenInfluenceMode;
            if (m_LastFlattenInfluenceMode == mode)
            {
                return;
            }

            m_LastFlattenInfluenceMode = mode;
            SetExtraDescriptionText(mode == FlattenSubTool.InfluenceMode.Sphere
                ? "Sphere influence"
                : "Plane-projected influence");
        }
    }
} // namespace TiltBrush
