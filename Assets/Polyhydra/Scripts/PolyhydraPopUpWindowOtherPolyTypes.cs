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
using System.Linq;


namespace TiltBrush
{

    public class PolyhydraPopUpWindowOtherPolyTypes : PolyhydraPopUpWindowBase
    {

        protected override string[] GetButtonList()
        {
            return Enum.GetNames(typeof(PreviewPolyhedron.OtherPolyTypes)).ToArray();
        }

        protected override string GetButtonTexturePath(int i)
        {
            return $"ShapeButtons/poly_other_{(PreviewPolyhedron.OtherPolyTypes)i}";
        }

        public override void HandleButtonPress(int buttonIndex)
        {
            ParentPanel.PolyhydraModel.OtherPolyType = (PreviewPolyhedron.OtherPolyTypes)buttonIndex;
            ParentPanel.ButtonOtherPolyType.SetButtonTexture(GetButtonTexture(buttonIndex));
            ParentPanel.SetSliderConfiguration();
        }
    }
} // namespace TiltBrush
