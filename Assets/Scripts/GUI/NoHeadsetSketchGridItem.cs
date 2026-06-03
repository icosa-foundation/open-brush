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
        private static Texture2D sm_ClickCursor;
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

            m_IsShowingClickCursor = true;
            sm_ClickHoverCount++;
            Cursor.SetCursor(GetClickCursor(), Vector2.zero, CursorMode.Auto);
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

        private void OnDisable()
        {
            ClearHoverCursor();
        }

        private bool IsInteractableSelectable()
        {
            Selectable selectable = GetComponent<Selectable>();
            return selectable != null && selectable.IsInteractable();
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

        private static Texture2D GetClickCursor()
        {
            if (sm_ClickCursor != null)
            {
                return sm_ClickCursor;
            }

            sm_ClickCursor = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            sm_ClickCursor.filterMode = FilterMode.Point;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color white = Color.white;
            Color black = Color.black;
            Color32[] pixels = new Color32[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            string[] rows =
            {
                "....#...........",
                "...##...........",
                "..#.#...........",
                "..#.#...........",
                "..#.#..#........",
                "..#.#.##........",
                "..#.##.#........",
                "..##...#........",
                "..#....#........",
                "..#....#........",
                "..#....#........",
                "...#...#........",
                "...#...#........",
                "....###.........",
                "................",
                "................"
            };

            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    if (rows[y][x] != '#')
                    {
                        continue;
                    }

                    SetCursorPixel(pixels, x, y, black);
                    SetCursorPixel(pixels, x + 1, y, white);
                }
            }

            sm_ClickCursor.SetPixels32(pixels);
            sm_ClickCursor.Apply();
            return sm_ClickCursor;
        }

        private static void SetCursorPixel(Color32[] pixels, int x, int y, Color color)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16)
            {
                return;
            }

            pixels[(15 - y) * 16 + x] = color;
        }
    }

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
