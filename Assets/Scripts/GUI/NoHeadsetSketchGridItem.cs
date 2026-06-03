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

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TiltBrush
{
    public class NoHeadsetPointerCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public static void ForcePointerVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ForcePointerVisible();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ForcePointerVisible();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ForcePointerVisible();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ForcePointerVisible();
        }
    }

    public class NoHeadsetSketchGridItem : MonoBehaviour
    {
        [SerializeField] private Button m_Button;
        [SerializeField] private Image m_Thumbnail;
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_Source;
        [SerializeField] private GameObject m_LoadingIndicator;

        private int m_Index;
        private Action<int> m_OnClick;

        public void SetReferences(Button button, Image thumbnail, TextMeshProUGUI title,
            TextMeshProUGUI source, GameObject loadingIndicator)
        {
            m_Button = button;
            m_Thumbnail = thumbnail;
            m_Title = title;
            m_Source = source;
            m_LoadingIndicator = loadingIndicator;
        }

        public void Init(int index, string title, string sourceLabel, Sprite thumbnail, bool loading,
            Action<int> onClick)
        {
            m_Index = index;
            m_OnClick = onClick;

            if (m_Title != null)
            {
                m_Title.text = title;
            }
            if (m_Source != null)
            {
                m_Source.text = sourceLabel;
                m_Source.gameObject.SetActive(!string.IsNullOrEmpty(sourceLabel));
            }

            SetThumbnail(thumbnail, loading);

            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.onClick.AddListener(() => m_OnClick?.Invoke(m_Index));
            }
        }

        public void SetThumbnail(Sprite thumbnail, bool loading)
        {
            if (m_Thumbnail != null)
            {
                m_Thumbnail.sprite = thumbnail;
                m_Thumbnail.color = thumbnail != null && !loading
                    ? Color.white
                    : new Color(0.28f, 0.28f, 0.28f, 1f);
            }
            if (m_LoadingIndicator != null)
            {
                m_LoadingIndicator.SetActive(loading);
            }
        }

        public bool HasLoadedThumbnailTexture()
        {
            if (m_Thumbnail == null || m_Thumbnail.sprite == null)
            {
                return false;
            }
            return m_Thumbnail.sprite.texture != null;
        }

        public bool HasAssignedThumbnailSprite()
        {
            return m_Thumbnail != null && m_Thumbnail.sprite != null;
        }

        public void SetTitle(string title)
        {
            if (m_Title != null)
            {
                m_Title.text = title;
            }
        }

        public void SetAvailableVisual(bool available)
        {
            CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = available ? 1f : 0.55f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        public void ClearListeners()
        {
            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
            }
            m_OnClick = null;
        }
    }
}
