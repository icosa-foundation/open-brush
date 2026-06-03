using UnityEngine;

namespace TiltBrush
{
    /// Represents a panel button that opens the Markov pen sketchbook panel by long press.
    /// Tracks the current press duration while the button is held.
    /// Triggers the assigned panel action only after the required long press duration.
    public class LongPressMarkovPenButton : PanelButton
    {
        [SerializeField] private float m_LongPressDuration = 0.3f;

        private float m_PressTimer;
        private bool m_HasLongPressTriggered;

        /// Initialises the long press Markov pen button.
        /// Calls the base button setup and configures the target panel type.
        /// Disables hold focus so the button can manage its own long press state.
        protected override void Awake()
        {
            base.Awake();

            m_HoldFocus = false;
            m_Type = BasePanel.PanelType.MarkovPenSketchbookPanel;
        }

        /// Handles the initial button press interaction.
        /// Moves and scales the button into its pressed visual state.
        /// Resets the long press timer and trigger state.
        /// @param raycastHitInfo Raycast hit information from the button interaction.
        public override void ButtonPressed(RaycastHit raycastHitInfo)
        {
            AdjustButtonPositionAndScale(m_ZAdjustClick, m_HoverScale, m_HoverBoxColliderGrow);

            m_CurrentButtonState = ButtonState.Held;
            m_PressTimer = 0.0f;
            m_HasLongPressTriggered = false;
        }

        /// Handles the button hold interaction.
        /// Increases the press timer while the button remains held.
        /// Triggers the assigned panel action once the long press duration is reached.
        /// @param raycastHitInfo Raycast hit information from the button interaction.
        public override void ButtonHeld(RaycastHit raycastHitInfo)
        {
            if (m_CurrentButtonState != ButtonState.Held || m_HasLongPressTriggered)
            {
                return;
            }

            m_PressTimer += Time.deltaTime;

            if (m_PressTimer >= m_LongPressDuration)
            {
                m_HasLongPressTriggered = true;

                if (IsAvailable())
                {
                    OnButtonPressed();

                    if (m_ButtonHasPressedAudio)
                    {
                        AudioManager.m_Instance.ItemSelect(transform.position);
                    }
                }

                m_CurrentButtonState = ButtonState.Untouched;
                ResetScale();
            }
        }

        /// Handles the button release interaction.
        /// Resets the button state after a completed long press.
        /// Restores the button scale without triggering a short press action.
        public override void ButtonReleased()
        {
            if (m_HasLongPressTriggered)
            {
                m_HasLongPressTriggered = false;
                m_CurrentButtonState = ButtonState.Untouched;
                ResetScale();

                return;
            }

            if (m_CurrentButtonState == ButtonState.Held)
            {
                m_CurrentButtonState = ButtonState.Untouched;
            }

            ResetScale();
        }

        /// Handles the button focus interaction.
        /// Moves and scales the button into its hover visual state.
        /// Plays hover audio when the button is not already pressed.
        /// Resets the long press state when focus is gained.
        public override void GainFocus()
        {
            AdjustButtonPositionAndScale(m_ZAdjustHover, m_HoverScale, m_HoverBoxColliderGrow);

            if (m_CurrentButtonState != ButtonState.Pressed)
            {
                AudioManager.m_Instance.ItemHover(transform.position);
            }

            m_CurrentButtonState = ButtonState.Hover;
            SetDescriptionActive(true);

            m_PressTimer = 0.0f;
            m_HasLongPressTriggered = false;
        }
    }
}
