using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// MarkovTool — zeichnet eine Sinuskurve entlang eines Catmull-Rom Splines.
    ///
    /// PRO-FRAME-ARCHITEKTUR (aus PointerScript.UpdateLineFromObject() abgeleitet):
    ///   PointerScript.UpdatePointer() → UpdateLineFromObject() liest Coords.AsRoom[transform]
    ///   und fügt genau einen Kontrollpunkt pro Frame ein. SetPointerTransform() setzt diesen
    ///   Transform. Wir liefern pro Frame einen Sine-versetzten Punkt via GetPointerPosition().
    ///
    /// SINE-ERZEUGUNG:
    ///   - Die Sine-Phase wird durch die akkumulierte Weglänge der RAW Controller-Bewegung
    ///     getrieben (nicht durch Spline-Punkte) → gleichmäßige Wellen bei jeder Geschwindigkeit.
    ///   - Die Sine-Achse steht senkrecht zur Bewegungsrichtung und liegt in der Ebene
    ///     des Controller-Up-Vektors.
    ///   - Der Catmull-Rom Spline glättet die Richtungsberechnung damit die Wellen stabil bleiben.
    /// </summary>
    public class MarkovTool : FreePaintTool
    {
        [Header("Sine Wave")]
        [SerializeField] private float m_SineAmplitude = 0.05f;
        [SerializeField] private float m_SineFrequency = 4f;

        [Header("Spline Smoothing")]
        // Kleinerer Wert = mehr Kontrollpunkte = glattere Richtung
        [SerializeField] private float m_ControlPointMinDistance = 0.005f;

        // ── Runtime state ──────────────────────────────────────────────────────────

        // Rohe Controller-Positionen für Spline-Richtungsberechnung
        private readonly List<Vector3> m_ControlPoints = new List<Vector3>();
        private readonly List<Quaternion> m_ControlRotations = new List<Quaternion>();

        // Akkumulierte Weglänge der RAW Controller-Bewegung — treibt Sine-Phase
        private float m_ArcLength;

        // Letzter bekannter Controller-Pos für Weglängen-Berechnung
        private Vector3 m_LastRawPos;
        private bool m_HasLastRawPos;

        // Sine-Position für diesen Frame (von GetPointerPosition zurückgegeben)
        private Vector3? m_SinePos;
        private Quaternion m_SineRot;

        private bool m_WasTriggerHeld;

        // ── Init / Enable ──────────────────────────────────────────────────────────

        public override void Init()
        {
            base.Init();
            ResetState();
        }

        public override void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);
            if (!bEnable) ResetState();
            m_WasTriggerHeld = false;
        }

        // ── UpdateTool ─────────────────────────────────────────────────────────────

        public override void UpdateTool()
        {
            bool triggerHeld = InputManager.Brush.GetCommand(InputManager.SketchCommands.Activate);
            bool triggerDown = InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Activate);
            bool triggerUp = m_WasTriggerHeld && !triggerHeld;

            if (triggerDown)
            {
                ResetState();
                // Ersten Punkt sofort setzen
                Transform a = InputManager.m_Instance.GetBrushControllerAttachPoint();
                m_ControlPoints.Add(a.position);
                m_ControlRotations.Add(a.rotation);
                m_LastRawPos = a.position;
                m_HasLastRawPos = true;
            }

            if (triggerHeld)
            {
                Transform attach = InputManager.m_Instance.GetBrushControllerAttachPoint();
                Vector3 rawPos = attach.position;

                // Weglänge akkumulieren — JEDES Frame, unabhängig von Kontrollpunkten
                if (m_HasLastRawPos)
                    m_ArcLength += Vector3.Distance(rawPos, m_LastRawPos);
                m_LastRawPos = rawPos;
                m_HasLastRawPos = true;

                // Kontrollpunkt nur wenn weit genug bewegt (für Spline-Richtung)
                if (m_ControlPoints.Count == 0 ||
                    Vector3.Distance(rawPos, m_ControlPoints[m_ControlPoints.Count - 1])
                        >= m_ControlPointMinDistance)
                {
                    m_ControlPoints.Add(attach.position);
                    m_ControlRotations.Add(attach.rotation);
                }

                // Sine-Position für diesen Frame berechnen
                ComputeSinePosition();
            }
            else
            {
                m_SinePos = null;
            }

            if (triggerUp) ResetState();

            m_WasTriggerHeld = triggerHeld;

            // base.UpdateTool() → PositionPointer() → GetPointerPosition() (unser Override)
            base.UpdateTool();
        }

        // ── GetPointerPosition override ────────────────────────────────────────────

        protected override (Vector3, Quaternion) GetPointerPosition()
        {
            if (m_SinePos.HasValue)
                return (m_SinePos.Value, m_SineRot);

            Transform attach = InputManager.m_Instance.GetBrushControllerAttachPoint();
            return (attach.position, attach.rotation * sm_OrientationAdjust);
        }

        // ── Sine-Berechnung ────────────────────────────────────────────────────────

        private void ComputeSinePosition()
        {
            int count = m_ControlPoints.Count;
            if (count < 2)
            {
                m_SinePos = null;
                return;
            }

            // Bewegungsrichtung: aus Spline wenn möglich, sonst linear
            Vector3 currentPos;
            Vector3 tangent;
            Quaternion controllerRot;

            if (count >= 4)
            {
                int i = count - 4;
                Vector3 p0 = m_ControlPoints[i];
                Vector3 p1 = m_ControlPoints[i + 1];
                Vector3 p2 = m_ControlPoints[i + 2];
                Vector3 p3 = m_ControlPoints[i + 3];
                currentPos = CatmullRom(p0, p1, p2, p3, 1f);
                tangent = CatmullRomDerivative(p0, p1, p2, p3, 1f);
                controllerRot = m_ControlRotations[i + 3];
            }
            else
            {
                currentPos = m_ControlPoints[count - 1];
                tangent = m_ControlPoints[count - 1] - m_ControlPoints[count - 2];
                controllerRot = m_ControlRotations[count - 1];
            }

            if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.forward;
            tangent = tangent.normalized;

            // Sine-Achse senkrecht zur Bewegungsrichtung
            Vector3 up = controllerRot * Vector3.up;
            Vector3 sineAxis = Vector3.Cross(tangent, up).normalized;
            if (sineAxis.sqrMagnitude < 1e-6f)
                sineAxis = controllerRot * Vector3.right;

            // Sine-Offset — Phase aus akkumulierter Weglänge (frame-rate-unabhängig)
            float sine = Mathf.Sin(m_ArcLength * m_SineFrequency * 2f * Mathf.PI);
            m_SinePos = currentPos + sineAxis * (sine * m_SineAmplitude);
            m_SineRot = Quaternion.LookRotation(tangent, up) * sm_OrientationAdjust;
        }

        // ── State reset ────────────────────────────────────────────────────────────

        private void ResetState()
        {
            m_ControlPoints.Clear();
            m_ControlRotations.Clear();
            m_ArcLength = 0f;
            m_HasLastRawPos = false;
            m_SinePos = null;
        }

        // ── Public Spline API (für Markov Pen System) ──────────────────────────────

        public IReadOnlyList<Vector3> SplineControlPoints => m_ControlPoints;

        public Vector3 EvaluateSpline(float t)
        {
            int count = m_ControlPoints.Count;
            if (count < 4) return Vector3.zero;
            int segs = count - 3;
            float scaled = Mathf.Clamp01(t) * segs;
            int seg = Mathf.Min((int)scaled, segs - 1);
            return CatmullRom(
                m_ControlPoints[seg], m_ControlPoints[seg + 1],
                m_ControlPoints[seg + 2], m_ControlPoints[seg + 3],
                scaled - seg);
        }

        // ── Catmull-Rom ────────────────────────────────────────────────────────────

        private static Vector3 CatmullRom(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Vector3 CatmullRomDerivative(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * ((-p0 + p2) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
        }
    }
}