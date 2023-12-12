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

namespace TiltBrush
{
    public class UnlockSkyboxCommand : BaseCommand
    {

        public string m_previousSkybox = SceneSettings.m_Instance.CustomSkybox;
        public bool m_previousPassthroughState = SceneSettings.m_Instance.PassthroughEnabled;

        public override bool NeedsSave { get { return base.NeedsSave; } }

        protected override void OnRedo()
        {
            SceneSettings.m_Instance.ClearCustomSkybox();
            SceneSettings.m_Instance.PassthroughEnabled = false;
            SceneSettings.m_Instance.InGradient = true;
        }

        protected override void OnUndo()
        {
            if (!string.IsNullOrEmpty(m_previousSkybox)) SceneSettings.m_Instance.LoadCustomSkybox(m_previousSkybox);
            SceneSettings.m_Instance.PassthroughEnabled = m_previousPassthroughState;
            SceneSettings.m_Instance.InGradient = false;
        }
    }
} // namespace TiltBrush
