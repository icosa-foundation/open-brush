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

using System.Linq;
using UnityEngine;
namespace TiltBrush
{
    public class AddLayerCommand : BaseCommand
    {
        private CanvasScript m_prevActiveLayer;
        private CanvasScript m_Layer;
        private bool m_MakeActive;

        public AddLayerCommand(bool makeActive, BaseCommand parent = null) : base(parent)
        {
            m_MakeActive = makeActive;
            m_prevActiveLayer = App.Scene.ActiveCanvas;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            m_Layer = App.Scene.AddLayerNow();
            App.Scene.ActiveCanvas = m_Layer;
        }

        protected override void OnUndo()
        {
            m_Layer.gameObject.SetActive(false);
            App.Scene.MarkLayerAsDeleted(m_Layer);
            App.Scene.ActiveCanvas = m_prevActiveLayer;
        }
    }

} // namespace TiltBrush
