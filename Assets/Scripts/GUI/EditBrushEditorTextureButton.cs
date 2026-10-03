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
using UnityEngine;
namespace TiltBrush
{

    public class EditBrushEditorTextureButton : BaseButton
    {
        private int m_TextureIndex;

        public void SetPreset(Texture2D tex, string texName, int textureIndex)
        {
            m_TextureIndex = textureIndex;
            SetButtonTexture(tex);
            SetDescriptionText(texName);
        }

        override protected void OnButtonPressed()
        {
            var parentPanel = gameObject.GetComponentInParent<EditBrushPanel>();
            var popup = (BrushEditorTexturePopUpWindow)parentPanel.PanelPopUp;
            var texturePropertyName = popup.OpenerButton.TexturePropertyName;
            parentPanel.TextureChanged(texturePropertyName, m_TextureIndex, popup.OpenerButton);
        }
    }
} // namespace TiltBrush
