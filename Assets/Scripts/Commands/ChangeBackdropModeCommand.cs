// Copyright 2026 The Open Brush Authors
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
    public class ChangeBackdropModeCommand : BaseCommand
    {
        private readonly BackdropMode m_PreviousMode;
        private readonly string m_PreviousSkybox;
        private readonly Color m_ColorA;
        private readonly Color m_ColorB;
        private readonly Quaternion m_Orientation;
        private readonly BackdropMode m_NextMode;
        private readonly string m_NextSkybox;

        public ChangeBackdropModeCommand(BackdropMode nextMode, string customSkybox = null)
        {
            var settings = SceneSettings.m_Instance;
            m_PreviousMode = settings.PassthroughEnabled ? BackdropMode.Passthrough :
                settings.InGradient ? BackdropMode.Gradient : BackdropMode.Skybox;
            m_PreviousSkybox = settings.CustomSkyboxPath;
            m_ColorA = settings.SkyColorA;
            m_ColorB = settings.SkyColorB;
            m_Orientation = settings.GradientOrientation;
            m_NextMode = nextMode;
            m_NextSkybox = customSkybox;
        }

        public override bool NeedsSave => true;

        public override bool IsAvailable => m_NextMode == BackdropMode.Passthrough
            ? SceneSettings.m_Instance.CanEnablePassthrough
            : SceneSettings.m_Instance.CanLeavePassthrough;

        protected override void OnRedo() => Apply(m_NextMode, m_NextSkybox);

        protected override void OnUndo() => Apply(m_PreviousMode, m_PreviousSkybox);

        private void Apply(BackdropMode mode, string customSkybox)
        {
            var settings = SceneSettings.m_Instance;
            if (mode == BackdropMode.Passthrough && !settings.CanEnablePassthrough) return;
            if (mode != BackdropMode.Passthrough && !settings.CanLeavePassthrough) return;

            bool passthrough = mode == BackdropMode.Passthrough;
            settings.PassthroughEnabled = passthrough;
            if (settings.PassthroughEnabled != passthrough) return;
            settings.ClearCustomSkybox();
            settings.InGradient = mode == BackdropMode.Gradient;
            if (mode == BackdropMode.Skybox)
            {
                if (!string.IsNullOrEmpty(customSkybox)) settings.LoadCustomSkybox(customSkybox);
                else settings.ReapplyEnvironmentSkybox();
            }
            settings.RestoreBackdropGradient(m_ColorA, m_ColorB, m_Orientation);
        }
    }
} // namespace TiltBrush
