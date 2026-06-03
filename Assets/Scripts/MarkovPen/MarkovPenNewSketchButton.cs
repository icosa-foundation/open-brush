using UnityEngine;

namespace TiltBrush.MarkovPen
{
    /// Represents the button used to open a new Markov pen drawing sketch.
    /// Configures the button to target the Markov pen drawing panel.
    /// Handles the pressed and released states of the panel button.
    public class MarkovPenNewSketchButton : PanelButton
    {
        /// Initialises the new sketch button.
        /// Calls the base button setup and configures the button behaviour.
        /// Disables hold focus and assigns the Markov pen drawing panel as target panel type.
        protected override void Awake()
        {
            base.Awake();

            m_HoldFocus = false;
            m_Type = BasePanel.PanelType.MarkovPenDrawingPanel;
        }

        /// Handles the button press interaction.
        /// Moves and scales the button into its pressed visual state.
        /// Sets the current button state to held while the button is pressed.
        /// @param raycastHitInfo Raycast hit information from the button interaction.
        public override void ButtonPressed(RaycastHit raycastHitInfo)
        {
            AdjustButtonPositionAndScale(m_ZAdjustClick, m_HoverScale, m_HoverBoxColliderGrow);
            m_CurrentButtonState = ButtonState.Held;
        }

        /// Handles the button release interaction.
        /// Resets the held button state back to untouched when needed.
        /// Restores the button scale and triggers the assigned button action.
        public override void ButtonReleased()
        {
            if (m_CurrentButtonState == ButtonState.Held)
            {
                m_CurrentButtonState = ButtonState.Untouched;
            }

            ResetScale();
            OnButtonPressed();
        }
    }
}
