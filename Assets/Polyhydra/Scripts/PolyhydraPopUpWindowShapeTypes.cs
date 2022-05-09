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

    public class PolyhydraPopUpWindowShapeTypes : PolyhydraPopUpWindowBase
    {

        protected override List<string> GetButtonList()
        {
            var names = Enum.GetNames(typeof(PreviewPolyhedron.MainCategories)).ToList();
            return names;
        }

        protected override string GetButtonTexturePath(string action)
        {
            return $"ShapeTypeButtons/{action}";
        }

        public override void HandleButtonPress(string action)
        {
            var shapeCategory = (PreviewPolyhedron.MainCategories)Enum.Parse(typeof(PreviewPolyhedron.MainCategories), action);
            ParentPanel.CurrentShapeCategory = shapeCategory;
            ParentPanel.ButtonShapeType.SetButtonTexture(GetButtonTexture(action));
            ParentPanel.SetPanelButtonVisibility();
            ParentPanel.ConfigureGeometry();
            ParentPanel.SetSliderConfiguration();
        }

    }
} // namespace TiltBrush
