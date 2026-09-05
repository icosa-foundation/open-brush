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
    public class SoundClipButton : BaseButton
    {
        private SoundClip m_SoundClip;

        override protected void OnButtonPressed()
        {
            base.OnButtonPressed();
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.SoundClipWidgetPrefab, TrTransform.FromTransform(transform), null,
                false, SelectionManager.m_Instance.SnappingGridSize, SelectionManager.m_Instance.SnappingAngle
            );
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);

            SoundClipWidget soundClipWidget = createCommand.Widget as SoundClipWidget;
            soundClipWidget.SetSoundClip(m_SoundClip);
            soundClipWidget.Show(true);
            createCommand.SetWidgetCost(soundClipWidget.GetTiltMeterCost());

            WidgetManager.m_Instance.WidgetsDormant = false;
            SketchControlsScript.m_Instance.EatGazeObjectInput();
            SelectionManager.m_Instance.RemoveFromSelection(false);
        }

        public void RefreshDescription()
        {
            if (m_SoundClip != null)
            {
                SetDescriptionText(m_SoundClip.HumanName);
            }
        }

        public SoundClip SoundClip
        {
            get { return m_SoundClip; }
            set
            {
                m_SoundClip = value;
                SetButtonTexture(m_SoundClip.Thumbnail);
            }
        }
    }
}
