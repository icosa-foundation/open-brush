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

using System.Collections.Generic;
using System.Linq;
namespace TiltBrush
    {
        public class SquashLayerCommand : BaseCommand
        {
            private CanvasScript m_SquashedLayer;
            private CanvasScript m_DestinationLayer;
            private IEnumerable<Stroke> m_OriginalStrokes;

            public SquashLayerCommand(int squashedLayerIndex, int destinationLayerIndex, BaseCommand parent = null) : base(parent)
            {
                var squashedLayer = App.Scene.GetCanvasByLayerIndex(squashedLayerIndex);
                var destinationLayer = App.Scene.GetCanvasByLayerIndex(destinationLayerIndex);
                new SquashLayerCommand(
                    squashedLayer,
                    destinationLayer,
                    parent
                );
            }
            
            public SquashLayerCommand(CanvasScript squashedLayer, CanvasScript destinationLayer, BaseCommand parent = null) : base(parent)
            {
                m_SquashedLayer = squashedLayer;
                m_DestinationLayer = destinationLayer;
                m_OriginalStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                    .Where(x => x.Canvas == m_SquashedLayer);
            }
            
            public override bool NeedsSave { get { return true; } }

            protected override void OnRedo()
            {
                // TODO: this should defer updates to the batches until the end
                foreach (var stroke in SketchMemoryScript.AllStrokes())
                {
                    if (stroke.Canvas == m_SquashedLayer)
                    {
                        stroke.SetParentKeepWorldPosition(m_DestinationLayer);
                    }
                }
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush,
                    string.Format("Squashed {0}", m_SquashedLayer.gameObject.name));
                App.Scene.ActiveCanvas = m_DestinationLayer;
                App.Scene.MarkLayerAsDeleted(m_SquashedLayer);
            }

            protected override void OnUndo()
            {
                if (!m_SquashedLayer.gameObject.activeSelf)
                {
                    m_SquashedLayer.gameObject.SetActive(true);
                    foreach (var stroke in m_OriginalStrokes)
                    {
                        stroke.SetParentKeepWorldPosition(m_SquashedLayer);
                    }
                    App.Scene.MarkLayerAsNotDeleted(m_SquashedLayer);
                }
            }
        }

    } // namespace TiltBrush
