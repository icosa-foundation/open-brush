// Copyright 2021 The Open Brush Authors
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

namespace TiltBrush
{
    public class ModifyStencilAttractDistanceCommand : BaseCommand
    {
        private readonly float m_StartDistance;
        private float m_EndDistance;
        private bool m_Final;

        public ModifyStencilAttractDistanceCommand(float endDistance, bool final = false,
                                BaseCommand parent = null) : base(parent)
        {
            m_StartDistance = WidgetManager.m_Instance.StencilAttractDist;
            m_EndDistance = endDistance;
            m_Final = final;
        }

        public override bool NeedsSave => true;

        protected override void OnUndo()
        {
            WidgetManager.m_Instance.StencilAttractDist = m_StartDistance;
        }

        protected override void OnRedo()
        {
            WidgetManager.m_Instance.StencilAttractDist = m_EndDistance;
        }

        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) { return true; }
            if (m_Final) { return false; }
            ModifyStencilAttractDistanceCommand distanceCommand = other as ModifyStencilAttractDistanceCommand;
            if (distanceCommand == null) { return false; }
            m_EndDistance = distanceCommand.m_EndDistance;
            m_Final = distanceCommand.m_Final;
            return true;
        }
    }
} // namespace TiltBrush
