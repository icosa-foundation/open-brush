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

namespace TiltBrush
{
    public class ActivateLayerCommand : BaseCommand
    {
        private readonly CanvasScript m_NewActiveLayer;
        private readonly CanvasScript m_PrevActiveLayer;
        private readonly int? m_NewTrackId;
        private readonly int? m_PrevTrackId;

        public ActivateLayerCommand(CanvasScript layer, BaseCommand parent = null) : base(parent)
        {
            m_PrevActiveLayer = App.Scene.ActiveCanvas;
            m_NewActiveLayer = layer;
            var timeline = App.Scene.animationUI_manager;
            m_PrevTrackId = timeline?.GetLayerTrackId(m_PrevActiveLayer);
            m_NewTrackId = timeline?.GetLayerTrackId(m_NewActiveLayer);
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            Activate(m_NewActiveLayer, m_NewTrackId);
        }

        protected override void OnUndo()
        {
            Activate(m_PrevActiveLayer, m_PrevTrackId);
        }

        private static void Activate(CanvasScript layer, int? trackId)
        {
            if (trackId.HasValue)
            {
                layer = App.Scene.animationUI_manager?.GetTrackCanvasAtPlayhead(trackId.Value);
            }
            if (layer != null) App.Scene.ActiveCanvas = layer;
        }
    }

} // namespace TiltBrush
