using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// Brush tool that fits a Catmull-Rom spline to the user's stroke in real time
    /// and synthesizes a style curve along it via ComputeStyleOffset.
    /// The offset method is intentionally isolated so it can be replaced with
    /// DCMM-based Markov sampling (Lang & Alexa 2016) in a later iteration.
    public class MarkovTool : FreePaintTool
    {
        [Header("Sine Wave")]
        [SerializeField] private float m_SineAmplitude = 0.5f;
        [SerializeField] private float m_SineFrequency = 0.6f;

        [Header("Spline Debug")]
        [SerializeField] private bool m_ShowSpline = false;
        [SerializeField] private LineRenderer m_SplineDebugRenderer;

        private readonly List<Vector3> m_ControlPoints = new();
        private float m_SplineArcLength;
        private Vector3 m_LastPos;
        private bool m_StrokeStarted;
        private bool m_WasTriggerHeld;

        // Minimum distance between committed control points to avoid near-zero tangents.
        private const float kMinPointSpacing = 0.015f;
        // Line renderer samples per spline segment used in debug mode.
        private const int kDebugSamplesPerSegment = 12;

        public override void Init()
        {
            base.Init();
            ResetStroke();
        }

        /// Enables or disables the tool. Resets the current stroke on disable.
        /// @param bEnable true to activate the tool, false to deactivate it.
        public override void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);
            if (!bEnable) ResetStroke();
            m_WasTriggerHeld = false;
        }

        /// Resets all per-stroke state to initial values.
        private void ResetStroke()
        {
            m_ControlPoints.Clear();
            m_SplineArcLength = 0f;
            m_StrokeStarted = false;
            RefreshDebugRenderer();
        }

        /// Processes trigger input and forwards the per-frame update.
        /// Resets the stroke on trigger-down and trigger-up.
        public override void UpdateTool()
        {
            bool triggerHeld = InputManager.Brush.GetCommand(InputManager.SketchCommands.Activate);
            bool triggerDown = InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Activate);
            bool triggerUp = m_WasTriggerHeld && !triggerHeld;

            if (triggerDown || triggerUp)
                ResetStroke();

            base.UpdateTool();

            m_WasTriggerHeld = triggerHeld;
        }

        /// Builds the Catmull-Rom base spline from the raw pointer path and displaces
        /// each position by the offset returned from ComputeStyleOffset.
        /// @returns Style-displaced position with unchanged rotation.
        protected override (Vector3, Quaternion) GetPointerPosition()
        {
            (Vector3 pos, Quaternion rot) = base.GetPointerPosition();

            if (!m_brushTrigger)
                return (pos, rot);

            if (!m_StrokeStarted)
            {
                m_ControlPoints.Clear();
                m_SplineArcLength = 0f;
                m_LastPos = pos;
                m_StrokeStarted = true;
            }

            // Commit a new control point once the pointer has moved far enough.
            if (m_ControlPoints.Count == 0 ||
                Vector3.Distance(m_ControlPoints[m_ControlPoints.Count - 1], pos) >= kMinPointSpacing)
            {
                m_ControlPoints.Add(pos);
                RefreshDebugRenderer();
            }

            m_SplineArcLength += Vector3.Distance(m_LastPos, pos);
            m_LastPos = pos;

            Vector3 tangent = SplineTangentAtTip(pos, rot);
            Vector3 offset = ComputeStyleOffset(m_SplineArcLength, tangent, rot);

            return (pos + offset, rot);
        }

        /// Represents the Style Curve currently Sine Function
        /// Returns the style offset for the current position along the base spline.
        /// Currently a sine wave acting as a placeholder for the full Markov synthesis.
        /// Replace this method with DCMM transition sampling once the model is trained.
        /// @param arcLength Accumulated arc length along the base spline.
        /// @param tangent Normalised tangent of the base spline at the current position.
        /// @param rot Controller rotation used as a fallback for the offset axis.
        /// @returns Offset vector to add to the raw base spline position.
        private Vector3 ComputeStyleOffset(float arcLength, Vector3 tangent, Quaternion rot)
        {
            Vector3 up = rot * Vector3.up;
            Vector3 offsetAxis = Vector3.Cross(tangent, up).normalized;
            if (offsetAxis.sqrMagnitude < 1e-6f)
                offsetAxis = rot * Vector3.right;

            float sine = Mathf.Sin(arcLength * m_SineFrequency * 2f * Mathf.PI);
            return offsetAxis * (sine * m_SineAmplitude);
        }

        /// Computes a smooth tangent at the current stroke tip using the last committed
        /// control point and the raw pointer position as a lookahead handle.
        /// @param pos Current raw pointer position.
        /// @param rot Controller rotation used as a fallback direction.
        /// @returns Normalised tangent at the current stroke tip.
        private Vector3 SplineTangentAtTip(Vector3 pos, Quaternion rot)
        {
            int n = m_ControlPoints.Count;
            if (n < 2) return rot * Vector3.forward;

            Vector3 diff = pos - m_ControlPoints[n - 2];
            return diff.sqrMagnitude > 1e-6f ? diff.normalized : rot * Vector3.forward;
        }

        /// Evaluates a point on a Catmull-Rom spline segment.
        /// @param p0 Control point before the segment start.
        /// @param p1 Segment start.
        /// @param p2 Segment end.
        /// @param p3 Control point after the segment end.
        /// @param t Interpolation parameter in [0, 1].
        /// @returns Interpolated position on the spline segment.
        private static Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        // Rebuilds the debug line renderer from all committed Catmull-Rom control points.
        private void RefreshDebugRenderer()
        {
            if (m_SplineDebugRenderer == null) return;
            m_SplineDebugRenderer.enabled = m_ShowSpline;

            if (!m_ShowSpline || m_ControlPoints.Count < 4)
            {
                m_SplineDebugRenderer.positionCount = 0;
                return;
            }

            var pts = new List<Vector3>();
            for (int i = 0; i <= m_ControlPoints.Count - 4; i++)
            {
                for (int s = 0; s < kDebugSamplesPerSegment; s++)
                {
                    float t = s / (float)kDebugSamplesPerSegment;
                    pts.Add(CatmullRomPoint(
                        m_ControlPoints[i],
                        m_ControlPoints[i + 1],
                        m_ControlPoints[i + 2],
                        m_ControlPoints[i + 3],
                        t));
                }
            }

            m_SplineDebugRenderer.positionCount = pts.Count;
            m_SplineDebugRenderer.SetPositions(pts.ToArray());
        }

        /// Read-only view of the committed Catmull-Rom control points for the current stroke.
        public IReadOnlyList<Vector3> SplineControlPoints => m_ControlPoints;
    }
}
