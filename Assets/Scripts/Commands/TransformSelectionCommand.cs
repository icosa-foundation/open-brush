// Copyright 2022 The Tilt Brush Authors
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
    public class TransformSelectionCommand : BaseCommand
    {
        private TrTransform m_StartTransform;
        private TrTransform m_EndTransform;
        
        public TransformSelectionCommand(TrTransform endXf, BaseCommand parent = null) : base(parent)
        {
            m_StartTransform = SelectionManager.m_Instance.SelectionTransform;
            m_EndTransform = endXf;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            SelectionManager.m_Instance.SelectionTransform = m_EndTransform;
        }

        protected override void OnUndo()
        {
            SelectionManager.m_Instance.SelectionTransform = m_StartTransform;
        }
    }
} // namespace TiltBrush
