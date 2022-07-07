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

using UnityEngine;
using UnityEngine.Events;

namespace TiltBrush
{
    public class PolyhydraColorButton : BaseButton
    {

        public int Index;
        public UnityEvent<int> OnPressed;
        public MeshRenderer ColorSwatch;
        override protected void OnButtonPressed()
        {
            OnPressed.Invoke(Index);
        }

        public void SetColorSwatch(Color color)
        {
            // Set's the color separately from normal UI code
            // So we can have color palette buttons that match the chosen color
            ColorSwatch.material.color = color;
            SetDescriptionText(
                "Color",
                ColorTable.m_Instance.NearestColorTo(color)
            );
        }

    }
} // namespace TiltBrush
