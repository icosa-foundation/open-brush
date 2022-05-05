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

    public class PolyhydraPopUpWindowConwayFaceSel : PolyhydraPopUpWindowBase
    {

        [NonSerialized] protected int OpStackIndex = 0;
        public float xSpacing = 2.5f;
        public float ySpacing = .25f;

        protected override string[] GetButtonList()
        {
            return Enum.GetNames(typeof(PreviewPolyhedron.AvailableFilters)).Skip(FirstButtonIndex).Take(ButtonsPerPage).ToArray();
        }

        public override void SetPopupCommandParameters(int commandParam, int commandParam2)
        {
            base.SetPopupCommandParameters(commandParam, commandParam2);
            OpStackIndex = commandParam;
        }

        protected override string GetButtonTexturePath(int i)
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
            string[] buttonLabels = GetButtonList();
            int columns = 2;
            for (int buttonIndex = 0; buttonIndex < buttonLabels.Length; buttonIndex++)
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

                PolyhydraThingButton rButtonScript = rButton.GetComponent<PolyhydraThingButton>();
                rButtonScript.ButtonIndex = buttonIndex;
                rButtonScript.parentPopup = this;
                rButtonScript.GetComponentInChildren<TextMeshPro>().text = buttonLabels[buttonIndex];
                rButtonScript.SetDescriptionText(buttonLabels[buttonIndex]);
                rButtonScript.RegisterComponent();
                _buttons.Add(rButton);
            }
        }

        public override void HandleButtonPress(int relativeButtonIndex)
        {
            int absoluteButtonIndex = relativeButtonIndex + FirstButtonIndex;
            var ops = ParentPanel.PolyhydraModel.ConwayOperators;

            var op = ops[OpStackIndex];
            op.filters = (PreviewPolyhedron.AvailableFilters)absoluteButtonIndex;
            ops[OpStackIndex] = op;
            ParentPanel.PolyhydraModel.ConwayOperators = ops;
            ParentPanel.ButtonsFaceSel[OpStackIndex].SetDescriptionText(GetButtonList()[relativeButtonIndex]);
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
