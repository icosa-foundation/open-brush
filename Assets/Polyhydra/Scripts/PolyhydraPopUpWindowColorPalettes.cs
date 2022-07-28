// Copyright 2022 The Open Brush Authors
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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class PolyhydraPopUpWindowColorPalettes : PolyhydraPopUpWindowBase
    {

        private Dictionary<int, Texture2D> _paletteIconCache = new();

        public override void Init(GameObject rParent, string sText)
        {
            ParentPanel = rParent.GetComponent<PolyhydraPanel>();
            FirstButtonIndex = 0;
            base.Init(rParent, sText);
        }

        protected override ItemListResults GetItemsList()
        {
            var allItems = PolyhydraPanel.ColorPalettes;
            int nextPageButtonIndex = FirstButtonIndex + ButtonsPerPage;
            bool nextPageExists = nextPageButtonIndex <= allItems.Count;

            return new ItemListResults(
                Enumerable.Range(0, allItems.Count).Select(i => i.ToString()).ToList()
                .Take(ButtonsPerPage)
                .ToList(), nextPageExists);
        }

        public override Texture2D GetButtonTexture(string action)
        {
            int paletteIndex = Int32.Parse(action);
            var paletteString = PolyhydraPanel.ColorPalettes[paletteIndex];
            if (_paletteIconCache.ContainsKey(paletteIndex)) return _paletteIconCache[paletteIndex];
            int width = 5, height = 5;
            var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (ColorUtility.TryParseHtmlString(paletteString[x], out Color color))
                    {
                        tex.SetPixel(x, y, color);
                    }
                }
            }
            tex.Apply();
            _paletteIconCache[paletteIndex] = tex;
            return tex;
        }

        public override void HandleButtonPress(string action, bool isFolder)
        {
            // ParentPanel.SetButtonTextAndIcon(PolyhydraButtonTypes.ColorPalette, action, friendlyLabel);
            ParentPanel.HandleSetColorsToPalette(Int32.Parse(action));
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void NextPage()
        {
            if (FirstButtonIndex + ButtonsPerPage < PolyhydraPanel.ColorPalettes.Count)
            {
                FirstButtonIndex += ButtonsPerPage;
                CreateButtons();
            }
            ParentPanel.CurrentOperatorPage = FirstButtonIndex / ButtonsPerPage;
        }

        public void PrevPage()
        {
            FirstButtonIndex -= ButtonsPerPage;
            FirstButtonIndex = Mathf.Max(0, FirstButtonIndex);
            CreateButtons();
            ParentPanel.CurrentOperatorPage = FirstButtonIndex / ButtonsPerPage;
        }

    }
} // namespace TiltBrush
