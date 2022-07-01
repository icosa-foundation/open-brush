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
        private Dictionary<string, object> m_parameters;
        private PolyMesh m_previousPoly;

        override public bool NeedsSave
        {
            get
            {
                return true;
            }
        }

        public EditableModelAddModifierCommand(EditableModelWidget widget, Dictionary<string, object> parameters, BaseCommand parent = null) : base(parent)
        {
            m_widget = widget;
            m_parameters = parameters;
        }

        protected override void OnRedo()
        {
            var id = m_widget.GetId();
            var poly = EditableModelManager.m_Instance.GetPolyMesh(id);
            m_previousPoly = poly;
            OpParams p;
            if (m_parameters.ContainsKey("param1") && m_parameters.ContainsKey("param2"))
            {
                p = new OpParams((float)m_parameters["param1"], (float)m_parameters["param2"]);
            }
            else if (m_parameters.ContainsKey("param1"))
            {
                p = new OpParams((float)m_parameters["param1"]);
            }
            else
            {
                p = new OpParams();
            }
            poly = poly.AppyOperation((PolyMesh.Operation)m_parameters["type"], p);
            EditableModelManager.m_Instance.RecordOperation(m_widget, m_parameters);
            EditableModelManager.m_Instance.RegenerateMesh(m_widget, poly);

        }

        protected override void OnUndo()
        {
            EditableModelManager.m_Instance.RemoveLastOperation(m_widget);
            EditableModelManager.m_Instance.RegenerateMesh(m_widget, m_previousPoly);
        }
    }
} // namespace TiltBrush
