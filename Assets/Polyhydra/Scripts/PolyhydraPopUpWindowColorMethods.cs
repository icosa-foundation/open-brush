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
using Polyhydra.Core;
using TiltBrush.MeshEditing;
using TMPro;
using UnityEngine;

namespace TiltBrush
{

    public class PolyhydraPopUpWindowColorMethods : PolyhydraPopUpWindowBase
    {

        public float xSpacing = 2.5f;
        public float ySpacing = .25f;

        protected override ItemListResults GetItemsList()
        {
            var allItems = Enum.GetNames(typeof(ColorMethods));
            int nextPageButtonIndex = FirstButtonIndex + ButtonsPerPage;
            bool nextPageExists = nextPageButtonIndex <= allItems.Count();

            return new ItemListResults(
                allItems.Skip(FirstButtonIndex).Take(ButtonsPerPage).ToList(), nextPageExists
            );
        }

        protected override void CreateButtons()
        {
            foreach (var btn in _buttons)
            {
                Destroy(btn);
            }
            _buttons = new List<GameObject>();
            ItemListResults itemList = GetItemsList();
            int columns = 2;
            for (int buttonIndex = 0; buttonIndex < itemList.ItemCount; buttonIndex++)
            {
                GameObject rButton = Instantiate(ButtonPrefab);
                rButton.transform.parent = transform;
                rButton.transform.localRotation = Quaternion.identity;

                float xOffset = buttonIndex % columns;
                float yOffset = Mathf.FloorToInt(buttonIndex / (float)columns);
                Vector3 position = new Vector3(xOffset * xSpacing, -yOffset * ySpacing, 0);
                rButton.transform.localPosition = new Vector3(-0.52f, 0.15f, -0.08f) + (position * .35f);

                rButton.transform.localScale = Vector3.one;
                string buttonName = itemList.Items[buttonIndex];
                string friendlyName = PolyhydraPanel.LabelFormatter(buttonName);
                PolyhydraPopupItemButton rButtonScript = rButton.GetComponent<PolyhydraPopupItemButton>();
                rButtonScript.parentPopup = this;
                rButtonScript.GetComponentInChildren<TextMeshPro>().text = friendlyName;
                rButtonScript.SetDescriptionText(friendlyName);
                rButtonScript.ButtonAction = buttonName;
                rButtonScript.RegisterComponent();
                _buttons.Add(rButton);
            }
        }

        public override Texture2D GetButtonTexture(string action)
        {
            return ParentPanel.GetButtonTexture(PolyhydraButtonTypes.ColorMethod, action);
        }

        public override void HandleButtonPress(string action, bool isFolder)
        {
            if (!Enum.TryParse(action, true, out ColorMethods colorMethod)) return;
            EditableModelManager.CurrentModel.ColorMethod = colorMethod;
            ParentPanel.SetButtonTextAndIcon(PolyhydraButtonTypes.ColorMethod, action);
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        public void NextPage()
        {
            if (FirstButtonIndex + ButtonsPerPage < Enum.GetNames(typeof(ColorMethods)).Length)
            {
                FirstButtonIndex += ButtonsPerPage;
                CreateButtons();
            }
        }

        public void PrevPage()
        {
            FirstButtonIndex -= ButtonsPerPage;
            FirstButtonIndex = Mathf.Max(0, FirstButtonIndex);
            CreateButtons();
        }

    }
} // namespace TiltBrush
