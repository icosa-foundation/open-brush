﻿// Copyright 2020 The Tilt Brush Authors
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
using UnityEngine.Localization;

namespace TiltBrush
{

    public class LoadBackgroundImageButton : BaseButton
    {
        public ReferenceImage ReferenceImage { get; set; }

        public void RefreshDescription()
        {
            if (ReferenceImage != null)
            {

                // null if image doesn't have error
                string errorMessage = ReferenceImage.ImageErrorExtraDescription();

                if (errorMessage != null)
                {
                    SetDescriptionText(App.ShortenForDescriptionText(ReferenceImage.FileName), errorMessage);
                }
                else
                {
                    SetDescriptionText(App.ShortenForDescriptionText(ReferenceImage.FileName));
                }

            }
        }
        override protected void OnButtonPressed()
        {
            if (ReferenceImage == null)
            {
                return;
            }
            if (ReferenceImage.NotLoaded)
            {
                // Load-on-demand.
                ReferenceImage.SynchronousLoad();
            }


            SceneSettings.m_Instance.LoadCustomSkyboxFromCache(ReferenceImage.FilePath);
        }

        override public void ResetState()
        {
            base.ResetState();

            // Make ourselves unavailable if our image has an error.
            bool available = false;
            if (ReferenceImage != null)
            {
                available = ReferenceImage.NotLoaded || ReferenceImage.Valid;
            }

            if (available != IsAvailable())
            {
                SetButtonAvailable(available);
            }

            RefreshDescription();
        }

        public void Set360ButtonTexture(Texture2D rTexture, float aspect = -1)
        {
            float isStereo = aspect > 1f ? 0f : 1f;
            m_CurrentButtonTexture = rTexture;
            m_ButtonRenderer.material.mainTexture = rTexture;
            m_ButtonRenderer.material.SetFloat("_Stereoscopic", isStereo);
        }

    }
} // namespace TiltBrush
