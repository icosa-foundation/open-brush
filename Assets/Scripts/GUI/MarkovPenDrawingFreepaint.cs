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
        public static readonly List<Vector3> s_ControlPoints = new();
        public static readonly List<Vector3> s_BaseCurvePoints = new();
        public static readonly List<Vector3> s_StyleCurvePoints = new();

        private static readonly Color s_BaseCurveColor = ParseHexColor("#11bb72");
        private static readonly Color s_StyleCurveColor = ParseHexColor("#0090da");

        private static Color s_PointerColorBeforeMarkovDrawing = Color.white;

        private static bool s_WasButtonPressed = false;
        private static bool s_WasPanelOpen = false;
        private static bool s_IsWaitingForFirstTriggerRelease = false;
        private static bool s_HasTriggerBeenReleasedWhileWaiting = false;

        private static bool s_IsBaseCurveDone = false;
        private static bool s_IsStyleCurveDone = false;

        private static bool s_HasBaseCurveStrokeStarted = false;
        private static bool s_HasStyleCurveStrokeStarted = false;
        private static bool s_HasSavedPointerColor = false;

        /// @brief Gets whether the Markov drawing panel is currently open and available.
        private static bool IsDrawingPanelOpen =>
            MarkovPenDrawingPanel.IsOpen && MarkovPenDrawingPanel.Instance != null;

        /// @brief Parses a HTML hex color string.
        /// @param hex The hex color string, for example "#11bb72".
        /// @return The parsed color, or white if parsing fails.
        private static Color ParseHexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }

            return Color.white;
        }

        /// @brief Updates the tool and redirects painting input to the Markov drawing panel when it is open.
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


            base.UpdateTool();
            if (isPanelOpen)
            {
                ApplyMarkovPanelPaintingOverride();
            }

        }
        /// @brief Resets drawing state when the Markov drawing panel is opened.
        /// Clears all saved point lists and waits for the first trigger release before drawing is allowed.
        public static void OnPanelOpened()
        {
            clearList();

            s_WasButtonPressed = false;
            s_IsWaitingForFirstTriggerRelease = true;
            s_HasTriggerBeenReleasedWhileWaiting = false;

            s_IsBaseCurveDone = false;
            s_IsStyleCurveDone = false;

            s_HasBaseCurveStrokeStarted = false;
            s_HasStyleCurveStrokeStarted = false;
            s_HasSavedPointerColor = false;

            ResetPointer();
        }

        public static void clearList()
        {
            s_ControlPoints.Clear();
            s_BaseCurvePoints.Clear();
            s_StyleCurvePoints.Clear();
        }

        /// @brief Resets interaction state when the Markov drawing panel is closed.
        /// Keeps the saved point lists unchanged so they can be used after closing the panel.
        public static void OnPanelClosed()
        {
            RestorePointerColorIfNeeded();

            s_WasButtonPressed = false;
            s_IsWaitingForFirstTriggerRelease = false;
            s_HasTriggerBeenReleasedWhileWaiting = false;

            s_IsBaseCurveDone = false;
            s_IsStyleCurveDone = false;

            s_HasBaseCurveStrokeStarted = false;
            s_HasStyleCurveStrokeStarted = false;

            ResetPointer();
        }

        /// @brief Resets pointer state and stops any active line drawing.
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

        /// @brief Saves the current pointer color before Markov drawing changes it.
        private static void SavePointerColorIfNeeded()
        {
            if (PointerManager.m_Instance == null || s_HasSavedPointerColor)
            {
                return;
            }

            s_PointerColorBeforeMarkovDrawing = PointerManager.m_Instance.PointerColor;
            s_HasSavedPointerColor = true;
        }

        /// @brief Restores the pointer color that was active before Markov drawing started.
        private static void RestorePointerColorIfNeeded()
        {
            if (PointerManager.m_Instance == null || !s_HasSavedPointerColor)
            {
                return;
            }

            PointerManager.m_Instance.PointerColor = s_PointerColorBeforeMarkovDrawing;
            s_HasSavedPointerColor = false;
        }

        /// @brief Sets the current pointer color for Markov drawing.
        /// @param color The color to apply to the pointer.
        private static void SetPointerColor(Color color)
        {
            if (PointerManager.m_Instance == null)
            {
                return;
            }

            SavePointerColorIfNeeded();
            PointerManager.m_Instance.PointerColor = color;
        }

        /// @brief Enables or disables drawing on the pointer.
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

        /// @brief Updates the active curve state.
        /// Handles the transition from base curve to style curve and disables drawing after the style curve.
        /// @param isPaintingActive Whether the user is currently painting on the drawing panel.
        private void UpdateCurveState(bool isPaintingActive)
        {
            if (PointerManager.m_Instance == null)
            {
                return;
            }

            if (s_IsStyleCurveDone)
            {
                PointerManager.m_Instance.StraightEdgeModeEnabled = false;
                return;
            }

            if (!s_IsBaseCurveDone)
            {
                UpdateBaseCurveState(isPaintingActive);
                return;
            }

            UpdateStyleCurveState(isPaintingActive);
        }

        /// @brief Updates the base curve stroke state.
        /// Keeps straight edge mode enabled while the base curve is being drawn.
        /// @param isPaintingActive Whether the user is currently painting on the drawing panel.
        private void UpdateBaseCurveState(bool isPaintingActive)
        {
            if (isPaintingActive)
            {
                s_HasBaseCurveStrokeStarted = true;

                PointerManager.m_Instance.StraightEdgeModeEnabled = true;
                SetPointerColor(s_BaseCurveColor);
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

        /// @brief Updates the style curve stroke state.
        /// Disables further drawing after the style curve trigger is released.
        /// @param isPaintingActive Whether the user is currently painting on the drawing panel.
        private void UpdateStyleCurveState(bool isPaintingActive)
        {
            PointerManager.m_Instance.StraightEdgeModeEnabled = false;

            if (isPaintingActive)
            {
                s_HasStyleCurveStrokeStarted = true;
                SetPointerColor(s_StyleCurveColor);
            }

            if (s_HasStyleCurveStrokeStarted && !m_brushTrigger)
            {
                s_IsStyleCurveDone = true;
                s_HasStyleCurveStrokeStarted = false;

                PointerManager.m_Instance.EnableLine(false);
                PointerManager.m_Instance.PointerPressure = 0f;
                PointerManager.m_Instance.EatLineEnabledInput();

                RestorePointerColorIfNeeded();
            }
        }

        /// @brief Saves a drawn point and assigns it to the base curve or style curve.
        /// @param point The world-space point drawn on the Markov drawing panel.
        private void SavePaintPoint(Vector3 point)
        {
            s_ControlPoints.Add(point);

            if (!s_IsBaseCurveDone)
            {
                s_BaseCurvePoints.Add(point);
                return;
            }

            if (!s_IsStyleCurveDone)
            {
                s_StyleCurvePoints.Add(point);
            }
        }

        /// @brief Overrides normal painting behavior while the Markov drawing panel is open.
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
                UpdateCurveState(false);
                return;
            }

            Ray ray = new Ray(attach.position, attach.forward);


            if (s_IsWaitingForFirstTriggerRelease)
            {
                SetDrawingActive(false);
                UpdateCurveState(false);

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
            Collider btn = MarkovPenDrawingPanel.Instance.TryGetButtonPoint(ray, out Vector3 buttonWorldPoint);
            if (btn != null)
            {
                PointerManager.m_Instance.SetPointerTransform(
                    InputManager.ControllerName.Brush,
                    buttonWorldPoint,
                    MarkovPenDrawingPanel.Instance.transform.rotation);

                SetDrawingActive(false);
                UpdateCurveState(false);

                if (m_brushTrigger &&
                    !s_WasButtonPressed &&
                    (s_IsStyleCurveDone || btn.Equals(MarkovPenDrawingPanel.Instance.CloseeButtonCollider)))
                {
                    Debug.LogError(btn.name);
                    s_WasButtonPressed = true;
                    MarkovPenDrawingPanel.Instance.OnButtonPressed(btn);
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
                UpdateCurveState(false);
                return;
            }

            PointerManager.m_Instance.SetPointerTransform(
                InputManager.ControllerName.Brush,
                drawingWorldPoint,
                MarkovPenDrawingPanel.Instance.transform.rotation);

            bool isPaintingActive =
                m_brushTrigger &&
                !s_IsStyleCurveDone &&
                App.Instance.IsInStateThatAllowsPainting() &&
                !MultiplayerManager.m_Instance.IsViewOnly;
            UpdateCurveState(isPaintingActive);
            SetDrawingActive(isPaintingActive);

            if (isPaintingActive)
            {
                SavePaintPoint(drawingWorldPoint);
            }
        }
    }


}
