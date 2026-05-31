using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class MarkovTool : FreePaintTool
    {
        private float m_SineAmplitude = 0.05f;
        private float m_SineFrequency = 10f;
        private float m_ControlPointMinDistance = 0.02f;
        private int m_SamplesPerSegment = 20;
        private readonly List<Vector3> m_ControlPoints = new List<Vector3>();
        private readonly List<Quaternion> m_ControlRotations = new List<Quaternion>();
        private float m_SplineArcLength;
        private int m_NextSegmentToEmit;
        private Vector3? m_PendingSinePosition;
        private Quaternion m_PendingSineRotation;


        public override void Init()
        {
            base.Init();
            ResetSplineState();
        }

        public override void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);
            if(!bEnable)
                ResetSplineState();
        }
        public override void UpdateTool()
        {
            bool triggerDown = InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Activate);
            bool triggerHeld = InputManager.Brush.GetCommand(InputManager.SketchCommands.Activate);

            if(triggerDown)
                OnStrokeBegin();

            if (triggerHeld)
                OnStrokeContinue();
        }

        private void OnStrokeBegin()
        {
            ResetSplineState();
            RecordControlPoint(); // seed: need a starting point immediately
        }

        private void OnStrokeContinue()
        {
            Vector3 currentPos = InputManager.m_Instance.GetBrushControllerAttachPoint().position;
            bool hasPrev = m_ControlPoints.Count > 0;
            bool farEnough = !hasPrev ||
                Vector3.Distance(currentPos, m_ControlPoints[m_ControlPoints.Count - 1])
                >= m_ControlPointMinDistance;
            
            if(farEnough)
                RecordControlPoint();
            EmitPendingSegments(flushAll:false);
        }

        private void OnStrokeEnd()
        {
            // Flush any remaining partial segment so the stroke finishes cleanly.
            EmitPendingSegments(flushAll: true);
            ResetSplineState();
        }

        private void RecordControlPoint()
        {
            Transform attach = InputManager.m_Instance.GetBrushControllerAttachPoint();
            m_ControlPoints.Add(attach.position);
            m_ControlRotations.Add(attach.rotation);
        }

        private void ResetSplineState()
        {
            m_ControlPoints.Clear();
            m_ControlRotations.Clear();
            m_SplineArcLength = 0f;
            m_NextSegmentToEmit = 0;
            m_PendingSinePosition = null;
        }

        private void EmitPendingSegments(bool flushAll)
        {
            int count = m_ControlPoints.Count;
            if (count < 4) return; // need at least one complete Catmull-Rom segment

            int lastEmittable = flushAll ? count - 4 : count - 5;
            if (lastEmittable < 0) return;

            for (int seg = m_NextSegmentToEmit; seg <= lastEmittable; seg++)
                EmitSegmentSamples(seg);
        }

        protected override (Vector3, Quaternion) GetPointerPosition()
        {
            if (m_PendingSinePosition.HasValue)
                return (m_PendingSinePosition.Value, m_PendingSineRotation);
            Transform attach = InputManager.m_Instance.GetBrushControllerAttachPoint();
            return (attach.position, attach.rotation);
        }

        private void EmitSegmentSamples(int segIndex)
        {
            Vector3 p0 = m_ControlPoints[segIndex];
            Vector3 p1 = m_ControlPoints[segIndex + 1];
            Vector3 p2 = m_ControlPoints[segIndex + 2];
            Vector3 p3 = m_ControlPoints[segIndex + 3];
            Quaternion r1 = m_ControlRotations[segIndex + 1];
            Quaternion r2 = m_ControlRotations[segIndex + 2];

            float segmentChord = Vector3.Distance(p1, p2);
            float stepArc = segmentChord / m_SamplesPerSegment;

            Vector3 lastPos = p1;
            Quaternion lastRot = r1;

            for (int i = 0; i < m_SamplesPerSegment; i++)
            {
                float t = (float)i / (m_SamplesPerSegment - 1);

                Vector3 splinePos = CatmullRom(p0, p1, p2, p3, t);
                Vector3 splineTangent = CatmullRomDerivative(p0, p1, p2, p3, t);
                if (splineTangent == Vector3.zero)
                    splineTangent = (p2 - p1).normalized; // graceful fallback at zero-derivative

                splineTangent = splineTangent.normalized;

                // ── Stable up-vector from interpolated controller orientation ──────
                Quaternion controllerRot = Quaternion.Slerp(r1, r2, t);
                Vector3 controllerUp = controllerRot * Vector3.up;

                // ── Sine axis: perpendicular to tangent, in tangent-up plane ───────
                Vector3 sineAxis = Vector3.Cross(splineTangent, controllerUp).normalized;
                if (sineAxis == Vector3.zero)
                    sineAxis = controllerRot * Vector3.right; // fallback if tangent ∥ up

                // ── Arc-length accumulation (uniform visual frequency) ─────────────
                m_SplineArcLength += stepArc;

                // ── Sine displacement ──────────────────────────────────────────────
                float sineValue = Mathf.Sin(m_SplineArcLength * m_SineFrequency * 2f * Mathf.PI);
                Vector3 sineOffset = sineAxis * (sineValue * m_SineAmplitude);

                lastPos = splinePos + sineOffset;
                lastRot = (splineTangent != Vector3.zero)
                    ? Quaternion.LookRotation(splineTangent, controllerUp) * sm_OrientationAdjust
                    : controllerRot;
            }

            m_PendingSinePosition = lastPos;
            m_PendingSineRotation = lastRot;

            m_NextSegmentToEmit = segIndex + 1;
        }

        public IReadOnlyList<Vector3> SplineControlPoints => m_ControlPoints;

        public Vector3 EvaluateSpline(float t)
        {
            int count = m_ControlPoints.Count;
            if (count < 4) return Vector3.zero;

            int totalSegments = count - 3;
            float scaledT = Mathf.Clamp01(t) * totalSegments;
            int seg = Mathf.Min((int)scaledT, totalSegments - 1);
            float localT = scaledT - seg;

            return CatmullRom(
                m_ControlPoints[seg],
                m_ControlPoints[seg + 1],
                m_ControlPoints[seg + 2],
                m_ControlPoints[seg + 3],
                localT);
        }

        // ── Catmull-Rom mathematics ────────────────────────────────────────────────

        /// <summary>
        /// Uniform Catmull-Rom spline position.
        /// The active segment runs from p1 to p2; p0 and p3 are ghost/flanking points.
        /// </summary>
        private static Vector3 CatmullRom(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                  2f * p1
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>First derivative of the Catmull-Rom formula (tangent vector, un-normalised).</summary>
        private static Vector3 CatmullRomDerivative(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                  (-p0 + p2)
                + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t
                + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
        }
    }
}