// Copyright 2021 The Tilt Brush Authors
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

namespace TiltBrush
{
    public partial class FreePaintTool
    {
        private bool m_RevolverActive;
        private float m_RevolverRadius;
        private float m_RevolverAngle;
        private float m_RevolverVelocity;
        private Quaternion m_RevolverBrushRotationOffset;

        private void BeginRevolver()
        {
            if (m_RevolverActive)
                return;

            m_RevolverActive = true;
            m_RevolverAngle = 0;
            m_RevolverVelocity = 0;

            SetRevolverRadius(1);

            BeginBrushGhosts(BrushGhost.PathModeID.Orbit);
        }

        private void SetRevolverRadius(float lerpRate)
        {
            Transform brushAttachTransform = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 brushDelta = m_btIntersectGoal - brushAttachTransform.position;
            m_RevolverRadius = Mathf.Lerp(m_RevolverRadius, brushDelta.magnitude, lerpRate);

            BrushGhostOrbitalRadius = m_RevolverRadius;
        }

        private void ApplyRevolver(ref Vector3 pos, ref Quaternion rot)
        {
            if (!m_RevolverActive)
                return;

            Transform lAttachPoint = InputManager.m_Instance.GetWandControllerAttachPoint();
            Vector3 lPos = lAttachPoint.position;

            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 rPos = rAttachPoint.position;

            Vector3 guideDelta = lPos - m_btCursorPos;
            Vector3 radialDelta = Vector3.ProjectOnPlane(rPos - m_btIntersectGoal, guideDelta.normalized);

            Quaternion radialLookRot = Quaternion.LookRotation(radialDelta.normalized, guideDelta);

            m_RevolverAngle = m_RevolverAngle + m_RevolverVelocity * 720 * Time.deltaTime;

            if (m_brushTrigger)
            {
                if (InputManager.m_Instance.IsBrushScrollActive())
                {

                    float turnRate = InputManager.m_Instance.GetBrushScrollAmount();
                    // apply a cubic exponential speed curve to make joystick handling easier
                    turnRate = Mathf.Sign(turnRate) * Mathf.Pow(Mathf.Abs(turnRate), 3);

                    m_RevolverVelocity = Mathf.MoveTowards(m_RevolverVelocity, -turnRate, Time.deltaTime * (turnRate == 0 ? 0.15f : 0.333f));
                }
            }
            else
            {
                Transform brushAttachTransform = InputManager.m_Instance.GetBrushControllerAttachPoint();

                Quaternion brushWorldRotation = brushAttachTransform.rotation * sm_OrientationAdjust;

                m_RevolverBrushRotationOffset = radialLookRot.TrueInverse() * brushWorldRotation;

                BrushGhostTilt = m_RevolverBrushRotationOffset;

                if (m_RevolverVelocity == 0)
                    m_RevolverAngle = 0;
            }


            Quaternion spindleRotation = Quaternion.AngleAxis(m_RevolverAngle, guideDelta.normalized);

            if (m_brushUndoButton)
                SetRevolverRadius(m_brushTrigger ? m_lazyInputRate : 1);

            Vector3 revolverOffset = spindleRotation * radialDelta.normalized * m_RevolverRadius;
            Quaternion btCursorRotGoal = spindleRotation * radialLookRot * m_RevolverBrushRotationOffset;
            m_btCursorRot = btCursorRotGoal;

            pos = (m_brushTrigger && !m_LazyInputActive ? m_btCursorPos : m_btIntersectGoal) + revolverOffset;
            rot = m_btCursorRot;

            BrushGhostTransform = TrTransform.TRS(m_btIntersectGoal, Quaternion.LookRotation(radialDelta.normalized, guideDelta), PointerManager.m_Instance.MainPointer.BrushSizeAbsolute);
            BrushGhostLerpT = (BrushGhostLerpT + Time.deltaTime * 0.25f) % 1f;
        }
    }
}
