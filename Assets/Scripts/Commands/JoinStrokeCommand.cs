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
    public class JoinStrokeCommand : BaseCommand
    {
        private Stroke m_StrokeA;
        private Stroke m_StrokeB;
        private JoinStrokeType m_JoinType;

        private List<PointerManager.ControlPoint> m_InitialCP;
        private List<PointerManager.ControlPoint> m_NewCP;

        private enum JoinStrokeType
        {
            FirstFirst,
            FirstLast,
            LastFirst,
            LastLast
        }

        public JoinStrokeCommand(
            Stroke strokeA, Stroke strokeB, BaseCommand parent = null) : base(parent)
        {
            m_StrokeA = strokeA;
            m_StrokeB = strokeB;
            m_JoinType = JoinStrokeType.FirstFirst;
            m_InitialCP = strokeA.m_ControlPoints.ToList();
            m_NewCP = strokeA.m_ControlPoints.ToList();

            float prevDistance = (m_StrokeA.m_ControlPoints[0].m_Pos - strokeB.m_ControlPoints[0].m_Pos).sqrMagnitude;
            float distanceTest;

            int lastIndexA = m_StrokeA.m_ControlPoints.Length - 1;
            int lastIndexB = strokeB.m_ControlPoints.Length - 1;

            distanceTest = (m_StrokeA.m_ControlPoints[0].m_Pos - strokeB.m_ControlPoints[lastIndexB].m_Pos).sqrMagnitude;
            if (distanceTest < prevDistance)
            {
                m_JoinType = JoinStrokeType.FirstLast;
                prevDistance = distanceTest;
            }

            distanceTest = (m_StrokeA.m_ControlPoints[lastIndexA].m_Pos - strokeB.m_ControlPoints[0].m_Pos).sqrMagnitude;
            if (distanceTest < prevDistance)
            {
                m_JoinType = JoinStrokeType.LastFirst;
                prevDistance = distanceTest;
            }

            distanceTest = (m_StrokeA.m_ControlPoints[lastIndexA].m_Pos - strokeB.m_ControlPoints[lastIndexB].m_Pos).sqrMagnitude;
            if (distanceTest < prevDistance)
            {
                m_JoinType = JoinStrokeType.LastLast;
            }

            switch (m_JoinType)
            {
                case JoinStrokeType.FirstFirst:
                    m_NewCP.InsertRange(0, strokeB.m_ControlPoints.Reverse());
                    break;
                case JoinStrokeType.FirstLast:
                    m_NewCP.InsertRange(0, strokeB.m_ControlPoints);
                    break;
                case JoinStrokeType.LastFirst:
                    m_NewCP.AddRange(strokeB.m_ControlPoints);
                    break;
                case JoinStrokeType.LastLast:
                    m_NewCP.AddRange(strokeB.m_ControlPoints.Reverse());
                    break;
            }
        }

        protected override void OnRedo()
        {
            ModifyStroke(m_StrokeA, m_NewCP);
            m_StrokeB.Uncreate();
        }

        protected override void OnUndo()
        {
            ModifyStroke(m_StrokeA, m_InitialCP);
            m_StrokeB.Recreate();
        }

        private void ModifyStroke(Stroke stroke, IEnumerable<PointerManager.ControlPoint> newControlPoints)
        {
            stroke.m_ControlPoints = newControlPoints.ToArray();
            stroke.Uncreate();
            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Recreate(null, stroke.Canvas);
        }

        public override bool NeedsSave { get { return true; } }

    }
}
