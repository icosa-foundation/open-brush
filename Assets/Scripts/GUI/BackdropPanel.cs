// Copyright 2023 The Open Brush Authors
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

namespace TiltBrush
{
    public enum BackdropMode
    {
        Gradient,
        Skybox,
        Passthrough
    }

    public class BackdropPanel : BasePanel
    {
        [SerializeField] private ToggleButton m_TogglePassthroughButton;
        public TextActionButton m_ButtonShowSkyboxControls;
        public TextActionButton m_ButtonShowGradientControls;
        public TextActionButton m_ButtonShowPassthroughControls;
        [SerializeField] BackdropMode m_CurrentBackdropMode;

        [SerializeField] private GameObject m_GradientControls;
        [SerializeField] private GameObject m_SkyboxControls;
        [SerializeField] private GameObject m_PassthroughControls;
        [SerializeField] MeshRenderer m_SkyboxPreview;
        [SerializeField] Material m_SkyboxPreviewMaterial;
        [SerializeField] Material m_PassthroughPreviewMaterial;

        private static readonly int Tex = Shader.PropertyToID("_Tex");

        public void TogglePassthrough()
        {
            SceneSettings.m_Instance.PassthroughEnabled = m_TogglePassthroughButton.IsToggledOn;
        }

        public override void InitPanel()
        {
            base.InitPanel();
            SceneSettings.m_Instance.BackdropModeChanged += OnBackdropModeChanged;
            m_TogglePassthroughButton.IsToggledOn = SceneSettings.m_Instance.PassthroughEnabled;
            DetectCurrentBackdropMode();
            OnBackdropModeChanged();
        }

        private void DetectCurrentBackdropMode()
        {
            if (SceneSettings.m_Instance.PassthroughEnabled)
            {
                m_CurrentBackdropMode = BackdropMode.Passthrough;
            }
            else if (SceneSettings.m_Instance.HasSkybox)
            {
                m_CurrentBackdropMode = BackdropMode.Skybox;
            }
            else if (SceneSettings.m_Instance.InGradient)
            {
                m_CurrentBackdropMode = BackdropMode.Gradient;
            }
            else
            {
                Debug.LogError("Unable to detect valid Backdrop Mode");
                m_CurrentBackdropMode = BackdropMode.Gradient;
            }
        }

        void OnDestroy()
        {
            SceneSettings.m_Instance.BackdropModeChanged -= OnBackdropModeChanged;
        }

        public void HandleChangeModeToGradient() => ChangeBackdropMode(BackdropMode.Gradient);

        public void HandleChangeModeToSkybox() => ChangeBackdropMode(BackdropMode.Skybox);

        public void HandleChangeModeToPassthrough() => ChangeBackdropMode(BackdropMode.Passthrough);

        void ChangeBackdropMode(BackdropMode newMode)
        {
            switch (newMode)
            {
                case BackdropMode.Gradient:
                    SceneSettings.m_Instance.ClearCustomSkybox();
                    SceneSettings.m_Instance.PassthroughEnabled = false;
                    if (!SceneSettings.m_Instance.InGradient)
                    {
                        SketchMemoryScript.m_Instance.PerformAndRecordCommand(new UnlockSkyboxCommand());
                    }
                    break;
                case BackdropMode.Skybox:
                    SceneSettings.m_Instance.InGradient = false;
                    SceneSettings.m_Instance.PassthroughEnabled = false;
                    if (SceneSettings.m_Instance.CurrentEnvironment.HasSkybox)
                    {
                        SceneSettings.m_Instance.ReapplyEnvironmentSkybox();
                    }
                    else
                    {
                        SceneSettings.m_Instance.LoadCustomSkybox(BackgroundImageCatalog.m_Instance.IndexToImage(0).FileName);
                    }
                    break;
                case BackdropMode.Passthrough:
                    SceneSettings.m_Instance.ClearCustomSkybox();
                    SceneSettings.m_Instance.InGradient = false;
                    SceneSettings.m_Instance.PassthroughEnabled = true;
                    break;
            }
            OnBackdropModeChanged();
        }

        void OnBackdropModeChanged()
        {
            DetectCurrentBackdropMode();
            switch (m_CurrentBackdropMode)
            {
                case BackdropMode.Gradient:
                    m_ButtonShowGradientControls.SetButtonSelected(true);
                    m_ButtonShowSkyboxControls.SetButtonSelected(false);
                    m_ButtonShowPassthroughControls.SetButtonSelected(false);
                    m_GradientControls.SetActive(true);
                    m_SkyboxControls.SetActive(false);
                    m_PassthroughControls.SetActive(false);
                    break;
                case BackdropMode.Skybox:
                    m_ButtonShowGradientControls.SetButtonSelected(false);
                    m_ButtonShowSkyboxControls.SetButtonSelected(true);
                    m_ButtonShowPassthroughControls.SetButtonSelected(false);
                    m_GradientControls.SetActive(false);
                    m_SkyboxControls.SetActive(true);
                    m_PassthroughControls.SetActive(false);
                    m_SkyboxPreview.material = m_SkyboxPreviewMaterial;
                    m_SkyboxPreview.material.mainTexture = SceneSettings.m_Instance.CurrentSkyboxMaterial.GetTexture(Tex);
                    break;
                case BackdropMode.Passthrough:
                    m_ButtonShowGradientControls.SetButtonSelected(false);
                    m_ButtonShowSkyboxControls.SetButtonSelected(false);
                    m_ButtonShowPassthroughControls.SetButtonSelected(true);
                    m_GradientControls.SetActive(false);
                    m_SkyboxControls.SetActive(false);
                    m_PassthroughControls.SetActive(true);
                    m_SkyboxPreview.material = m_PassthroughPreviewMaterial;
                    break;
            }
        }
    }
} // namespace TiltBrush
