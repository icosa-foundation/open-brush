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
        private CanvasScript m_NewActiveLayer;
        private CanvasScript m_PrevActiveLayer;

        public ActivateLayerCommand(CanvasScript layer, BaseCommand parent = null) : base(parent)
        {
            m_PrevActiveLayer = App.Scene.ActiveCanvas;
            m_NewActiveLayer = layer;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            App.Scene.ActiveCanvas = m_NewActiveLayer;
        }

        protected override void OnUndo()
        {
            App.Scene.ActiveCanvas = m_PrevActiveLayer;
        }
    }

} // namespace TiltBrush
