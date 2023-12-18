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

namespace TiltBrush.FrameAnimation
{
    public class AddFrameCommand : BaseCommand
    {
        private (int,int) m_TimelineLocation;
        private (int,int) m_InsertingAt;
        AnimationUI_Manager m_Manager;

        bool m_ExpandTimeline;
        bool m_JustMoved = true;

        int m_FrameOnStart;

        public AddFrameCommand()
        {
           m_Manager = App.Scene.animationUI_manager;
           m_TimelineLocation = m_Manager.GetCanvasLocation(App.Scene.ActiveCanvas);
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            m_InsertingAt = m_Manager.AddKeyFrame(m_TimelineLocation.Item1,m_TimelineLocation.Item2);
        }

        protected override void OnUndo()
        {
            Object.Destroy( m_Manager.Timeline[m_InsertingAt.Item1].Frames[m_InsertingAt.Item2].Canvas);
            m_Manager.Timeline[m_InsertingAt.Item1].Frames.RemoveAt(m_InsertingAt.Item2);
            m_Manager.FillTimeline();
            m_Manager.SelectTimelineFrame(m_TimelineLocation.Item1,m_TimelineLocation.Item2);
        }
    }
} // namespace TiltBrush.FrameAnimation
