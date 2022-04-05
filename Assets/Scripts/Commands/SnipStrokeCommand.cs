// Copyright 2022 The Open Brush Authors
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

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush
{
    public class SnipStrokeCommand : BaseCommand
    {
        private Stroke m_InitialStroke;
        private Stroke m_NewStroke;

        private PointerManager.ControlPoint[] m_InitialCP;
        private PointerManager.ControlPoint[] m_SnippedCP1;
        private PointerManager.ControlPoint[] m_SnippedCP2;

        private int m_SnipIndex;

        public SnipStrokeCommand(
            Stroke stroke, int snipIndex, BaseCommand parent = null) : base(parent)
        {
            m_InitialStroke = stroke;
            m_SnipIndex = snipIndex;
            m_InitialCP = (PointerManager.ControlPoint[])stroke.m_ControlPoints.Clone();
            m_NewStroke = SketchMemoryScript.m_Instance.DuplicateStroke(m_InitialStroke, App.Scene.ActiveCanvas, null);
            m_SnippedCP1 = m_InitialCP.Take(m_SnipIndex).ToArray();
            m_SnippedCP2 = m_InitialCP.Skip(m_SnipIndex).ToArray();
        }

        protected override void OnRedo()
        {
            ModifyStroke(m_InitialStroke, m_SnippedCP1);
            ModifyStroke(m_NewStroke, m_SnippedCP2);
        }

        protected override void OnUndo()
        {
            ModifyStroke(m_InitialStroke, m_InitialCP);

            switch (m_NewStroke.m_Type)
            {
                case Stroke.Type.BrushStroke:
                    BaseBrushScript brushScript = m_NewStroke.m_Object.GetComponent<BaseBrushScript>();
                    if (brushScript)
                    {
                        brushScript.HideBrush(true);
                    }
                    break;
                case Stroke.Type.BatchedBrushStroke:
                    m_NewStroke.m_BatchSubset.m_ParentBatch.DisableSubset(m_NewStroke.m_BatchSubset);
                    break;
            }
        }

        private void ModifyStroke(Stroke stroke, IEnumerable<PointerManager.ControlPoint> newControlPoints)
        {
            stroke.m_ControlPoints = newControlPoints.ToArray();
            stroke.InvalidateCopy();
            stroke.Uncreate();
            stroke.Recreate();
        }

        public override bool NeedsSave { get { return true; } }

    }
}
