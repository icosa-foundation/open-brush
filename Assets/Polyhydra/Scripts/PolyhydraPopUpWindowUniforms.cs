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
using System.Globalization;
using System.Linq;
using Polyhydra.Wythoff;


namespace TiltBrush
{

    public class PolyhydraPopUpWindowUniforms : PolyhydraPopUpWindowBase
    {

        private Uniform[] GetCurrentUniformList(PreviewPolyhedron.MainCategories shapeCategory)
        {
            switch (shapeCategory)
            {
                case PreviewPolyhedron.MainCategories.Platonic:
                    return Uniform.Platonic;
                case PreviewPolyhedron.MainCategories.Archimedean:
                    return Uniform.Archimedean;
                case PreviewPolyhedron.MainCategories.KeplerPoinsot:
                    return Uniform.KeplerPoinsot;
                    // case ShapeCategories.UniformConvex:
                    //   return Uniform.Convex;
                    // case ShapeCategories.UniformStar:
                    //   return Uniform.Star;
            }

            return null;
        }
        protected override List<string> GetButtonList()
        {
            return GetCurrentUniformList(ParentPanel.CurrentShapeCategory).Select(x => x.Name).ToList();
        }

        protected override string GetButtonTexturePath(string action)
        {
            return $"ShapeButtons/poly_uniform_{action}".Replace(" ", "_");
        }

        public override void HandleButtonPress(string action)
        {
            string enumName = action.Replace(" ", "_");
            UniformTypes polyType = (UniformTypes)Enum.Parse(typeof(UniformTypes), enumName, true);
            ParentPanel.PolyhydraModel.UniformPolyType = polyType;
            ParentPanel.ButtonUniformType.SetButtonTexture(GetButtonTexture(action));
            ParentPanel.SetSliderConfiguration();
        }

    }
} // namespace TiltBrush
