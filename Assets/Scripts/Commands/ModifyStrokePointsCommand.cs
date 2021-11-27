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
    public class ModifyStrokePointsCommand : BaseCommand
    {
        private Stroke m_TargetStroke;
        private PointerManager.ControlPoint[] m_StartPoints;
        private PointerManager.ControlPoint[] m_EndPoints;

        public ModifyStrokePointsCommand(Stroke stroke, PointerManager.ControlPoint[] newControlPoints, BaseCommand parent = null) : base(parent)
        {
            m_TargetStroke = stroke;
            m_StartPoints = (PointerManager.ControlPoint[])stroke.m_ControlPoints.Clone();
            m_EndPoints = newControlPoints;
        }

        public override bool NeedsSave { get { return true; } }

        private void ApplyNewPositionsToStroke(PointerManager.ControlPoint[] points)
        {
            m_TargetStroke.m_ControlPoints = points;
            m_TargetStroke.InvalidateCopy();
            m_TargetStroke.Uncreate();
            m_TargetStroke.Recreate();
        }

        protected override void OnRedo()
        {
            ApplyNewPositionsToStroke(m_EndPoints);
        }

        protected override void OnUndo()
        {
            ApplyNewPositionsToStroke(m_StartPoints);
        }
    }
} // namespace TiltBrush
