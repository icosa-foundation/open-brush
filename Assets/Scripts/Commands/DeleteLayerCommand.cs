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
    public class DeleteLayerCommand : BaseCommand
    {
        private CanvasScript m_Layer;

        public DeleteLayerCommand(int layerIndex, BaseCommand parent = null) : base(parent)
        {
            m_Layer = App.Scene.GetCanvasByLayerIndex(layerIndex);
        }

        public DeleteLayerCommand(CanvasScript layer, BaseCommand parent = null) : base(parent)
        {
            m_Layer = layer;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            if (m_Layer.gameObject.activeSelf)
            {
                m_Layer.gameObject.SetActive(false);
                App.Scene.MarkLayerAsDeleted(m_Layer);
                if (App.Scene.ActiveCanvas == m_Layer)
                {
                    // If we've deleted the active canvas the switch to main
                    App.Scene.ActiveCanvas = App.Scene.MainCanvas;
                }
            }
        }

        protected override void OnUndo()
        {
            if (!m_Layer.gameObject.activeSelf)
            {
                m_Layer.gameObject.SetActive(true);
                App.Scene.MarkLayerAsNotDeleted(m_Layer);
            }
        }
    }

} // namespace TiltBrush
