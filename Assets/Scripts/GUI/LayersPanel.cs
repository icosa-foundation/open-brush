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

using TiltBrush.Layers;

namespace TiltBrush
{
    public class LayersPanel : BasePanel
    {

        private LayerUI_Manager m_LayerUI_Manager;

        void Awake()
        {
            m_LayerUI_Manager = GetComponent<LayerUI_Manager>();
        }

        public override void GotoPage(int iIndex)
        {
            m_LayerUI_Manager.GotoPage(iIndex);
        }

        public override void AdvancePage(int iAmount)
        {
            m_LayerUI_Manager.AdvancePage(iAmount);
        }
    }
}
