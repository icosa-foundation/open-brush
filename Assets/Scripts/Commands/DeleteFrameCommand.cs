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

namespace TiltBrush.FrameAnimation
{
    public class DeleteFrameCommand : BaseCommand
    {
        private (int,int) m_TimelineLocation;
        private (int,int) m_InsertingAt;
        AnimationUI_Manager m_Manager;
        bool m_ExpandTimeline;
        bool m_JustMoved = true;
        int m_FrameOnStart;
        AnimationUI_Manager.DeletedFrame m_DeletedFrame;

        public DeleteFrameCommand()
        {
           m_Manager = App.Scene.animationUI_manager;
           m_TimelineLocation = m_Manager.GetCanvasLocation(App.Scene.ActiveCanvas);
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            m_DeletedFrame = m_Manager.RemoveKeyFrame(m_TimelineLocation.Item1,m_TimelineLocation.Item2);
        }

        protected override void OnUndo()
        {
            for (int i = 0; i< m_DeletedFrame.Length;i++)
            {
                if (m_DeletedFrame.Location.Item2 + i >= m_Manager.Timeline[m_DeletedFrame.Location.Item1].Frames.Count)
                {
                     m_Manager.Timeline[m_DeletedFrame.Location.Item1].Frames.Add(m_DeletedFrame.Frame);
                }
                else
                {
                    m_Manager.Timeline[m_DeletedFrame.Location.Item1].Frames[m_DeletedFrame.Location.Item2 + i] = m_DeletedFrame.Frame;
                }
            }
            m_Manager.ResetTimeline();
            m_Manager.SelectTimelineFrame(m_DeletedFrame.Location.Item1,m_DeletedFrame.Location.Item2);
        }
    }
} // namespace TiltBrush.FrameAnimation
