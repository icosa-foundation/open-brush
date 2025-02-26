﻿// Copyright 2021 The Open Brush Authors
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
    public class ModifyStencilGridSizeCommand : BaseCommand
    {
        private readonly float m_StartSize;
        private float m_EndSize;
        private bool m_Final;

        public static readonly int GlobalGridSizeMultiplierHash = Shader.PropertyToID("_GlobalGridSizeMultiplier");

        public ModifyStencilGridSizeCommand(float endSize, bool final = false,
                                            BaseCommand parent = null) : base(parent)
        {
            m_StartSize = Shader.GetGlobalFloat(GlobalGridSizeMultiplierHash);
            m_EndSize = endSize;
            m_Final = final;
        }

        public override bool NeedsSave => true;

        protected override void OnUndo()
        {
            Shader.SetGlobalFloat(GlobalGridSizeMultiplierHash, m_StartSize);
        }

        protected override void OnRedo()
        {
            Shader.SetGlobalFloat(GlobalGridSizeMultiplierHash, m_EndSize);
        }

        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) { return true; }
            if (m_Final) { return false; }
            ModifyStencilGridSizeCommand gridSizeCommand = other as ModifyStencilGridSizeCommand;
            if (gridSizeCommand == null) { return false; }
            m_EndSize = gridSizeCommand.m_EndSize;
            m_Final = gridSizeCommand.m_Final;
            return true;
        }
    }
} // namespace TiltBrush
