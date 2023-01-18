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
using UnityEngine;
namespace TiltBrush
{
    public class SquashLayerCommand : BaseCommand
    {
        private CanvasScript m_SquashedLayer;
        private CanvasScript m_DestinationLayer;
        private Stroke[] m_OriginalStrokes;
        private GrabWidget[] m_ActiveWidgets;
        private bool SquashedLayerWasActive;

        public SquashLayerCommand(int squashedLayerIndex, int destinationLayerIndex, BaseCommand parent = null) : base(parent)
        {
            m_SquashedLayer = App.Scene.GetCanvasByLayerIndex(squashedLayerIndex);
            m_DestinationLayer = App.Scene.GetCanvasByLayerIndex(destinationLayerIndex);
            Init();
        }

        public SquashLayerCommand(CanvasScript squashedLayer, CanvasScript destinationLayer, BaseCommand parent = null) : base(parent)
        {
            m_SquashedLayer = squashedLayer;
            m_DestinationLayer = destinationLayer;
            Init();
        }

        private void Init()
        {
            m_OriginalStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                .Where(x => x.Canvas == m_SquashedLayer).ToArray();
            SquashedLayerWasActive = App.Scene.ActiveCanvas == m_SquashedLayer;
            m_ActiveWidgets = m_SquashedLayer.GetComponentsInChildren<GrabWidget>();
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            foreach (var stroke in m_OriginalStrokes)
            {
                stroke.SetParentKeepWorldPosition(m_DestinationLayer);
            }

            foreach (var widget in m_ActiveWidgets)
            {
                widget.transform.SetParent(m_DestinationLayer.transform, true);
            }

            App.Scene.ActiveCanvas = m_DestinationLayer;
            App.Scene.MarkLayerAsDeleted(m_SquashedLayer);
            if (SquashedLayerWasActive) App.Scene.ActiveCanvas = m_DestinationLayer;
        }

        protected override void OnUndo()
        {
            m_SquashedLayer.gameObject.SetActive(true);
            App.Scene.MarkLayerAsNotDeleted(m_SquashedLayer);
            if (SquashedLayerWasActive) App.Scene.ActiveCanvas = m_SquashedLayer;
            foreach (var stroke in m_OriginalStrokes)
            {
                stroke.SetParentKeepWorldPosition(m_SquashedLayer);
            }

            foreach (var widget in m_ActiveWidgets)
            {
                widget.transform.SetParent(m_SquashedLayer.transform, true);
            }

        }
    }

} // namespace TiltBrush
