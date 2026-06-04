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
using UnityEngine.UI;

namespace TiltBrush
{
    public class NoHeadsetPointerCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Texture2D m_ClickCursor;
        [SerializeField] private Vector2 m_ClickHotspot = Vector2.zero;

        private static int sm_ClickHoverCount;
        private bool m_IsShowingClickCursor;

        public static void ForcePointerVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void ResetCursor()
        {
            sm_ClickHoverCount = 0;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            ForcePointerVisible();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ForcePointerVisible();
            if (!IsInteractableSelectable())
            {
                return;
            }

            Texture2D cursor = GetCursorTexture();
            if (cursor == null)
            {
                return;
            }

            m_IsShowingClickCursor = true;
            sm_ClickHoverCount++;
            Cursor.SetCursor(cursor, m_ClickHotspot, CursorMode.Auto);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ForcePointerVisible();
            ClearHoverCursor();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ForcePointerVisible();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ForcePointerVisible();
        }

        private bool IsInteractableSelectable()
        {
            Selectable selectable = GetComponent<Selectable>();
            return selectable != null && selectable.IsInteractable();
        }

        private Texture2D GetCursorTexture()
        {
            return m_ClickCursor != null
                ? m_ClickCursor
                : Resources.Load<Texture2D>("Icons/pointingarrow");
        }

        private void OnDisable()
        {
            ClearHoverCursor();
        }

        private void ClearHoverCursor()
        {
            if (!m_IsShowingClickCursor)
            {
                return;
            }

            m_IsShowingClickCursor = false;
            sm_ClickHoverCount = Mathf.Max(0, sm_ClickHoverCount - 1);
            if (sm_ClickHoverCount == 0)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }
    }
}
