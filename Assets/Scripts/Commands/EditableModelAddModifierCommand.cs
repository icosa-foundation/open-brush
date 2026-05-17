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

using System.Collections.Generic;
using Polyhydra.Core;
using TiltBrush.MeshEditing;
namespace TiltBrush
{
    public class EditableModelAddModifierCommand : BaseCommand
    {

        private EditableModelWidget m_widget;
        private PreviewPolyhedron.OpDefinition m_OpDefinition;
        private PolyMesh m_previousPoly;

        override public bool NeedsSave
        {
            get
            {
                return true;
            }
        }

        public EditableModelAddModifierCommand(EditableModelWidget widget, PreviewPolyhedron.OpDefinition opDefinition, BaseCommand parent = null) : base(parent)
        {
            m_widget = widget;
            m_OpDefinition = opDefinition;
        }

        protected override void OnRedo()
        {
            var poly = m_widget.m_PolyMesh;
            m_previousPoly = poly;
            poly = PreviewPolyhedron.ApplyOp(poly, m_OpDefinition);
            EditableModelManager.m_Instance.RegenerateMesh(m_widget, poly);
        }

        protected override void OnUndo()
        {
            EditableModelManager.m_Instance.RegenerateMesh(m_widget, m_previousPoly);
        }
    }
} // namespace TiltBrush
