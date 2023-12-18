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
    public class DuplicateFrameCommand : BaseCommand
    {
        private (int,int) m_TimelineLocation;
        private (int,int) m_DuplicatingIndex;
        AnimationUI_Manager m_Manager;
        bool m_ExpandTimeline;
        bool m_JustMoved = true;
        int m_FrameOnStart;
        AnimationUI_Manager.DeletedFrame m_DeletedFrame;

        public DuplicateFrameCommand()
        {
           m_Manager = App.Scene.animationUI_manager;
           m_TimelineLocation = m_Manager.GetCanvasLocation(App.Scene.ActiveCanvas);
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            m_DuplicatingIndex = m_Manager.duplicateKeyFrame(m_TimelineLocation.Item1,m_TimelineLocation.Item2);
        }

        protected override void OnUndo()
        {
            if (m_DuplicatingIndex.Item1 == -1 || m_DuplicatingIndex.Item2 == -1) return;
            m_Manager.RemoveKeyFrame(m_DuplicatingIndex.Item1,m_DuplicatingIndex.Item2);
            m_Manager.FillandCleanTimeline();
            m_Manager.SelectTimelineFrame(m_DuplicatingIndex.Item1,m_DuplicatingIndex.Item2 - 1);
            m_Manager.ResetTimeline();
        }
    }
} // namespace TiltBrush.FrameAnimation
