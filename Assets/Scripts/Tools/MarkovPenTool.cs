using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Provides a custom Markov pen tool that can draw like a brush while forcing Straight Edge mode.
    /// Handles pointer positioning, brush trigger input, painting activation, and tool transform updates.
    /// </summary>
    public class MarkovPenTool : BaseTool
    {
        /// <summary>
        /// Stores the visual direction indicator object used by the tool.
        /// The indicator is rotated according to the current free paint pointer angle.
        /// </summary>
        private GameObject m_ToolDirectionIndicator;

        /// <summary>
        /// Indicates whether the tool transform should follow the brush controller.
        /// Used when the sketch surface is in free paint mode.
        /// </summary>
        private bool m_IsLockedToController;

        /// <summary>
        /// Stores the brush controller transform when the tool is locked to the controller.
        /// Used to update the tool transform position and rotation.
        /// </summary>
        private Transform m_BrushController;

        /// <summary>
        /// Indicates whether the tool is currently painting.
        /// Becomes true when the brush trigger is pressed and painting is allowed.
        /// </summary>
        private bool m_IsPaintingActive;

        /// <summary>
        /// Stores the current brush trigger pressure value.
        /// Passed to the pointer manager as brush pressure while drawing.
        /// </summary>
        private float m_BrushTriggerRatio;

        /// <summary>
        /// Stores the Straight Edge mode state before the Markov pen was enabled.
        /// Used to restore the previous state when the tool is disabled.
        /// </summary>
        private bool m_PreviousStraightEdgeMode;

        /// <summary>
        /// Defines the orientation correction applied to the brush controller rotation.
        /// Matches the orientation adjustment used by the free paint pointer.
        /// </summary>
        private static readonly Quaternion s_OrientationAdjust =
            Quaternion.Euler(new Vector3(0, 180, 0));

        /// <summary>
        /// Initialize the Markov pen tool and cache required child references.
        /// Finds the direction indicator object and resets the painting state.
        /// </summary>
        public override void Init()
        {
            base.Init();

            Transform indicator = transform.Find("DirectionIndicator");
            if (indicator != null)
            {
                m_ToolDirectionIndicator = indicator.gameObject;
            }
            m_IsPaintingActive = false;
        }

        /// <summary>
        /// Enable or disable the Markov pen tool.
        /// Activates Straight Edge mode while the tool is enabled and restores the previous state when disabled.
        /// </summary>
        /// <param name="isEnabled">Whether the Markov pen tool should be enabled.</param>
        public override void EnableTool(bool isEnabled)
        {
            Debug.LogWarning("Markov Pen Tool Enabled: " + isEnabled);
            base.EnableTool(isEnabled);

            if (isEnabled)
            {
                m_IsLockedToController = m_SketchSurface.IsInFreePaintMode();

                if (m_IsLockedToController)
                {
                    m_BrushController =
                        InputManager.m_Instance.GetController(InputManager.ControllerName.Brush);
                }

                m_PreviousStraightEdgeMode =
                    PointerManager.m_Instance.StraightEdgeModeEnabled;

                PointerManager.m_Instance.StraightEdgeModeEnabled = true;
            }
            else
            {
                PointerManager.m_Instance.EnableLine(false);
                m_IsPaintingActive = false;

                PointerManager.m_Instance.StraightEdgeModeEnabled =
                    m_PreviousStraightEdgeMode;
            }

            SketchControlsScript.m_Instance.ForceShowUIReticle(false);
        }

        /// <summary>
        /// Hide or show the Markov pen tool visuals.
        /// Updates the direction indicator visibility based on the hidden state.
        /// </summary>
        /// <param name="isHidden">Whether the Markov pen tool should be hidden.</param>
        public override void HideTool(bool isHidden)
        {
            base.HideTool(isHidden);

            if (m_ToolDirectionIndicator != null)
            {
                m_ToolDirectionIndicator.SetActive(!isHidden);
            }
        }

        /// <summary>
        /// Determine whether the main pointer should be visible while this tool is active.
        /// Hides the pointer only while intro sketchbook mode is active.
        /// </summary>
        /// <returns>True if the pointer should be shown; otherwise, false.</returns>
        public override bool ShouldShowPointer()
        {
            return !PanelManager.m_Instance.IntroSketchbookMode;
        }

        /// <summary>
        /// Determine whether touch visuals should be shown for this tool.
        /// The Markov pen does not use touch visuals.
        /// </summary>
        /// <returns>Always returns false.</returns>
        public override bool ShouldShowTouch()
        {
            return false;
        }

        /// <summary>
        /// Determine whether the brush size can be adjusted.
        /// Brush size adjustment is only allowed when the application is in a paintable state.
        /// </summary>
        /// <returns>True if the brush size can be adjusted; otherwise, false.</returns>
        public override bool CanAdjustSize()
        {
            return App.Instance.IsInStateThatAllowsPainting();
        }

        /// <summary>
        /// Get the current brush size value for the brush controller pointer.
        /// Returns the normalized brush size value between zero and one.
        /// </summary>
        /// <returns>The normalized brush size value for the brush pointer.</returns>
        public override float GetSize01()
        {
            return PointerManager.m_Instance.GetPointerBrushSize01(
                InputManager.ControllerName.Brush
            );
        }

        /// <summary>
        /// Update the brush size by the given adjustment amount.
        /// Applies the size change to all pointers and notifies the application that the brush size changed.
        /// </summary>
        /// <param name="adjustAmount">The amount by which the brush size should be adjusted.</param>
        public override void UpdateSize(float adjustAmount)
        {
            PointerManager.m_Instance.AdjustAllPointersBrushSize01(adjustAmount);
            PointerManager.m_Instance.MarkAllBrushSizeUsed();
            App.Switchboard.TriggerBrushSizeChanged();
        }

        /// <summary>
        /// Update the Markov pen tool input and drawing state.
        /// Forces Straight Edge mode, positions the pointer, handles trigger input, and updates brush pressure.
        /// </summary>
        public override void UpdateTool()
        {
            PointerManager.m_Instance.StraightEdgeModeEnabled = true;

            bool isBrushTriggerActive =
                InputManager.Brush.GetCommand(InputManager.SketchCommands.Activate);

            m_BrushTriggerRatio = InputManager.Brush.GetTriggerRatio();

            if (m_EatInput && !isBrushTriggerActive)
            {
                m_EatInput = false;
            }

            if (m_ExitOnAbortCommand &&
                InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Abort))
            {
                m_RequestExit = true;
            }

            PositionPointer();

            m_IsPaintingActive =
                !m_EatInput &&
                !m_ToolHidden &&
                isBrushTriggerActive &&
                App.Instance.IsInStateThatAllowsPainting();
            if (MarkovPenDrawingPanel.IsOpen && MarkovPenDrawingPanel.Instance != null)
            {
                Debug.LogWarning("Markov Pen Panel Opened");
                // Constrain pointer to MarkovPenDrawingPanel surface and allow 2D painting there.
                Transform attach = InputManager.m_Instance.GetBrushControllerAttachPoint();
                if (attach != null)
                {
                    Debug.LogWarning("Attach point found: " + attach.name);

                    Ray ray = new Ray(attach.position, attach.forward);
                    Vector2 panel2D;
                    Vector3 worldPoint;
                    if (MarkovPenDrawingPanel.Instance.TryGetPanel2DPoint(ray, out panel2D, out worldPoint))
                    {
                        Debug.LogWarning("Raycast hit panel at: " + worldPoint);

                        // Place and orient pointer flat on the panel so strokes are created in 2D on the panel.
                        PointerManager.m_Instance.SetPointerTransform(InputManager.ControllerName.Brush, worldPoint, MarkovPenDrawingPanel.Instance.transform.rotation);
                        // Allow painting only while trigger is held and app allows painting.
                        m_IsPaintingActive = !m_EatInput && !m_ToolHidden && App.Instance.IsInStateThatAllowsPainting();
                    }
                    else
                    {
                        Debug.LogWarning("Raycast missed panel, disabling painting");

                        // If raycast misses, don't allow painting.
                        m_IsPaintingActive = false;
                    }
                }
                else
                {
                    Debug.LogWarning("Attach point not found, disabling painting");

                    m_IsPaintingActive = false;
                }
            }
            Debug.LogWarning(MarkovPenDrawingPanel.IsOpen + " " + MarkovPenDrawingPanel.Instance);

            PointerManager.m_Instance.EnableLine(m_IsPaintingActive);
            PointerManager.m_Instance.PointerPressure = m_BrushTriggerRatio;

            if (m_ToolDirectionIndicator != null)
            {
                m_ToolDirectionIndicator.transform.localRotation =
                    Quaternion.Euler(
                        PointerManager.m_Instance.FreePaintPointerAngle,
                        0f,
                        0f
                    );
            }
        }

        /// <summary>
        /// Perform late-frame updates for the Markov pen tool.
        /// Keeps Straight Edge mode active, updates the pointer when safe, and updates the tool transform.
        /// </summary>
        public override void LateUpdateTool()
        {
            base.LateUpdateTool();

            PointerManager.m_Instance.StraightEdgeModeEnabled = true;

            if (!PointerManager.m_Instance.IsMainPointerProcessingLine())
            {
                PositionPointer();
            }

            UpdateTransformsFromControllers();
        }

        /// <summary>
        /// Position the brush pointer at the brush controller attach point.
        /// Applies orientation correction, stencil magnetization, and sends the final transform to the pointer manager.
        /// </summary>
        private void PositionPointer()
        {
            Transform attachPoint =
                InputManager.m_Instance.GetBrushControllerAttachPoint();

            if (attachPoint == null)
            {
                return;
            }

            Vector3 position = attachPoint.position;
            Quaternion rotation = attachPoint.rotation * s_OrientationAdjust;

            if (position == Vector3.zero)
            {
                return;
            }

            WidgetManager.m_Instance.MagnetizeToStencils(ref position, ref rotation);

            PointerManager.m_Instance.SetPointerTransform(
                InputManager.ControllerName.Brush,
                position,
                rotation
            );
        }

        /// <summary>
        /// Update the Unity GameObject transform when the tool is not locked to the brush controller.
        /// Keeps the visible tool object aligned with the sketch surface when needed.
        /// </summary>
        private void Update()
        {
            if (!m_IsLockedToController)
            {
                UpdateTransformsFromControllers();
            }
        }

        /// <summary>
        /// Update the visible tool transform from the brush controller or the sketch surface panel.
        /// Locks the tool to the brush controller in free paint mode and otherwise aligns it with the sketch surface panel.
        /// </summary>
        private void UpdateTransformsFromControllers()
        {
            if (m_IsLockedToController && m_BrushController != null)
            {
                transform.position = m_BrushController.position;
                transform.rotation = m_BrushController.rotation;
            }
            else if (SketchSurfacePanel.m_Instance != null)
            {
                transform.position = SketchSurfacePanel.m_Instance.transform.position;
                transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
            }
        }
    }
}
