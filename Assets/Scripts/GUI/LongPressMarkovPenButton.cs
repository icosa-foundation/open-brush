using UnityEngine;

namespace TiltBrush
{
    /// @brief Provides a Markov pen button with separate normal press and long press behavior.
    /// A normal press replays stored Markov control points, while a long press activates the
    /// configured long press tool and opens the assigned panel action through the base button logic.
    public class LongPressMarkovPenButton : PanelButton
    {
        [SerializeField] private float m_LongPressDuration = 0.3f;

        [Header("Normal Press")]
        [SerializeField] private BaseTool.ToolType m_NormalPressTool;

        [Header("Long Press")]
        [SerializeField] private BaseTool.ToolType m_LongPressTool;

        private float m_PressTimer;
        private bool m_HasLongPressTriggered;
        private MarkovPenPointFollower m_PointFollower;

        /// @brief Initialize the long press Markov pen button.
        /// Creates the point follower component and disables hold focus so this button can
        /// handle the long press state manually.
        protected override void Awake()
        {
            base.Awake();

            m_PointFollower = gameObject.AddComponent<MarkovPenPointFollower>();
            m_HoldFocus = false;
        }

        /// @brief Handle the initial button press interaction.
        /// Moves the button into its pressed visual state and resets long press tracking.
        /// @param raycastHitInfo Raycast hit information from the button interaction.
        public override void ButtonPressed(RaycastHit raycastHitInfo)
        {
            AdjustButtonPositionAndScale(
                m_ZAdjustClick,
                m_HoverScale,
                m_HoverBoxColliderGrow);

            m_CurrentButtonState = ButtonState.Held;
            m_PressTimer = 0.0f;
            m_HasLongPressTriggered = false;
        }

        /// @brief Handle the button hold interaction.
        /// Increases the press timer while the button remains held and executes the long press
        /// behavior once the configured duration is reached.
        /// @param raycastHitInfo Raycast hit information from the button interaction.
        public override void ButtonHeld(RaycastHit raycastHitInfo)
        {
            if (m_CurrentButtonState != ButtonState.Held || m_HasLongPressTriggered)
            {
                return;
            }

            m_PressTimer += Time.deltaTime;

            if (m_PressTimer < m_LongPressDuration)
            {
                return;
            }

            m_HasLongPressTriggered = true;

            if (IsAvailable())
            {
                ActivateLongPressTool();
                OnButtonPressed();
                PlayPressedAudio();
            }

            SetButtonUntouched();
        }

        /// @brief Handle the button release interaction.
        /// Executes the normal press behavior if no long press was triggered.
        public override void ButtonReleased()
        {
            if (m_HasLongPressTriggered)
            {
                m_HasLongPressTriggered = false;
                SetButtonUntouched();
                return;
            }

            StartPointFollower();
            //ActivateNormalPressTool();
            PlayPressedAudio();
            SetButtonUntouched();
        }

        /// @brief Handle the button focus interaction.
        /// Moves the button into its hover visual state, plays hover audio, and resets long press tracking.
        public override void GainFocus()
        {
            AdjustButtonPositionAndScale(
                m_ZAdjustHover,
                m_HoverScale,
                m_HoverBoxColliderGrow);

            if (m_CurrentButtonState != ButtonState.Pressed)
            {
                AudioManager.m_Instance.ItemHover(transform.position);
            }

            m_CurrentButtonState = ButtonState.Hover;
            SetDescriptionActive(true);

            m_PressTimer = 0.0f;
            m_HasLongPressTriggered = false;
        }

        /// @brief Activate the tool assigned to the normal press action.
        private void ActivateNormalPressTool()
        {
            if (SketchSurfacePanel.m_Instance == null)
            {
                Debug.LogError("LongPressMarkovPenButton: SketchSurfacePanel instance is null.");
                return;
            }

            SketchSurfacePanel.m_Instance.EnableSpecificTool(m_NormalPressTool);
        }

        /// @brief Activate the tool assigned to the long press action.
        private void ActivateLongPressTool()
        {
            if (SketchSurfacePanel.m_Instance == null)
            {
                Debug.LogError("LongPressMarkovPenButton: SketchSurfacePanel instance is null.");
                return;
            }

            SketchSurfacePanel.m_Instance.EnableSpecificTool(m_LongPressTool);
        }

        /// @brief Start following the saved Markov control points with the pointer follower.
        private void StartPointFollower()
        {
            if (m_PointFollower == null)
            {
                m_PointFollower = gameObject.AddComponent<MarkovPenPointFollower>();
            }

            m_PointFollower.StartFollowing(MarkovPenDrawingFreepaint.s_ControlPoints);
        }

        /// @brief Play the button pressed audio feedback if it is enabled.
        private void PlayPressedAudio()
        {
            if (!m_ButtonHasPressedAudio)
            {
                return;
            }

            AudioManager.m_Instance.ItemSelect(transform.position);
        }

        /// @brief Reset the button state and visual scale to the untouched state.
        private void SetButtonUntouched()
        {
            m_CurrentButtonState = ButtonState.Untouched;
            ResetScale();
        }
    }
}
