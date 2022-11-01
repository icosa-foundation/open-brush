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

using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class TransformSelectionCommand : BaseCommand
    {
        private TrTransform m_Transform;
        private Vector3 m_Pivot;

        public TransformSelectionCommand(TrTransform xf, Vector3 pivot, BaseCommand parent = null) : base(parent)
        {
            m_Transform = xf;
            m_Pivot = pivot;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            TransformItems.TransformSelected(m_Pivot, m_Transform);
        }

        protected override void OnUndo()
        {
            TransformItems.TransformSelected(m_Pivot, m_Transform.inverse);
        }
    }
} // namespace TiltBrush
