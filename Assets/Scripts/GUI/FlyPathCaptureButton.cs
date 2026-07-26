// Copyright 2024 The Open Brush Authors
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
    public class FlyPathCaptureButton : BaseButton
    {
        [SerializeField] private string m_CaptureDescription = "Record Flight as Camera Path";
        [SerializeField] private string m_StopDescription = "Stop Recording Flight";

        protected override void Awake()
        {
            base.Awake();
            App.Switchboard.ToolChanged += UpdateVisuals;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            App.Switchboard.ToolChanged -= UpdateVisuals;
        }

        public override void UpdateVisuals()
        {
            base.UpdateVisuals();

            bool wasRecording = m_ToggleActive;
            m_ToggleActive = FlyPathCapture.IsRecording;
            if (wasRecording != m_ToggleActive)
            {
                SetButtonActivated(m_ToggleActive);
                SetDescriptionText(m_ToggleActive ? m_StopDescription : m_CaptureDescription);
            }
        }

        protected override void OnButtonPressed()
        {
            if (FlyPathCapture.IsRecording)
            {
                FlyPathCapture.StopRecordingAndCreatePath();
            }
            else
            {
                FlyPathCapture.StartRecording();
            }

            UpdateVisuals();
        }
    }
}
