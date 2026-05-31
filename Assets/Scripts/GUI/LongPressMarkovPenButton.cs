using UnityEngine;

namespace TiltBrush
{
    public class LongPressMarkovPenButton : PanelButton
    {
        [SerializeField] private float m_LongPressDuration = 0.3f;

        private float m_PressTimer;
        private bool m_LongPressTriggered;

        override protected void Awake()
        {
            base.Awake();
            m_HoldFocus = false;
            m_Type = BasePanel.PanelType.MarkovPenDrawingPanel;
        }

        override public void ButtonPressed(RaycastHit rHitInfo)
        {
            AdjustButtonPositionAndScale(m_ZAdjustClick, m_HoverScale, m_HoverBoxColliderGrow);
            m_CurrentButtonState = ButtonState.Held;
            m_PressTimer = 0.0f;
            m_LongPressTriggered = false;
        }

        override public void ButtonHeld(RaycastHit rHitInfo)
        {
            if (m_CurrentButtonState != ButtonState.Held || m_LongPressTriggered)
            {
                return;
            }

            m_PressTimer += Time.deltaTime;

            if (m_PressTimer >= m_LongPressDuration)
            {
                m_LongPressTriggered = true;

                if (IsAvailable())
                {
                    // Das ist die normale PanelButton-Logik:
                    // öffnet ein echtes Panel, kein PopUp.
                    OnButtonPressed();

                    if (m_ButtonHasPressedAudio)
                    {
                        AudioManager.m_Instance.ItemSelect(transform.position);
                    }
                }

                // Wichtig: Button-Zustand loslassen, damit das neue Panel interagierbar ist.
                m_CurrentButtonState = ButtonState.Untouched;
                ResetScale();
            }
        }

        override public void ButtonReleased()
        {
            // Nach einem LongPress nichts mehr auslösen.
            if (m_LongPressTriggered)
            {
                m_LongPressTriggered = false;
                m_CurrentButtonState = ButtonState.Untouched;
                ResetScale();
                return;
            }

            // Kurzer Klick soll hier NICHT das Panel öffnen.
            // Falls kurzer Klick MarkovPen-Tool aktivieren soll, hier Tool-Logik einbauen.
            if (m_CurrentButtonState == ButtonState.Held)
            {
                m_CurrentButtonState = ButtonState.Untouched;
            }

            ResetScale();
        }

        override public void GainFocus()
        {
            AdjustButtonPositionAndScale(m_ZAdjustHover, m_HoverScale, m_HoverBoxColliderGrow);

            if (m_CurrentButtonState != ButtonState.Pressed)
            {
                AudioManager.m_Instance.ItemHover(transform.position);
            }

            m_CurrentButtonState = ButtonState.Hover;
            SetDescriptionActive(true);
            m_PressTimer = 0.0f;
            m_LongPressTriggered = false;
        }
    }
}
