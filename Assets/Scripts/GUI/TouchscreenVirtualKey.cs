using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TiltBrush
{
    [RequireComponent(typeof(Button))]
    public class TouchscreenVirtualKey : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public char m_Key;
        [NonSerialized] public bool m_IsPressed;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_button.interactable) return;
            m_IsPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            m_IsPressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_IsPressed = false;
        }

        // Afaik needed so Pointer exit works .. doing nothing further
        public void OnPointerEnter(PointerEventData eventData) { }
    }
}
