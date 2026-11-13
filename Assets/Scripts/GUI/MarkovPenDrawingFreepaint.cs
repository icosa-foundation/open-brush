using System.Collections.Generic;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    /// @brief Provides a Markov drawing tool that paints only on the Markov drawing panel
    /// Stores drawn points, separates base curve and style curve points, and manages
    /// pointer state while the drawing panel is active.
    public class MarkovPenDrawingFreepaint : FreePaintTool
    {
        private const float k_MinDirectionIndicatorLength = 0.001f;
        private const float k_MinDirectionIndicatorLengthSquared =
            k_MinDirectionIndicatorLength * k_MinDirectionIndicatorLength;
        private const string k_GuideLineObjectName = "GuideLine";

        [SerializeField] private LineRenderer m_DirectionIndicator;
        [SerializeField, Min(0.0f)] private float m_DirectionIndicatorPanelOffset = 0.03f;
        [SerializeField, Min(0.0f)] private float m_DirectionIndicatorControllerOffset = 0.02f;
        
        // Initial responsiveness for live style smoothing; 1 disables smoothing.
        private const float k_StyleResponsiveness = 0.5f;

        // Preserve the curve's input history between frames of the active stroke.
        private MarkovPen.Curve m_StyleSmoothingCurve;

        private static readonly List<Vector3> s_ControlPoints = new();
        private static readonly List<Vector3> s_BaseCurvePoints = new();
        private static readonly List<Vector3> s_StyleCurvePoints = new();

        private static readonly List<Vector3> s_BackupControlPoints = new();
        private static readonly List<Vector3> s_BackupBaseCurvePoints = new();
        private static readonly List<Vector3> s_BackupStyleCurvePoints = new();

        private static readonly Color s_BaseCurveColor = ParseHexColor("#ffffff");
        private static readonly Color s_StyleCurveColor = ParseHexColor("#0090da");

        private static Color s_PointerColorBeforeMarkovDrawing = Color.white;

        private static bool s_WasButtonPressed;
        private static bool s_IsWaitingForFirstTriggerRelease;
        private static bool s_IsBaseCurveDone;
        private static bool s_IsStyleCurveDone;
        private static bool s_HasBaseCurveStrokeStarted;
        private static bool s_HasStyleCurveStrokeStarted;
        private static bool s_HasSavedPointerColor;

        /// @brief Get all active points drawn on the Markov drawing panel
        public static IReadOnlyList<Vector3> ControlPoints => s_ControlPoints;

        /// @brief Get all active points belonging to the base curve
        public static List<Vector3> BaseCurvePoints => s_BaseCurvePoints;

        /// @brief Get all active points belonging to the style curve
        public static List<Vector3> StyleCurvePoints => s_StyleCurvePoints;

        /// @brief Get the backed-up drawing points from the last saved Markov drawing
        public static IReadOnlyList<Vector3> BackupControlPoints => s_BackupControlPoints;

        /// @brief Get the backed-up base curve points from the last saved Markov drawing
        public static IReadOnlyList<Vector3> BackupBaseCurvePoints => s_BackupBaseCurvePoints;

        /// @brief Get the backed-up style curve points from the last saved Markov drawing
        public static IReadOnlyList<Vector3> BackupStyleCurvePoints => s_BackupStyleCurvePoints;

        /// @brief Get whether the Markov drawing panel is currently open and available
        private static bool IsDrawingPanelOpen =>
            MarkovPenDrawingPanel.IsOpen &&
            MarkovPenDrawingPanel.Instance != null;

        /// @brief Parse an HTML hexadecimal color string
        /// @param hexColorString The hexadecimal color string, for example "#11bb72".
        /// @return The parsed color, or white if parsing fails.
        private static Color ParseHexColor(string hexColorString)
        {
            return ColorUtility.TryParseHtmlString(hexColorString, out Color parsedColor)
                ? parsedColor
                : Color.white;
        }

        /// @brief Update the direction indicator between the controller and drawing panel
        /// @param ray The ray extending from the brush controller.
        private void UpdateDirectionIndicator(Ray ray)
        {
            if (m_DirectionIndicator == null)
            {
                return;
            }

            MarkovPenDrawingPanel drawingPanel = MarkovPenDrawingPanel.Instance;

            if (drawingPanel == null ||
                !drawingPanel.TryGetClosestPanelPoint(ray, out Vector3 panelHitPoint) ||
                ray.direction.sqrMagnitude < k_MinDirectionIndicatorLengthSquared)
            {
                SetDirectionIndicatorActive(false);
                return;
            }

            Vector3 normalizedDirection = ray.direction.normalized;
            float controllerOffset = Mathf.Max(0.0f, m_DirectionIndicatorControllerOffset);
            float panelOffset = Mathf.Max(0.0f, m_DirectionIndicatorPanelOffset);

            Vector3 startPoint = ray.origin + normalizedDirection * controllerOffset;

            float hitDistance = Vector3.Distance(ray.origin, panelHitPoint);
            float lineLength = hitDistance - controllerOffset - panelOffset;

            if (lineLength <= k_MinDirectionIndicatorLength)
            {
                SetDirectionIndicatorActive(false);
                return;
            }

            Vector3 endPoint = startPoint + normalizedDirection * lineLength;

            m_DirectionIndicator.positionCount = 2;
            m_DirectionIndicator.useWorldSpace = true;
            m_DirectionIndicator.SetPosition(0, startPoint);
            m_DirectionIndicator.SetPosition(1, endPoint);

            SetDirectionIndicatorActive(true);
        }

        /// @brief Enable or disable the direction indicator
        /// @param isActive True when the direction indicator should be visible.
        private void SetDirectionIndicatorActive(bool isActive)
        {
            if (m_DirectionIndicator != null)
            {
                m_DirectionIndicator.enabled = isActive;
            }
        }

        /// @brief Update panel drawing and manage smoothing state for the style stroke.
        public override void UpdateTool()
        {
            base.UpdateTool();

            if (!IsDrawingPanelOpen)
            {
                m_StyleSmoothingCurve = null;
                SetDirectionIndicatorActive(false);
                return;
            }

            // Start each style stroke with fresh smoothing history.
            if (!s_HasStyleCurveStrokeStarted ||
                !m_brushTrigger ||
                s_IsStyleCurveDone)
            {
                m_StyleSmoothingCurve = null;
            }

            ApplyMarkovPanelPaintingOverride();
        }

        /// @brief Reset drawing state when the Markov drawing panel is opened
        /// Clears active point lists and waits for the first trigger release before drawing is allowed.
        public static void OnPanelOpened()
        {
            ClearPaintPointLists();
            ResetInteractionState(waitForFirstTriggerRelease: true);

            SetGuideLinesActive(false);
            ResetPointer();
        }

        /// @brief Copy the active point lists into the backup point lists
        public static void BackupPaintPointLists()
        {
            CopyPoints(s_ControlPoints, s_BackupControlPoints);
            CopyPoints(s_BaseCurvePoints, s_BackupBaseCurvePoints);
            CopyPoints(s_StyleCurvePoints, s_BackupStyleCurvePoints);
        }

        /// @brief Restore the active point lists from the backup point lists
        public static void RestorePaintPointListsFromBackup()
        {
            CopyPoints(s_BackupControlPoints, s_ControlPoints);
            CopyPoints(s_BackupBaseCurvePoints, s_BaseCurvePoints);
            CopyPoints(s_BackupStyleCurvePoints, s_StyleCurvePoints);
        }

        /// @brief Clear all active points stored for the Markov drawing panel
        public static void ClearPaintPointLists()
        {
            s_ControlPoints.Clear();
            s_BaseCurvePoints.Clear();
            s_StyleCurvePoints.Clear();
        }

        /// @brief Reset interaction state when the Markov drawing panel is closed
        /// Keeps backup point lists unchanged so they can be used after closing the panel.
        public static void OnPanelClosed()
        {
            RestorePointerColorIfNeeded();
            ResetInteractionState(waitForFirstTriggerRelease: false);

            SetGuideLinesActive(true);
            ResetPointer();
        }

        private static void ResetInteractionState(bool waitForFirstTriggerRelease)
        {
            s_WasButtonPressed = false;
            s_IsWaitingForFirstTriggerRelease = waitForFirstTriggerRelease;
            s_IsBaseCurveDone = false;
            s_IsStyleCurveDone = false;
            s_HasBaseCurveStrokeStarted = false;
            s_HasStyleCurveStrokeStarted = false;
        }

        private static void CopyPoints(List<Vector3> source, List<Vector3> destination)
        {
            destination.Clear();
            destination.AddRange(source);
        }

        /// @brief Enable or disable all scene guide lines
        /// @param isActive True when the guide lines should be active.
        private static void SetGuideLinesActive(bool isActive)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();

            foreach (Transform currentTransform in transforms)
            {
                if (currentTransform == null ||
                    currentTransform.name != k_GuideLineObjectName)
                {
                    continue;
                }

                GameObject guideLineObject = currentTransform.gameObject;

                if (!guideLineObject.scene.IsValid())
                {
                    continue;
                }

                guideLineObject.SetActive(isActive);
            }
        }

        /// @brief Reset pointer state and stops any active line drawing
        private static void ResetPointer()
        {
            PointerManager pointerManager = PointerManager.m_Instance;

            if (pointerManager == null)
            {
                return;
            }

            pointerManager.StraightEdgeModeEnabled = false;
            pointerManager.EnableLine(false);
            pointerManager.PointerPressure = 0.0f;
            pointerManager.EatLineEnabledInput();
        }

        /// @brief Save the pointer color before Markov drawing changes it
        private static void SavePointerColorIfNeeded()
        {
            PointerManager pointerManager = PointerManager.m_Instance;

            if (pointerManager == null || s_HasSavedPointerColor)
            {
                return;
            }

            s_PointerColorBeforeMarkovDrawing = pointerManager.PointerColor;
            s_HasSavedPointerColor = true;
        }

        /// @brief Restore the pointer color active before Markov drawing started
        private static void RestorePointerColorIfNeeded()
        {
            PointerManager pointerManager = PointerManager.m_Instance;

            if (pointerManager == null || !s_HasSavedPointerColor)
            {
                return;
            }

            pointerManager.PointerColor = s_PointerColorBeforeMarkovDrawing;
            s_HasSavedPointerColor = false;
        }

        /// @brief Set the current pointer color for Markov drawing
        /// @param color The color to apply to the pointer.
        private static void SetPointerColor(Color color)
        {
            PointerManager pointerManager = PointerManager.m_Instance;

            if (pointerManager == null)
            {
                return;
            }

            SavePointerColorIfNeeded();
            pointerManager.PointerColor = color;
        }

        /// @brief Enable or disable drawing on the pointer
        /// @param isActive True when drawing should be active.
        private void SetDrawingActive(bool isActive)
        {
            PointerManager pointerManager = PointerManager.m_Instance;

            if (pointerManager == null)
            {
                return;
            }

            pointerManager.EnableLine(isActive);
            pointerManager.PointerPressure = isActive
                ? Mathf.Clamp01(m_brushTriggerRatio)
                : 0.0f;
        }

        /// @brief Update the active curve state
        /// Handles the transition from base curve to style curve and disables drawing afterwards.
        /// @param isPaintingActive True when the user is painting on the drawing panel.
        private void UpdateCurveState(bool isPaintingActive)
        {
            PointerManager pointerManager = PointerManager.m_Instance;

            if (pointerManager == null)
            {
                return;
            }

            if (s_IsStyleCurveDone)
            {
                pointerManager.StraightEdgeModeEnabled = false;
                return;
            }

            if (!s_IsBaseCurveDone)
            {
                UpdateBaseCurveState(pointerManager, isPaintingActive);
                return;
            }

            UpdateStyleCurveState(pointerManager, isPaintingActive);
        }

        /// @brief Update the base curve stroke state
        /// @param pointerManager The active pointer manager.
        /// @param isPaintingActive True when the user is painting on the drawing panel.
        private void UpdateBaseCurveState(PointerManager pointerManager, bool isPaintingActive)
        {
            if (isPaintingActive)
            {
                s_HasBaseCurveStrokeStarted = true;

                pointerManager.StraightEdgeModeEnabled = true;
                SetPointerColor(s_BaseCurveColor);
            }

            if (s_HasBaseCurveStrokeStarted && !m_brushTrigger)
            {
                s_IsBaseCurveDone = true;
                s_HasBaseCurveStrokeStarted = false;

                pointerManager.StraightEdgeModeEnabled = false;
                pointerManager.EatLineEnabledInput();
            }

            if (s_IsBaseCurveDone)
            {
                pointerManager.StraightEdgeModeEnabled = false;
            }
        }

        /// @brief Update the style curve stroke state
        /// @param pointerManager The active pointer manager.
        /// @param isPaintingActive True when the user is painting on the drawing panel.
        private void UpdateStyleCurveState(PointerManager pointerManager, bool isPaintingActive)
        {
            pointerManager.StraightEdgeModeEnabled = false;

            if (isPaintingActive)
            {
                s_HasStyleCurveStrokeStarted = true;
                SetPointerColor(s_StyleCurveColor);
            }

            if (s_HasStyleCurveStrokeStarted && !m_brushTrigger)
            {
                FinishStyleCurve(pointerManager);
            }
        }

        /// @brief Finish the style curve stroke and restore normal pointer state
        /// @param pointerManager The active pointer manager.
        private static void FinishStyleCurve(PointerManager pointerManager)
        {
            s_IsStyleCurveDone = true;
            s_HasStyleCurveStrokeStarted = false;

            pointerManager.EnableLine(false);
            pointerManager.PointerPressure = 0.0f;
            pointerManager.EatLineEnabledInput();

            RestorePointerColorIfNeeded();
        }

        /// @brief Save a drawn point and assigns it to the active curve
        /// @param panelPoint The panel-space point drawn on the Markov drawing panel.
        private static void SavePaintPoint(Vector2 panelPoint)
        {
            Vector3 point = panelPoint;
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

        /// @brief Override normal painting behavior while the Markov drawing panel is open
        /// Redirects the brush pointer onto panel colliders, handles button interaction,
        /// and stores points while painting is active.
        private void ApplyMarkovPanelPaintingOverride()
        {
            MarkovPenDrawingPanel drawingPanel = MarkovPenDrawingPanel.Instance;
            PointerManager pointerManager = PointerManager.m_Instance;
            InputManager inputManager = InputManager.m_Instance;

            if (drawingPanel == null ||
                pointerManager == null ||
                inputManager == null)
            {
                SetDrawingActive(false);
                UpdateCurveState(false);
                return;
            }

            Transform attachTransform = inputManager.GetBrushControllerAttachPoint();

            if (attachTransform == null)
            {
                SetDrawingActive(false);
                UpdateCurveState(false);
                return;
            }

            Ray ray = new Ray(attachTransform.position, attachTransform.forward);
            UpdateDirectionIndicator(ray);

            if (s_IsWaitingForFirstTriggerRelease)
            {
                HandleInitialTriggerRelease();
                return;
            }

            Collider buttonCollider =
                drawingPanel.TryGetButtonPoint(ray, out Vector3 buttonWorldPoint);

            drawingPanel.SetHoveredButton(buttonCollider);

            if (buttonCollider != null)
            {
                HandleButtonHover(drawingPanel, pointerManager, buttonCollider, buttonWorldPoint);
                return;
            }

            if (!m_brushTrigger)
            {
                s_WasButtonPressed = false;
            }

            if (!drawingPanel.TryGetDrawingPoint(
                ray,
                out Vector2 panelPoint,
                out Vector3 drawingWorldPoint))
            {
                if (s_HasStyleCurveStrokeStarted)
                {
                    FinishStyleCurve(pointerManager);
                }

                SetDrawingActive(false);
                UpdateCurveState(false);
                return;
            }
            
            bool isPaintingActive =
                m_brushTrigger &&
                !s_IsStyleCurveDone &&
                IsApplicationPaintingAllowed() &&
                !IsViewOnlyMultiplayer();

            // Apply Elasticurve smoothing while drawing the freehand style stroke.
            if (isPaintingActive && s_IsBaseCurveDone)
            {
                if (m_StyleSmoothingCurve == null)
                {
                    m_StyleSmoothingCurve =
                        new MarkovPen.Curve(k_StyleResponsiveness);
                }

                // Feed the raw panel point into the existing smoothing algorithm.
                m_StyleSmoothingCurve.AddControlPoint(panelPoint);

                Vector3 smoothedPoint =
                    m_StyleSmoothingCurve.LatestControlPoint;

                // Rotate the panel-space correction into world space, preserving hit depth.
                Vector3 panelOffset = smoothedPoint - (Vector3)panelPoint;
                drawingWorldPoint +=
                    drawingPanel.transform.rotation * panelOffset;

                // Store the same position that the brush displays.
                panelPoint = new Vector2(smoothedPoint.x, smoothedPoint.y);
            }

            pointerManager.SetPointerTransform(
                InputManager.ControllerName.Brush,
                drawingWorldPoint,
                drawingPanel.transform.rotation);

            UpdateCurveState(isPaintingActive);
            SetDrawingActive(isPaintingActive);

            if (isPaintingActive)
            {
                SavePaintPoint(panelPoint);
            }
        }

        private void HandleInitialTriggerRelease()
        {
            SetDrawingActive(false);
            UpdateCurveState(false);

            if (m_brushTrigger)
            {
                return;
            }

            s_IsWaitingForFirstTriggerRelease = false;
            s_WasButtonPressed = false;
            ResetPointer();
        }

        private void HandleButtonHover(
            MarkovPenDrawingPanel drawingPanel,
            PointerManager pointerManager,
            Collider buttonCollider,
            Vector3 buttonWorldPoint)
        {
            pointerManager.SetPointerTransform(
                InputManager.ControllerName.Brush,
                buttonWorldPoint,
                drawingPanel.transform.rotation);

            SetDrawingActive(false);
            UpdateCurveState(false);

            if (m_brushTrigger && !s_WasButtonPressed && IsButtonPressAllowed(drawingPanel, buttonCollider))
            {
                s_WasButtonPressed = true;

                if (buttonCollider == drawingPanel.SaveButtonCollider)
                {
                    BackupPaintPointLists();
                }

                drawingPanel.OnButtonPressed(buttonCollider);
            }

            if (!m_brushTrigger)
            {
                s_WasButtonPressed = false;
            }
        }

        private static bool IsButtonPressAllowed(
            MarkovPenDrawingPanel drawingPanel,
            Collider buttonCollider)
        {
            bool isCloseButtonPressed = buttonCollider == drawingPanel.CloseButtonCollider;
            bool isSaveButtonPressed = buttonCollider == drawingPanel.SaveButtonCollider;

            return isCloseButtonPressed ||
                (isSaveButtonPressed && s_IsStyleCurveDone);
        }

        private static bool IsApplicationPaintingAllowed()
        {
            App application = App.Instance;
            return application != null && application.IsInStateThatAllowsPainting();
        }

        private static bool IsViewOnlyMultiplayer()
        {
            MultiplayerManager multiplayerManager = MultiplayerManager.m_Instance;
            return multiplayerManager != null && multiplayerManager.IsViewOnly;
        }
    }
}
