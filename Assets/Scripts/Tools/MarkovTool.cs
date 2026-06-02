using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class MarkovTool : FreePaintTool
    {
        [Header("Sine Wave")]
        [SerializeField] private float m_SineAmplitude = 0.5f;
        [SerializeField] private float m_SineFrequency = 0.6f;

        private float m_StrokeArcLength;
        private Vector3 m_LastRawPos;
        private Vector3 m_TangentBasePos;
        private Vector3 m_StrokeTangent;
        private bool m_StrokeStarted;
        private readonly List<TrTransform> m_RawPath = new List<TrTransform>();
        private bool m_WasTriggerHeld;

        // Tangent is only updated after this much movement, so slow drawing doesn't
        // produce a jittery/zero tangent that makes the sine axis flip randomly.
        private const float kTangentUpdateDist = 0.02f;

        public override void Init()
        {
            base.Init();
            ResetStroke();
        }

        public override void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);
            if (!bEnable) ResetStroke();
            m_WasTriggerHeld = false;
        }

        private void ResetStroke()
        {
            m_StrokeArcLength = 0f;
            m_StrokeStarted = false;
            m_StrokeTangent = Vector3.forward;
            m_RawPath.Clear();
        }

        public override void UpdateTool()
        {
            bool triggerHeld = InputManager.Brush.GetCommand(InputManager.SketchCommands.Activate);
            bool triggerDown = InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Activate);
            bool triggerUp = m_WasTriggerHeld && !triggerHeld;

            if (triggerDown || triggerUp)
                ResetStroke();

            base.UpdateTool(); // calls PositionPointer() → GetPointerPosition()

            m_WasTriggerHeld = triggerHeld;
        }

        // Overriding GetPointerPosition ensures the sine offset is baked into the position
        // before UpdatePosition_LS builds the mesh — modifying m_ControlPoints after the fact
        // only changes bookkeeping, not the already-rendered geometry.
        protected override (Vector3, Quaternion) GetPointerPosition()
        {
            (Vector3 pos, Quaternion rot) = base.GetPointerPosition();

            if (!m_brushTrigger)
                return (pos, rot);

            if (!m_StrokeStarted)
            {
                m_LastRawPos = pos;
                m_TangentBasePos = pos;
                m_StrokeArcLength = 0f;
                m_StrokeStarted = true;
                m_StrokeTangent = rot * Vector3.forward;
                m_RawPath.Clear();
            }
            else
            {
                m_StrokeArcLength += Vector3.Distance(m_LastRawPos, pos);
                m_LastRawPos = pos;

                // Only update the tangent after meaningful movement so slow drawing
                // doesn't produce a near-zero delta that makes the sine axis flip.
                if (Vector3.Distance(m_TangentBasePos, pos) > kTangentUpdateDist)
                {
                    m_StrokeTangent = (pos - m_TangentBasePos).normalized;
                    m_TangentBasePos = pos;
                }
            }

            m_RawPath.Add(TrTransform.TR(pos, rot));

            Vector3 up = rot * Vector3.up;
            Vector3 sineAxis = Vector3.Cross(m_StrokeTangent, up).normalized;
            if (sineAxis.sqrMagnitude < 1e-6f)
                sineAxis = rot * Vector3.right;

            float sine = Mathf.Sin(m_StrokeArcLength * m_SineFrequency * 2f * Mathf.PI);
            return (pos + sineAxis * (sine * m_SineAmplitude), rot);
        }

        /// <summary>
        /// Raw (unmodified) cursor positions for the current stroke.
        /// </summary>
        public List<TrTransform> RawSplinePath =>
            PointerManager.m_Instance.IsMainPointerCreatingStroke()
                ? new List<TrTransform>(m_RawPath)
                : new List<TrTransform>();
    }
}
