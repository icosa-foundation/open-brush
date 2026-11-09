// Copyright 2020 The Open Brush Authors
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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Drawing tool that synthesizes free-hand curve styles along arbitrary base paths
    /// </summary>
    public class MarkovPenTool : FreePaintTool
    {
        /// <summary>
        /// The internal MarkovPen instance that handles training and synthesis
        /// </summary>
        private static MarkovPen m_MarkovPen;

        /// <summary>
        /// The last pointer position and rotation to be returned when there is no more input
        /// </summary>
        private Tuple<Vector3, Quaternion> m_LastPointer = new(Vector3.zero, Quaternion.identity);

        /// <summary>
        /// Activate or deactivate the Markov Pen tool
        /// </summary>
        /// 
        /// <param name="isEnabled">
        /// true to activate the tool; false to deactivate it
        /// </param>
        public override void EnableTool(bool isEnabled)
        {
            base.EnableTool(isEnabled);
        }

        /// <summary>
        /// Show or hide the tool's visual indicators
        /// </summary>
        /// <param name="isHidden">
        /// true to hide the tool visuals; false to show them
        /// </param>
        public override void HideTool(bool isHidden)
        {
            base.HideTool(isHidden);
        }

        /// <summary>
        /// Read controller input and drive synthesis
        /// </summary>
        ///
        /// <remarks>
        /// Called every tool update tick.
        /// </remarks>
        public override void UpdateTool()
        {
            if (m_MarkovPen == null)
            {
                base.UpdateTool();
                return;
            }

            bool triggerDown = InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Activate);

            if (triggerDown)
            {
                m_MarkovPen.NewLine();
                (Vector3, Quaternion) start = base.GetPointerPosition();
                m_LastPointer = Tuple.Create(start.Item1, start.Item2);
            }

            List<Tuple<Vector3, Quaternion>> pointers = m_MarkovPen.Stylize(base.GetPointerPosition());

            if (pointers.Count > 0)
            {
                m_LastPointer = pointers[0];
                pointers.RemoveAt(0);
            }

            base.UpdateTool();

            PointerScript pointer = PointerManager.m_Instance.MainPointer;

            while (pointers.Count > 0)
            {
                m_LastPointer = pointers[0];


                PointerManager.m_Instance.SetPointerTransform(
                    InputManager.ControllerName.Brush,
                    m_LastPointer.Item1,
                    m_LastPointer.Item2);

                if (pointer != null && pointer.IsCreatingStroke())
                {
                    pointer.UpdateLineFromObject();
                }

                pointers.RemoveAt(0);
            }
        }

        /// <summary>
        /// Retrieve the world-space position and rotation for the brush pointer
        /// </summary>
        /// 
        /// <returns>
        /// A tuple of (position, rotation) in global coordinates
        /// </returns>
        protected override (Vector3, Quaternion) GetPointerPosition()
        {

            if (m_MarkovPen == null) return base.GetPointerPosition();

            return (m_LastPointer.Item1, m_LastPointer.Item2);
        }

        /// <summary>
        /// Create a new Markov pen
        /// </summary>
        /// <param name="basePath">
        /// Control Points of the example base path
        /// </param>
        /// <param name="styleCurve">
        /// Control points of the example style curve
        /// </param>
        public static void CreateMarkovPen(List<Vector3> basePath, List<Vector3> styleCurve)
        {
            m_MarkovPen = new MarkovPen(basePath, styleCurve);
        }
    }
}