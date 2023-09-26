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

using UnityEngine;
namespace TiltBrush
{
    public class ModifyStencilGridLineWidthCommand : BaseCommand
    {
        private readonly float m_StartSize;
        private float m_EndSize;
        private bool m_Final;

        public static readonly int GlobalGridLineWidthMultiplierHash = Shader.PropertyToID("_GlobalGridLineWidthMultiplier");

        public ModifyStencilGridLineWidthCommand(float endSize, bool final = false,
                                            BaseCommand parent = null) : base(parent)
        {
            m_StartSize = Shader.GetGlobalFloat(GlobalGridLineWidthMultiplierHash);
            m_EndSize = endSize;
            m_Final = final;
        }

        public override bool NeedsSave => true;

        protected override void OnUndo()
        {
            Shader.SetGlobalFloat(GlobalGridLineWidthMultiplierHash, m_StartSize);
        }

        protected override void OnRedo()
        {
            Shader.SetGlobalFloat(GlobalGridLineWidthMultiplierHash, m_EndSize);
        }

        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) { return true; }
            if (m_Final) { return false; }
            ModifyStencilGridLineWidthCommand gridLineWidthCommand = other as ModifyStencilGridLineWidthCommand;
            if (gridLineWidthCommand == null) { return false; }
            m_EndSize = gridLineWidthCommand.m_EndSize;
            m_Final = gridLineWidthCommand.m_Final;
            return true;
        }
    }
} // namespace TiltBrush
