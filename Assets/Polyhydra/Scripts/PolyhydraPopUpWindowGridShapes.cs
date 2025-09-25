﻿// Copyright 2022 The Open Brush Authors
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
using UnityEngine;

namespace TiltBrush
{

    public class PolyhydraPopUpWindowGridShapes : PolyhydraPopUpWindowBase
    {

        protected override ItemListResults GetItemsList()
        {
            return new ItemListResults(Enum.GetNames(typeof(GridEnums.GridShapes)).ToList(), false);
        }

        public override Texture2D GetButtonTexture(string action)
        {
            return ParentPanel.GetButtonTexture(PolyhydraButtonTypes.GridShape, action);
        }

        public override void HandleButtonPress(string action, bool isFolder)
        {
            PreviewPolyhedron.m_Instance.m_PolyRecipe.GridShape = (GridEnums.GridShapes)Enum.Parse(typeof(GridEnums.GridShapes), action);
            ParentPanel.SetButtonTextAndIcon(PolyhydraButtonTypes.GridShape, action);
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }
    }
} // namespace TiltBrush
