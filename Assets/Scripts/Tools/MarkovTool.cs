using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// MarkovTool — zeichnet eine Sinuskurve entlang eines Catmull-Rom Splines.
    ///
    /// STRATEGIE:
    ///   Der Cursor bewegt sich normal mit dem Controller (GetPointerPosition nicht überschrieben).
    ///   Nach jedem Frame wird der aufgezeichnete Stroke via PointerScript.CurrentPath
    ///   mit Sine-versetzten Positionen überschrieben. Die Linie wird also zur Sinuskurve
    ///   geformt, ohne dass der Cursor zittert.
    ///
    ///   Catmull-Rom Spline liefert glatte Tangenten für die Sine-Achsen-Berechnung.
    /// </summary>
    public class MarkovTool : FreePaintTool
    {
        [Header("Sine Wave")]
        [SerializeField] private float m_SineAmplitude = 0.08f;
        [SerializeField] private float m_SineFrequency = 5f;

        // ── Runtime state ──────────────────────────────────────────────────────────

        // Akkumulierte Weglänge pro Kontrollpunkt (parallel zu CurrentPath)
        private readonly List<float> m_ArcLengths = new List<float>();

        private bool m_WasTriggerHeld;

        // ── Init / Enable ──────────────────────────────────────────────────────────

        public override void Init()
        {
            base.Init();
            m_ArcLengths.Clear();
        }

        public override void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);
            if (!bEnable)
                m_ArcLengths.Clear();
            m_WasTriggerHeld = false;
        }

        // ── UpdateTool ─────────────────────────────────────────────────────────────

        public override void UpdateTool()
        {
            bool triggerHeld = InputManager.Brush.GetCommand(InputManager.SketchCommands.Activate);
            bool triggerDown = InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Activate);
            bool triggerUp = m_WasTriggerHeld && !triggerHeld;

            if (triggerDown)
                m_ArcLengths.Clear();

            if (triggerUp)
                m_ArcLengths.Clear();

            // base läuft zuerst — danach hat PointerScript schon den neuen Punkt aufgezeichnet
            base.UpdateTool();

            // Jetzt Sine auf den aufgezeichneten Stroke anwenden
            if (triggerHeld && PointerManager.m_Instance.IsMainPointerCreatingStroke())
                ApplySineToCurrentPath();

            m_WasTriggerHeld = triggerHeld;
        }

        // ── Sine auf CurrentPath anwenden ──────────────────────────────────────────

        private void ApplySineToCurrentPath()
        {
            var pointer = PointerManager.m_Instance.MainPointer;
            var path = pointer.CurrentPath; // Liste von TrTransform (pos + rot + scale)

            int count = path.Count;
            if (count < 2) return;

            // Weglängen-Liste auf aktuelle Punktanzahl bringen
            while (m_ArcLengths.Count < count)
            {
                if (m_ArcLengths.Count == 0)
                {
                    m_ArcLengths.Add(0f);
                }
                else
                {
                    int prev = m_ArcLengths.Count - 1;
                    float dist = Vector3.Distance(path[prev].translation, path[m_ArcLengths.Count].translation);
                    m_ArcLengths.Add(m_ArcLengths[prev] + dist);
                }
            }
            // Falls Punkte entfernt wurden (ShouldCurrentLineEnd → neue Linie)
            while (m_ArcLengths.Count > count)
                m_ArcLengths.RemoveAt(m_ArcLengths.Count - 1);

            // Sine-versetzte Positionen berechnen und zurückschreiben
            var modified = new List<TrTransform>(count);
            for (int i = 0; i < count; i++)
            {
                TrTransform xf = path[i];
                Vector3 tangent = GetTangent(path, i);
                Quaternion rot = xf.rotation;
                Vector3 up = rot * Vector3.up;

                Vector3 sineAxis = Vector3.Cross(tangent, up).normalized;
                if (sineAxis.sqrMagnitude < 1e-6f)
                    sineAxis = rot * Vector3.right;

                float arc = m_ArcLengths[i];
                float sine = Mathf.Sin(arc * m_SineFrequency * 2f * Mathf.PI);
                Vector3 pos = xf.translation + sineAxis * (sine * m_SineAmplitude);

                modified.Add(TrTransform.TRS(pos, xf.rotation, xf.scale));
            }

            pointer.CurrentPath = modified;
        }

        // ── Tangente an Punkt i (Catmull-Rom / zentrale Differenz) ─────────────────

        private static Vector3 GetTangent(List<TrTransform> path, int i)
        {
            int last = path.Count - 1;
            if (last < 1) return Vector3.forward;

            if (i == 0)
                return (path[1].translation - path[0].translation).normalized;
            if (i == last)
                return (path[last].translation - path[last - 1].translation).normalized;

            // Zentrale Differenz — entspricht Catmull-Rom Tangente
            Vector3 t = (path[i + 1].translation - path[i - 1].translation);
            return t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.forward;
        }

        // ── Public Spline API (für Markov Pen System) ──────────────────────────────

        /// <summary>
        /// Gibt die rohen (nicht-Sine-versetzten) Kontrollpunkte des aktuellen Strokes zurück.
        /// </summary>
        public List<TrTransform> RawSplinePath =>
            PointerManager.m_Instance.IsMainPointerCreatingStroke()
                ? PointerManager.m_Instance.MainPointer.CurrentPath
                : new List<TrTransform>();
    }
}