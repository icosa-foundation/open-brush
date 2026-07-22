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

using Polyhydra.Core;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public class RecolorPolyCommand : BaseCommand
    {
        private readonly EditableModelWidget m_Ewidget;
        private readonly PolyMesh m_PolyMesh;
        private readonly Color[] m_NewColors;
        private readonly Color[] m_PreviousColors;
        private readonly ColorMethods m_PreviousColorMethod;

        public override bool NeedsSave { get { return true; } }

        public RecolorPolyCommand(EditableModelWidget ewidget, Color[] colors)
        {
            m_Ewidget = ewidget;
            m_PolyMesh = ewidget.m_PolyMesh;
            m_NewColors = colors;
            m_PreviousColors = (Color[])ewidget.m_PolyRecipe.Colors.Clone();
            m_PreviousColorMethod = ewidget.m_PolyRecipe.ColorMethod;
        }

        protected override void OnRedo()
        {
            m_Ewidget.m_PolyRecipe.Colors = m_NewColors;
            m_Ewidget.m_PolyRecipe.ColorMethod = ColorMethods.ByIndex;
            EditableModelManager.m_Instance.RegenerateMesh(m_Ewidget, m_PolyMesh);
        }

        protected override void OnUndo()
        {
            m_Ewidget.m_PolyRecipe.Colors = m_PreviousColors;
            m_Ewidget.m_PolyRecipe.ColorMethod = m_PreviousColorMethod;
            EditableModelManager.m_Instance.RegenerateMesh(m_Ewidget, m_PolyMesh);
        }

    }
}
