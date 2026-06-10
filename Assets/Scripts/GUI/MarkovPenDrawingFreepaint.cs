using System.Collections.Generic;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    /// @brief Provides a Markov drawing tool that paints only on the Markov drawing panel.
    /// Stores all drawn points, separates the base curve from style curve points, and manages
    /// pointer state while the drawing panel is active.
    class MarkovPenDrawingFreepaint : FreePaintTool
    {
        public static List<Vector3> s_ControlPoints = new();
        public static List<Vector3> s_BaseCurvePoints = new();
        public static List<Vector3> s_StyleCurvePoints = new();

        private static bool s_WasButtonPressed = false;
        private static bool s_WasPanelOpen = false;
        private static bool s_IsWaitingForFirstTriggerRelease = false;
        private static bool s_HasTriggerBeenReleasedWhileWaiting = false;
        private static bool s_IsBaseCurveDone = false;
        private static bool s_HasBaseCurveStrokeStarted = false;

        /// @brief Gets whether the Markov drawing panel is currently open and available.
        private static bool IsDrawingPanelOpen =>
            MarkovPenDrawingPanel.IsOpen && MarkovPenDrawingPanel.Instance != null;

        /// @brief Update the tool and redirect painting input to the Markov drawing panel when it is open.
        public override void UpdateTool()
        {
            bool isPanelOpen = IsDrawingPanelOpen;

            if (isPanelOpen && !s_WasPanelOpen)
            {
                OnPanelOpened();
            }
            else if (!isPanelOpen && s_WasPanelOpen)
            {
                OnPanelClosed();
            }

            s_WasPanelOpen = isPanelOpen;

            if (isPanelOpen)
            {
                base.UpdateTool();
                ApplyMarkovPanelPaintingOverride();
                base.UpdateTool();
                ApplyMarkovPanelPaintingOverride();
                return;
            }

            base.UpdateTool();

        }

        /// @brief Reset drawing state when the Markov drawing panel is opened.
        /// Clears all saved point lists and waits for the first trigger release before drawing is allowed.
        public static void OnPanelOpened()
        {
            s_ControlPoints.Clear();
            s_BaseCurvePoints.Clear();
            s_StyleCurvePoints.Clear();

            s_WasButtonPressed = false;
            s_IsBaseCurveDone = false;
            s_HasBaseCurveStrokeStarted = false;
            s_IsWaitingForFirstTriggerRelease = true;
            s_HasTriggerBeenReleasedWhileWaiting = false;

            ResetPointer();
        }

        /// @brief Reset interaction state when the Markov drawing panel is closed.
        /// Keeps the saved point lists unchanged so they can be used after closing the panel.
        public static void OnPanelClosed()
        {
            s_WasButtonPressed = false;
            s_IsWaitingForFirstTriggerRelease = false;
            s_HasTriggerBeenReleasedWhileWaiting = false;
            s_IsBaseCurveDone = false;
            s_HasBaseCurveStrokeStarted = false;

            ResetPointer();
        }

        /// @brief Reset pointer state and stop any active line drawing.
        private static void ResetPointer()
        {
            if (PointerManager.m_Instance == null)
            {
                return;
            }

            PointerManager.m_Instance.StraightEdgeModeEnabled = false;
            PointerManager.m_Instance.EnableLine(false);
            PointerManager.m_Instance.PointerPressure = 0f;
            PointerManager.m_Instance.EatLineEnabledInput();
        }

        /// @brief Enable or disable drawing on the pointer.
        /// @param isActive Whether drawing should be active.
        private void SetDrawingActive(bool isActive)
        {
            if (PointerManager.m_Instance == null)
            {
                return;
            }

            PointerManager.m_Instance.EnableLine(isActive);
            PointerManager.m_Instance.PointerPressure = isActive ? m_brushTriggerRatio : 0f;
        }

        /// @brief Update whether the current stroke belongs to the base curve or style curve.
        /// Keeps straight edge mode enabled while the base curve is being drawn and disables it after release.
        /// @param isPaintingActive Whether the user is currently painting on the drawing panel.
        private void UpdateBaseCurveState(bool isPaintingActive)
        {
            if (PointerManager.m_Instance == null)
            {
                return;
            }

            if (!s_IsBaseCurveDone && isPaintingActive)
            {
                s_HasBaseCurveStrokeStarted = true;
                PointerManager.m_Instance.StraightEdgeModeEnabled = true;
            }

            if (s_HasBaseCurveStrokeStarted && !m_brushTrigger)
            {
                s_IsBaseCurveDone = true;
                s_HasBaseCurveStrokeStarted = false;
                PointerManager.m_Instance.StraightEdgeModeEnabled = false;
                PointerManager.m_Instance.EatLineEnabledInput();
            }

            if (s_IsBaseCurveDone)
            {
                PointerManager.m_Instance.StraightEdgeModeEnabled = false;
            }
        }

        /// @brief Save a drawn point and assign it to the base curve or style curve.
        /// @param point The world-space point drawn on the Markov drawing panel.
        private void SavePaintPoint(Vector3 point)
        {
            s_ControlPoints.Add(point);

            if (!s_IsBaseCurveDone)
            {
                s_BaseCurvePoints.Add(point);
            }
            else
            {
                s_StyleCurvePoints.Add(point);
            }
        }

        /// @brief Override normal painting behavior while the Markov drawing panel is open.
        /// Redirects the brush pointer onto the panel colliders, handles button interaction,
        /// and stores points while painting is active.
        private void ApplyMarkovPanelPaintingOverride()
        {
            if (!IsDrawingPanelOpen)
            {
                return;
            }

            Transform attach = InputManager.m_Instance.GetBrushControllerAttachPoint();

            if (attach == null)
            {
                SetDrawingActive(false);
                UpdateBaseCurveState(false);
                return;
            }

            Ray ray = new Ray(attach.position, attach.forward);

            if (s_IsWaitingForFirstTriggerRelease)
            {
                SetDrawingActive(false);
                UpdateBaseCurveState(false);

                if (!m_brushTrigger)
                {
                    s_HasTriggerBeenReleasedWhileWaiting = true;
                }

                if (s_HasTriggerBeenReleasedWhileWaiting && !m_brushTrigger)
                {
                    s_IsWaitingForFirstTriggerRelease = false;
                    s_HasTriggerBeenReleasedWhileWaiting = false;
                    s_WasButtonPressed = false;
                    ResetPointer();
                }

                return;
            }

            if (MarkovPenDrawingPanel.Instance.TryGetButtonPoint(ray, out Vector3 buttonWorldPoint))
            {
                PointerManager.m_Instance.SetPointerTransform(
                    InputManager.ControllerName.Brush,
                    buttonWorldPoint,
                    MarkovPenDrawingPanel.Instance.transform.rotation);

                SetDrawingActive(false);
                UpdateBaseCurveState(false);

                if (m_brushTrigger && !s_WasButtonPressed && s_StyleCurvePoints.Count > 0)
                {
                    s_WasButtonPressed = true;
                    MarkovPenDrawingPanel.Instance.OnButtonPressed();
                }

                if (!m_brushTrigger)
                {
                    s_WasButtonPressed = false;
                }

                return;
            }

            if (!m_brushTrigger)
            {
                s_WasButtonPressed = false;
            }

            if (!MarkovPenDrawingPanel.Instance.TryGetDrawingPoint(
                ray,
                out _,
                out Vector3 drawingWorldPoint))
            {
                SetDrawingActive(false);
                UpdateBaseCurveState(false);
                return;
            }

            PointerManager.m_Instance.SetPointerTransform(
                InputManager.ControllerName.Brush,
                drawingWorldPoint,
                MarkovPenDrawingPanel.Instance.transform.rotation);

            bool isPaintingActive =
                m_brushTrigger &&
                App.Instance.IsInStateThatAllowsPainting() &&
                !MultiplayerManager.m_Instance.IsViewOnly;

            UpdateBaseCurveState(isPaintingActive);
            SetDrawingActive(isPaintingActive);

            if (isPaintingActive)
            {
                SavePaintPoint(drawingWorldPoint);
            }
        }
    }
}
