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


namespace TiltBrush
{

    public class PolyhydraPopUpWindowRadialTypes : PolyhydraPopUpWindowBase
    {

        protected override List<string> GetButtonList()
        {
            return Enum.GetNames(typeof(RadialSolids.RadialPolyType)).ToList();
        }

        protected override string GetButtonTexturePath(string action)
        {
            return $"ShapeButtons/poly_johnson_{action}";

        }

        public override void HandleButtonPress(string action)
        {
            ParentPanel.CurrentPolyhedra.RadialPolyType = (RadialSolids.RadialPolyType)Enum.Parse(typeof(RadialSolids.RadialPolyType), action);
            ParentPanel.ButtonRadialType.SetButtonTexture(GetButtonTexture(action));
            ParentPanel.SetSliderConfiguration();
        }

    }
} // namespace TiltBrush
