// Copyright 2022 The Tilt Brush Authors
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

using System.Collections.Generic;
using Polyhydra.Core;
using UnityEngine;

namespace TiltBrush.MeshEditing
{
    public class EditableModel
    {
        private Color[] _colors;
        public int MaterialIndex = 0;

        public Color[] Colors
        {
            get => _colors;
            set => _colors = (Color[])value.Clone();
        }
        public GeneratorTypes GeneratorType { get; set; }
        public PolyMesh PolyMesh { get; private set; }
        public ColorMethods ColorMethod { get; set; } = ColorMethods.ByRole;
        public Dictionary<string, object> GeneratorParameters { get; set; }
        public List<Dictionary<string, object>> Operations { get; set; }

        public Material CurrentMaterial => EditableModelManager.m_Instance.m_Materials[MaterialIndex];

        public EditableModel(PolyMesh polyMesh, Color[] colors, ColorMethods colorMethod, int materialIndex,
                        GeneratorTypes type, Dictionary<string, object> generatorParameters)
        {
            GeneratorType = type;
            PolyMesh = polyMesh;
            Colors = (Color[])colors.Clone();
            ColorMethod = colorMethod;
            MaterialIndex = materialIndex;
            GeneratorParameters = generatorParameters;
            Operations = new List<Dictionary<string, object>>();
        }

        public EditableModel(GeneratorTypes generatorType)
        {
            GeneratorType = generatorType;
        }

        public EditableModel(PolyMesh polyMesh, Color[] colors, ColorMethods colorMethod, int materialIndex,
                             GeneratorTypes type, Dictionary<string, object> generatorParameters,
                             List<Dictionary<string, object>> operations)
        {
            GeneratorType = type;
            PolyMesh = polyMesh;
            Colors = (Color[])colors.Clone();
            ColorMethod = colorMethod;
            MaterialIndex = materialIndex;
            GeneratorParameters = generatorParameters;
            Operations = operations;
        }

        public void SetPolyMesh(PolyMesh poly)
        {
            PolyMesh = poly;
        }
    }
}
