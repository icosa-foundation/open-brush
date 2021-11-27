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

namespace TiltBrush
{

    public partial class FreePaintTool
    {
        private bool m_LazyInputActive;
        private static bool m_LazyInputTangentMode;
        private bool m_showLazyInputVisuals;
        private float m_lazyInputRate;

        void UpdateLazyInputRate()
        {
            if (!m_LazyInputActive)
            {
                m_lazyInputRate = 1;
                return;
            }

            float lerpRateGoal = m_brushTriggerRatio * Time.deltaTime * Mathf.Lerp(0.2f, 5, Mathf.Pow(m_brushTriggerRatio, 5));

            // add laziness to the rate at which laziness changes!
            m_lazyInputRate = Mathf.Lerp(m_lazyInputRate, lerpRateGoal, Time.deltaTime * 0.01f);
            m_lazyInputRate = Mathf.MoveTowards(m_lazyInputRate, lerpRateGoal, Time.deltaTime * 0.01f);
        }

        private static TrTransform TangentLazyLerp(TrTransform startTx, TrTransform endTx, float lerpT)
        {
            Vector3 beeline = endTx.translation - startTx.translation;

            Vector3 startCursorNormal = startTx.rotation * Vector3.forward;
            Vector3 endCursorNormal = endTx.rotation * Vector3.forward;

            Vector3 forwardDelta = Vector3.ProjectOnPlane(beeline, startCursorNormal);
            Vector3 midPointDelta = Vector3.Project(Vector3.ProjectOnPlane(beeline, endCursorNormal), forwardDelta.normalized);

            float midPointLerp = Mathf.InverseLerp(0, beeline.magnitude, Vector3.Project(midPointDelta, beeline.normalized).magnitude);

            float beelineTangentDot = Mathf.Abs(Vector3.Dot(beeline.normalized, startCursorNormal));

            Vector3 moveDelta = Vector3.Lerp(Vector3.zero, Vector3.Lerp(midPointDelta, beeline, midPointLerp), lerpT);

            TrTransform result = TrTransform.TRS(

              startTx.translation + Vector3.Lerp(moveDelta, Vector3.zero, beelineTangentDot),

              Quaternion.Slerp(startTx.rotation, endTx.rotation, Mathf.Lerp(midPointLerp, 1, beelineTangentDot) * lerpT),

              Mathf.Lerp(startTx.scale, endTx.scale, lerpT)

            );
            return result;
        }

        private static TrTransform NormalLazyLerp(TrTransform startTx, TrTransform endTx, float lerpT)
        {
            Vector3 beeline = endTx.translation - startTx.translation;

            TrTransform result = TrTransform.TRS(

              Vector3.Lerp(startTx.translation, endTx.translation, lerpT),

              Quaternion.Slerp(startTx.rotation, endTx.rotation, lerpT),

              Mathf.Lerp(startTx.scale, endTx.scale, lerpT)

            );
            return result;
        }

        private static TrTransform LazyLerp(TrTransform startTx, TrTransform endTx, float lerpT, bool tangentMode)
        {
            if (tangentMode)
                return TangentLazyLerp(startTx, endTx, lerpT);
            else
                return NormalLazyLerp(startTx, endTx, lerpT);
        }

        void ApplyLazyInput(ref Vector3 pos, ref Quaternion rot)
        {
            if (!m_PaintingActive || !m_LazyInputActive || m_GridSnapActive)
            {

                // if (m_GridSnapActive)
                //   ApplyGridSnap(ref pos, ref rot);

                m_btCursorPos = pos;
                m_btCursorRot = rot;
                m_lazyInputRate = 0;

                EndLazyInputVisuals();
                return;
            }

            UpdateLazyInputRate();

            //Vector3 beeline = pos - m_btCursorPos;
            //
            //// Vector3 beelineDelta = Vector3.Lerp(Vector3.zero, beeline, m_lazyInputRate);
            //
            //Vector3 oldCursorNormal = m_btCursorRot * Vector3.forward;
            //Vector3 newCursorNormal = rot * Vector3.forward;
            //
            //Vector3 forwardDelta = Vector3.ProjectOnPlane(beeline, oldCursorNormal);
            //Vector3 midPointDelta = Vector3.Project(Vector3.ProjectOnPlane(beeline, newCursorNormal), forwardDelta.normalized);
            //
            //float midPointLerp = Mathf.InverseLerp(0, beeline.magnitude, Vector3.Project(midPointDelta, beeline.normalized).magnitude);
            //
            //m_btCursorRot = Quaternion.Slerp(m_btCursorRot, rot, Mathf.Lerp(midPointLerp, 1, Mathf.Abs(Vector3.Dot(beeline.normalized, newCursorNormal))) * m_lazyInputRate);
            //
            //Vector3 posDelta = Vector3.Lerp(Vector3.zero, Vector3.Lerp(midPointDelta, beeline, midPointLerp), m_lazyInputRate);
            //m_btCursorPos = m_btCursorPos + posDelta;
            //
            //// if (beelineDelta.magnitude > 0) {
            ////   m_btCursorPos = m_btCursorPos + beelineDelta;
            //// 
            ////   m_btCursorRot = Quaternion.Slerp(m_btCursorRot, rot, m_lazyInputRate);
            //// }


            TrTransform result = LazyLerp(TrTransform.TRS(m_btCursorPos, m_btCursorRot, PointerManager.m_Instance.MainPointer.BrushSizeAbsolute), TrTransform.TRS(pos, rot, PointerManager.m_Instance.MainPointer.BrushSizeAbsolute), m_lazyInputRate, m_LazyInputTangentMode);

            m_btCursorPos = result.translation;
            m_btCursorRot = result.rotation;

            pos = m_btCursorPos;
            rot = m_btCursorRot;

            UpdateLazyInputVisuals();
        }

        private void UpdateLazyInputVisuals()
        {
            BeginLazyInputVisuals();

            Transform brushAttachTransform = InputManager.m_Instance.GetBrushControllerAttachPoint();

            Vector3 cursorPos = m_btCursorPos;
            Vector3 brushPos = brushAttachTransform.position;

            cursorPos = Vector3.Lerp(brushPos, cursorPos, m_BimanualGuideLineT);

            float line_length = (cursorPos - brushPos).magnitude;
            if (line_length > 0.0f)
            {
                Vector3 brush_to_wand = (cursorPos - brushPos).normalized;
                Vector3 centerpoint = cursorPos - (cursorPos - brushPos) / 2.0f;
                transform.position = centerpoint;
                m_BimanualGuideLine.position = centerpoint;
                m_BimanualGuideLine.up = brush_to_wand;
                m_BimanualGuideLineOutline.position = centerpoint;
                m_BimanualGuideLineOutline.up = brush_to_wand;
                Vector3 temp = Vector3.one * m_BimanualGuideLineBaseWidth * m_BimanualGuideIntensity;
                temp.y = line_length / 2.0f;
                m_BimanualGuideLine.localScale = temp;
                temp.y = line_length / 2.0f + m_BimanualGuideLineOutlineWidth * Mathf.Min(1.0f, 1.0f / line_length) * m_BimanualGuideIntensity;
                temp.x += m_BimanualGuideLineOutlineWidth;
                temp.z += m_BimanualGuideLineOutlineWidth;
                m_BimanualGuideLineOutline.localScale = temp;
            }
            else
            {
                // Short term disable of line
                m_BimanualGuideLine.localScale = Vector3.zero;
                m_BimanualGuideLineOutline.localScale = Vector3.zero;
            }

            m_BimanualGuideLineRenderer.material.SetColor("_Color",
                SketchControlsScript.m_Instance.m_GrabHighlightActiveColor);

            BrushGhostTransform = TrTransform.TRS(m_btCursorPos, m_btCursorRot, PointerManager.m_Instance.MainPointer.BrushSizeAbsolute);
            BrushGhostGoal = TrTransform.TRS(brushAttachTransform.position, brushAttachTransform.rotation * sm_OrientationAdjust, PointerManager.m_Instance.MainPointer.BrushSizeAbsolute);
            BrushGhostLerpT = (BrushGhostLerpT + Time.deltaTime * 0.25f) % 1f;

        }

        private void BeginLazyInputVisuals()
        {
            if (m_showLazyInputVisuals)
                return;

            m_showLazyInputVisuals = true;
            m_BimanualGuideLineT = 1;

            m_BimanualGuideLineRenderer.material.SetFloat("_Intensity", m_BimanualGuideHintIntensity);
            m_BimanualGuideIntensity = m_BimanualGuideHintIntensity;
            m_BimanualGuideLineRenderer.enabled = true;
            m_BimanualGuideLineOutlineRenderer.enabled = true;

            BeginBrushGhosts(BrushGhost.PathModeID.Trail);
        }

        private void EndLazyInputVisuals()
        {
            if (!m_showLazyInputVisuals)
                return;

            m_showLazyInputVisuals = false;

            m_BimanualGuideLineT = 0;
            m_BimanualGuideLineDrawInTime = 0.0f;
            m_BimanualGuideLineT = 0.0f;
            m_BimanualGuideLineRenderer.enabled = false;
            m_BimanualGuideLineOutlineRenderer.enabled = false;

            EndBrushGhosts();
        }

    }
}
