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

using UnityEngine;
namespace TiltBrush
{
    public class PolyhydraMultistateButton : MultistateButton
    {

        [SerializeField] private int m_InitialIndex = 0;
        public ModeTypes ModeType;

        public enum ModeTypes
        {
            CreateMode,
            ModifyMode
        }

        public Option CurrentOption
        {
            get
            {
                return m_Options[m_CurrentOptionIdx];
            }
        }

        override protected void OnStart()
        {
            base.OnStart();
            ForceSelectedOption(m_InitialIndex);
        }

        override protected void OnButtonPressed()
        {
            var polyhydraTool = SketchSurfacePanel.m_Instance.GetToolOfType(BaseTool.ToolType.PolyhydraTool) as PolyhydraTool;
            SetSelectedOption((m_CurrentOptionIdx + 1) % NumOptions);
            switch (ModeType)
            {
                case ModeTypes.CreateMode:
                    polyhydraTool.SetCreateMode(m_CurrentOptionIdx);
                    break;
                case ModeTypes.ModifyMode:
                    polyhydraTool.SetModifyMode(m_CurrentOptionIdx);
                    break;
            }

        }

    }
}
