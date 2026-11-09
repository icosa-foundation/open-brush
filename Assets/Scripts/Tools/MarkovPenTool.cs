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
    /// @brief Drawing tool that synthesizes free-hand curve styles along arbitrary base paths
    ///        Until the Markov synthesis is implemented every method delegates to FreePaintTool,
    ///        so the tool behaves identically to plain free-hand drawing.
    /// @note in future versions it will be using a Markov Chain or an autoregressive hidden Markov Model
    /// 
    public class MarkovPenTool : FreePaintTool
    {
        private static MarkovPen m_MarkovPen;

        private Tuple<Vector3, Quaternion> m_LastPointer = new(Vector3.zero, Quaternion.identity);

        /// @brief Activate or deactivate the Markov Pen tool
        /// @param isEnabled true to activate the tool; false to deactivate it.
        public override void EnableTool(bool isEnabled)
        {
            base.EnableTool(isEnabled);
        }

        /// @brief Show or hide the tool's visual indicators
        /// @param isHidden true to hide the tool visuals; false to show them.
        /// @note  Later: indicates Up Vector? adds visual indicator for controller position
        public override void HideTool(bool isHidden)
        {
            base.HideTool(isHidden);
        }

        /// @brief Read controller input and drive synthesis
        ///
        /// Called every tool update tick.
        /// @note  Later: sample the target base path B from the controller position each frame,
        ///        run one step of DCMM synthesis to produce the next
        ///        point on B', and feed the result into the pointer manager.
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

        /// @brief Return the world-space position and rotation for the brush pointer
        /// @returns A tuple of (position, rotation) in global space.
        protected override (Vector3, Quaternion) GetPointerPosition()
        {

            if (m_MarkovPen == null) return base.GetPointerPosition();

            return (m_LastPointer.Item1, m_LastPointer.Item2);
        }

        /// @brief Create a new Markov pen
        /// 
        /// @param basePath - Control Points of the given example Base Path
        /// @param styleCurve - Control points of the given example Style Curve
        public static void CreateMarkovPen(List<Vector3> basePath, List<Vector3> styleCurve)
        {
            m_MarkovPen = new MarkovPen(basePath, styleCurve);
        }
    }
}