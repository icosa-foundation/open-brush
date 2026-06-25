using System.Collections.Generic;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    /// @brief Provides a Markov drawing tool that paints only on the Markov drawing panel.
    /// Stores drawn points, separates base curve and style curve points, and manages
    /// pointer state while the drawing panel is active.
    public class MarkovPenDrawingFreepaint : FreePaintTool
    {

        private const float k_MinDirectionIndicatorLength = 0.001f;
        private const string k_GuideLineObjectName = "GuideLine";

        [SerializeField] private LineRenderer m_DirectionIndicator;
        [SerializeField] private float m_DirectionIndicatorPanelOffset = 0.03f;
        [SerializeField] private float m_DirectionIndicatorControllerOffset = 0.02f;

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

        /// @brief Gets all active points drawn on the Markov drawing panel.
        public static IReadOnlyList<Vector3> ControlPoints => s_ControlPoints;

        /// @brief Gets all active points belonging to the base curve.
        public static IReadOnlyList<Vector3> BaseCurvePoints => s_BaseCurvePoints;

        /// @brief Gets all active points belonging to the style curve.
        public static IReadOnlyList<Vector3> StyleCurvePoints => s_StyleCurvePoints;

        /// @brief Gets the backed-up drawing points from the last saved Markov drawing.
        public static IReadOnlyList<Vector3> BackupControlPoints => s_BackupControlPoints;

        /// @brief Gets the backed-up base curve points from the last saved Markov drawing.
        public static IReadOnlyList<Vector3> BackupBaseCurvePoints => s_BackupBaseCurvePoints;

        /// @brief Gets the backed-up style curve points from the last saved Markov drawing.
        public static IReadOnlyList<Vector3> BackupStyleCurvePoints => s_BackupStyleCurvePoints;

        /// @brief Gets whether the Markov drawing panel is currently open and available.
        private static bool IsDrawingPanelOpen =>
            MarkovPenDrawingPanel.IsOpen &&
            MarkovPenDrawingPanel.Instance != null;

        /// @brief Parses an HTML hexadecimal color string.
        /// @param hexColorString The hexadecimal color string, for example "#11bb72".
        /// @return The parsed color, or white if parsing fails.
        private static Color ParseHexColor(string hexColorString)
        {
            if (ColorUtility.TryParseHtmlString(hexColorString, out Color parsedColor))
            {
                return parsedColor;
            }

            return Color.white;
        }

        /// @brief Updates the direction indicator between the controller and drawing panel.
        /// @param ray The ray extending from the brush controller.
        private void UpdateDirectionIndicator(Ray ray)
        {
            if (m_DirectionIndicator == null)
            {
                return;
            }

            MarkovPenDrawingPanel drawingPanel = MarkovPenDrawingPanel.Instance;

            if (drawingPanel == null ||
                !drawingPanel.TryGetClosestPanelPoint(ray, out Vector3 panelHitPoint))
            {
                SetDirectionIndicatorActive(false);
                return;
            }

            if (ray.direction.sqrMagnitude < k_MinDirectionIndicatorLength)
            {
                SetDirectionIndicatorActive(false);
                return;
            }

            Vector3 normalizedDirection = ray.direction.normalized;
            Vector3 startPoint =
                ray.origin + normalizedDirection * m_DirectionIndicatorControllerOffset;

            float hitDistance = Vector3.Distance(ray.origin, panelHitPoint);
            float lineLength = Mathf.Max(
                0.0f,
                hitDistance -
                m_DirectionIndicatorControllerOffset -
                m_DirectionIndicatorPanelOffset);

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

        /// @brief Enables or disables the direction indicator.
        /// @param isActive True when the direction indicator should be visible.
        private void SetDirectionIndicatorActive(bool isActive)
        {
            if (m_DirectionIndicator != null)
            {
                m_DirectionIndicator.enabled = isActive;
            }
        }

        /// @brief Updates the tool and redirects painting input to the Markov drawing panel.
        public override void UpdateTool()
        {
            bool isPanelOpen = IsDrawingPanelOpen;

            base.UpdateTool();

            if (!isPanelOpen)
            {
                SetDirectionIndicatorActive(false);
                return;
            }

            ApplyMarkovPanelPaintingOverride();
        }

        /// @brief Resets drawing state when the Markov drawing panel is opened.
        /// Clears active point lists and waits for the first trigger release before drawing is allowed.
        public static void OnPanelOpened()
        {
            ClearPaintPointLists();

            s_WasButtonPressed = false;
            s_IsWaitingForFirstTriggerRelease = true;
            s_IsBaseCurveDone = false;
            s_IsStyleCurveDone = false;
            s_HasBaseCurveStrokeStarted = false;
            s_HasStyleCurveStrokeStarted = false;
            s_HasSavedPointerColor = false;

            SetGuideLinesActive(false);
            ResetPointer();
        }

        /// @brief Copies the active point lists into the backup point lists.
        public static void BackupPaintPointLists()
        {
            s_BackupControlPoints.Clear();
            s_BackupBaseCurvePoints.Clear();
            s_BackupStyleCurvePoints.Clear();

            s_BackupControlPoints.AddRange(s_ControlPoints);
            s_BackupBaseCurvePoints.AddRange(s_BaseCurvePoints);
            s_BackupStyleCurvePoints.AddRange(s_StyleCurvePoints);
        }

        /// @brief Restores the active point lists from the backup point lists.
        public static void RestorePaintPointListsFromBackup()
        {
            ClearPaintPointLists();

            s_ControlPoints.AddRange(s_BackupControlPoints);
            s_BaseCurvePoints.AddRange(s_BackupBaseCurvePoints);
            s_StyleCurvePoints.AddRange(s_BackupStyleCurvePoints);
        }

        /// @brief Clears all active points stored for the Markov drawing panel.
        public static void ClearPaintPointLists()
        {
            s_ControlPoints.Clear();
            s_BaseCurvePoints.Clear();
            s_StyleCurvePoints.Clear();
        }

        /// @brief Resets interaction state when the Markov drawing panel is closed.
        /// Keeps backup point lists unchanged so they can be used after closing the panel.
        public static void OnPanelClosed()
        {
            RestorePointerColorIfNeeded();

            s_WasButtonPressed = false;
            s_IsWaitingForFirstTriggerRelease = false;
            s_IsBaseCurveDone = false;
            s_IsStyleCurveDone = false;
            s_HasBaseCurveStrokeStarted = false;
            s_HasStyleCurveStrokeStarted = false;

            SetGuideLinesActive(true);
            ResetPointer();

        }

        /// @brief Enables or disables all scene guide lines.
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

        /// @brief Resets pointer state and stops any active line drawing.
        private static void ResetPointer()
        {
            if (PointerManager.m_Instance == null)
            {
                return;
            }

            PointerManager.m_Instance.StraightEdgeModeEnabled = false;
            PointerManager.m_Instance.EnableLine(false);
            PointerManager.m_Instance.PointerPressure = 0.0f;
            PointerManager.m_Instance.EatLineEnabledInput();
        }

        /// @brief Saves the pointer color before Markov drawing changes it.
        private static void SavePointerColorIfNeeded()
        {
            if (PointerManager.m_Instance == null || s_HasSavedPointerColor)
            {
                return;
            }

            s_PointerColorBeforeMarkovDrawing = PointerManager.m_Instance.PointerColor;
            s_HasSavedPointerColor = true;
        }

        /// @brief Restores the pointer color active before Markov drawing started.
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
        /// @param isActive True when drawing should be active.
        private void SetDrawingActive(bool isActive)
        {
            if (PointerManager.m_Instance == null)
            {
                return;
            }

            PointerManager.m_Instance.EnableLine(isActive);
            PointerManager.m_Instance.PointerPressure =
                isActive ? m_brushTriggerRatio : 0.0f;
        }

        /// @brief Updates the active curve state.
        /// Handles the transition from base curve to style curve and disables drawing afterwards.
        /// @param isPaintingActive True when the user is painting on the drawing panel.
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
        /// @param isPaintingActive True when the user is painting on the drawing panel.
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
        /// @param isPaintingActive True when the user is painting on the drawing panel.
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
                PointerManager.m_Instance.PointerPressure = 0.0f;
                PointerManager.m_Instance.EatLineEnabledInput();

                RestorePointerColorIfNeeded();
            }
        }

        /// @brief Saves a drawn point and assigns it to the active curve.
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

            Transform attachTransform =
                inputManager.GetBrushControllerAttachPoint();

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
                SetDrawingActive(false);
                UpdateCurveState(false);

                if (!m_brushTrigger)
                {
                    s_IsWaitingForFirstTriggerRelease = false;
                    s_WasButtonPressed = false;
                    ResetPointer();
                }

                return;
            }

            Collider buttonCollider =
                drawingPanel.TryGetButtonPoint(ray, out Vector3 buttonWorldPoint);

            drawingPanel.SetHoveredButton(buttonCollider);

            if (buttonCollider != null)
            {
                pointerManager.SetPointerTransform(
                    InputManager.ControllerName.Brush,
                    buttonWorldPoint,
                    drawingPanel.transform.rotation);

                SetDrawingActive(false);
                UpdateCurveState(false);

                bool isCloseButtonPressed =
                    buttonCollider == drawingPanel.CloseButtonCollider;

                bool isSaveButtonPressed =
                    buttonCollider == drawingPanel.SaveButtonCollider;

                bool isButtonPressAllowed =
                    isCloseButtonPressed ||
                    (isSaveButtonPressed && s_IsStyleCurveDone);

                if (m_brushTrigger &&
                    !s_WasButtonPressed &&
                    isButtonPressAllowed)
                {
                    s_WasButtonPressed = true;

                    if (isSaveButtonPressed)
                    {
                        BackupPaintPointLists();
                    }

                    drawingPanel.OnButtonPressed(buttonCollider);
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

            if (!drawingPanel.TryGetDrawingPoint(
                ray,
                out _,
                out Vector3 drawingWorldPoint))
            {
                SetDrawingActive(false);
                UpdateCurveState(false);
                return;
            }

            pointerManager.SetPointerTransform(
                InputManager.ControllerName.Brush,
                drawingWorldPoint,
                drawingPanel.transform.rotation);

            App application = App.Instance;
            MultiplayerManager multiplayerManager = MultiplayerManager.m_Instance;

            bool isPaintingAllowed =
                application != null &&
                application.IsInStateThatAllowsPainting();

            bool isViewOnly =
                multiplayerManager != null &&
                multiplayerManager.IsViewOnly;

            bool isPaintingActive =
                m_brushTrigger &&
                !s_IsStyleCurveDone &&
                isPaintingAllowed &&
                !isViewOnly;

            UpdateCurveState(isPaintingActive);
            SetDrawingActive(isPaintingActive);

            if (isPaintingActive)
            {
                SavePaintPoint(drawingWorldPoint);
            }
        }
    }
}
