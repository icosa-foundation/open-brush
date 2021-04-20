// Copyright 2020 The Tilt Brush Authors
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
using System.Linq;
using UnityEngine;


namespace TiltBrush
{

    public class PolyhydraPopUpWindowShapeTypes : PolyhydraPopUpWindowBase
    {

        protected override string[] GetButtonList()
        {
            var names = Enum.GetNames(typeof(VrUi.ShapeCategories)).ToArray();
            for (var i = 0; i < names.Length; i++)
            {
                // Not really Johnson Polyhedra, are they?
                names[i] = names[i] == "Johnson" ? "Radial" : names[i];
            }

            return names;
        }

        protected override string GetButtonTexturePath(int i)
        {
            return $"ShapeTypeButtons/{(VrUi.ShapeCategories)i}";
        }

        public override void HandleButtonPress(int buttonIndex)
        {
            var shapeCategory = (VrUi.ShapeCategories)buttonIndex;
            ParentPanel.CurrentShapeCategory = shapeCategory;
            ParentPanel.ButtonShapeType.SetButtonTexture(GetButtonTexture(buttonIndex));
            ParentPanel.SetPanelButtonVisibility();
            ParentPanel.ConfigureGeometry();
            ParentPanel.SetSliderConfiguration();
        }

    }
} // namespace TiltBrush
