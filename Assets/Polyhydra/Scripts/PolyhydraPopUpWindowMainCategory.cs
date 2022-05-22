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
using UnityEngine;

namespace TiltBrush
{

    public class PolyhydraPopUpWindowMainCategory : PolyhydraPopUpWindowBase
    {

        protected override List<string> GetButtonList()
        {
            var names = ParentPanel.GetMainCategoryNames();
            return names;
        }
        
        public override Texture2D GetButtonTexture(string action)
        {
            return ParentPanel.GetButtonTexture(PolyhydraButtonTypes.MainCategory, action);
        }

        public override void HandleButtonPress(string action)
        {
            ParentPanel.SetButtonTextAndIcon(PolyhydraButtonTypes.MainCategory, action);
            var mainCat = (PolyhydraPanel.PolyhydraMainCategories)Enum.Parse(
                typeof(PolyhydraPanel.PolyhydraMainCategories),
                action
            );
            ParentPanel.HandleMainCategoryButtonPress(mainCat);
        }
    }
} // namespace TiltBrush
