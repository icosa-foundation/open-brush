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
using TMPro;
using UnityEngine;


namespace TiltBrush
{

    public class PolyhydraPopUpWindowOpFilters : PolyhydraPopUpWindowBase
    {

        public float xSpacing = 2.5f;
        public float ySpacing = .25f;

        protected override List<string> GetButtonList()
        {
            return Enum.GetNames(typeof(PreviewPolyhedron.AvailableFilters)).Skip(FirstButtonIndex).Take(ButtonsPerPage).ToList();
        }

        protected override string GetButtonTexturePath(string action)
        {
            return $"IconButtons/Spherize";
        }

        protected override void CreateButtons()
        {
            foreach (var btn in _buttons)
            {
                Destroy(btn);
            }
            _buttons = new List<GameObject>();
            List<string> buttonLabels = GetButtonList();
            int columns = 2;
            for (int buttonIndex = 0; buttonIndex < buttonLabels.Count; buttonIndex++)
            {
                GameObject rButton = Instantiate(ButtonPrefab);
                rButton.transform.parent = transform;
                rButton.transform.localRotation = Quaternion.identity;

                float xOffset = buttonIndex % columns;
                float yOffset = Mathf.FloorToInt(buttonIndex / (float)columns);
                Vector3 position = new Vector3(xOffset * xSpacing, -yOffset * ySpacing, 0);
                rButton.transform.localPosition = new Vector3(-0.52f, 0.15f, -0.08f) + (position * .35f);

                rButton.transform.localScale = Vector3.one;

                Renderer rButtonRenderer = rButton.GetComponent<Renderer>();
                // rButtonRenderer.material.mainTexture = GetButtonTexture(buttonIndex);

                PolyhydraPopupItemButton rButtonScript = rButton.GetComponent<PolyhydraPopupItemButton>();
                rButtonScript.parentPopup = this;
                rButtonScript.GetComponentInChildren<TextMeshPro>().text = buttonLabels[buttonIndex];
                rButtonScript.SetDescriptionText(buttonLabels[buttonIndex]);
                rButtonScript.ButtonAction = buttonLabels[buttonIndex];
                rButtonScript.RegisterComponent();
                _buttons.Add(rButton);
            }
        }

        public override void HandleButtonPress(string action)
        {
            var ops = ParentPanel.CurrentPolyhedra.Operators;
            var op = ops[ParentPanel.CurrentActiveOpIndex];
            op.filterType = (PreviewPolyhedron.AvailableFilters)Enum.Parse(typeof(PreviewPolyhedron.AvailableFilters), action);
            ops[ParentPanel.CurrentActiveOpIndex] = op;
            ParentPanel.CurrentPolyhedra.Operators = ops;
            ParentPanel.ButtonOpFilter.SetDescriptionText(action);
        }

        public void NextPage()
        {
            if (FirstButtonIndex + ButtonsPerPage < Enum.GetNames(typeof(PolyMesh.Operation)).Length)
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
