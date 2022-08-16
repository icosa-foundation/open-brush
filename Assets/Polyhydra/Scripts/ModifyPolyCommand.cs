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

namespace TiltBrush
{
    public class ModifyPolyCommand : BaseCommand
    {
        private readonly EditableModelWidget m_Ewidget;
        private readonly PolyMesh m_NewPoly;
        private readonly PolyMesh m_PreviousPoly;
        private readonly PolyRecipe m_NewPolyRecipe;
        private readonly PolyRecipe m_PreviousPolyRecipe;

        public override bool NeedsSave { get { return true; } }

        public ModifyPolyCommand(EditableModelWidget ewidget, PolyMesh newPoly, PolyRecipe newPolyRecipe)
        {
            m_Ewidget = ewidget;
            m_NewPoly = newPoly;
            m_NewPolyRecipe = newPolyRecipe;
            m_PreviousPoly = ewidget.m_PolyMesh;
            m_PreviousPolyRecipe = ewidget.m_PolyRecipe;
        }

        protected override void OnRedo()
        {
            m_Ewidget.m_PolyRecipe = m_NewPolyRecipe;
            EditableModelManager.m_Instance.RegenerateMesh(m_Ewidget, m_NewPoly);
        }

        protected override void OnUndo()
        {
            m_Ewidget.m_PolyRecipe = m_PreviousPolyRecipe;
            EditableModelManager.m_Instance.RegenerateMesh(m_Ewidget, m_PreviousPoly);
        }

    }
}
