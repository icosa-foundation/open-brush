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
using UnityEngine.UI;

namespace TiltBrush
{
    public class NoHeadsetSketchGridItem : MonoBehaviour
    {
        [SerializeField] private Button m_Button;
        [SerializeField] private Image m_Thumbnail;
        [SerializeField] private Image m_LocalThumbnail;
        [SerializeField] private Image m_RemoteThumbnail;
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_Source;
        [SerializeField] private GameObject m_LoadingIndicator;

        private int m_Index;
        private Action<int> m_OnClick;
        private string m_TitleText;
        private string m_AuthorText;

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
                m_Title.richText = true;
            }
            m_TitleText = title ?? "";
            m_AuthorText = null;
            UpdateTitleText();
            if (m_Source != null)
            {
                m_Source.text = sourceLabel;
                m_Source.gameObject.SetActive(!string.IsNullOrEmpty(sourceLabel));
            }
            SetAuthor(null);

            SetThumbnail(thumbnail, loading);

            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.onClick.AddListener(() => m_OnClick?.Invoke(m_Index));
            }
        }

        public void SetThumbnailFrame(bool square)
        {
            if (m_LocalThumbnail != null)
            {
                m_LocalThumbnail.gameObject.SetActive(square);
            }
            if (m_RemoteThumbnail != null)
            {
                m_RemoteThumbnail.gameObject.SetActive(!square);
            }

            Image selectedThumbnail = square ? m_LocalThumbnail : m_RemoteThumbnail;
            if (selectedThumbnail != null)
            {
                m_Thumbnail = selectedThumbnail;
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
            m_TitleText = title ?? "";
            UpdateTitleText();
        }

        public void SetAuthor(string author)
        {
            m_AuthorText = author ?? "";
            UpdateTitleText();
        }

        private void UpdateTitleText()
        {
            if (m_Title == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(m_AuthorText))
            {
                m_Title.text = m_TitleText;
                return;
            }

            m_Title.text = $"{m_TitleText}\n<color=\"grey\"><size=75%>{m_AuthorText}</size></color>";
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

        public void SetInteractionEnabled(bool enabled)
        {
            if (m_Button != null)
            {
                m_Button.interactable = enabled;
            }

            if (!enabled)
            {
                NoHeadsetPointerCursor.ResetCursor();
            }
        }

        public void ClearListeners()
        {
            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.interactable = true;
            }
            m_OnClick = null;
        }
    }
}
