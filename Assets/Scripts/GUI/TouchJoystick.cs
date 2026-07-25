// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using UnityEngine.EventSystems;

namespace TiltBrush
{
    /// <summary>
    /// A simple on-screen virtual joystick for non-VR touchscreen use.
    /// Exposes an analog Value in the range [-1, 1] on each axis and whether it is
    /// currently being touched. Read by FlyTool only when on a touchscreen device;
    /// it does not affect desktop or VR input paths.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TouchJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        // The knob that visually follows the finger. Optional.
        [SerializeField] private RectTransform m_Handle;
        // How far (in this RectTransform's local units) the knob travels at full deflection.
        [SerializeField] private float m_HandleRange = 75f;

        private RectTransform m_BaseRect;

        // Analog output, each axis clamped to [-1, 1]. x = strafe, y = forward/back.
        public Vector2 Value { get; private set; }
        public bool IsPressed { get; private set; }

        private void Awake()
        {
            m_BaseRect = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_BaseRect == null) { return; }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_BaseRect, eventData.position, eventData.pressEventCamera, out Vector2 local);

            // local is measured from the rect's pivot, which we assume is centered.
            Vector2 v = local / m_HandleRange;
            if (v.sqrMagnitude > 1f) { v = v.normalized; }
            Value = v;

            if (m_Handle != null)
            {
                m_Handle.anchoredPosition = v * m_HandleRange;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
            Value = Vector2.zero;
            if (m_Handle != null)
            {
                m_Handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
