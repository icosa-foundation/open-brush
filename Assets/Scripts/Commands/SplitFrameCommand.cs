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
    public class SplitFrameCommand : BaseCommand
    {
        private (int,int) m_TimelineLocation;
        private (int,int) m_SplittingIndex;
        AnimationUI_Manager m_Manager;
        bool m_ExpandTimeline;
        bool m_JustMoved = true;
        int m_FrameOnStart;
        AnimationUI_Manager.DeletedFrame m_DeletedFrame;

        public SplitFrameCommand()
        {
           m_Manager = App.Scene.animationUI_manager;
           m_TimelineLocation = m_Manager.GetCanvasLocation(App.Scene.ActiveCanvas);
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            m_SplittingIndex = m_Manager.splitKeyFrame(m_TimelineLocation.Item1,m_TimelineLocation.Item2);
        }

        protected override void OnUndo()
        {
            if (m_SplittingIndex.Item1 == -1 || m_SplittingIndex.Item2 == -1) return;

            int followingLength = m_Manager.GetFrameLength(m_SplittingIndex.Item1,m_SplittingIndex.Item2);
            CanvasScript previousCanvas = m_Manager.Timeline[m_SplittingIndex.Item1].Frames[m_SplittingIndex.Item2 - 1].Canvas;

            for (int i = 0; i < followingLength; i++)
            {
                AnimationUI_Manager.Frame differentFrame = m_Manager.Timeline[m_SplittingIndex.Item1].Frames[m_SplittingIndex.Item2 + i];
                differentFrame.Canvas = previousCanvas;
                m_Manager.Timeline[m_SplittingIndex.Item1].Frames[m_SplittingIndex.Item2 + i] = differentFrame;
            }
            m_Manager.SelectTimelineFrame(m_SplittingIndex.Item1,m_SplittingIndex.Item2);
            m_Manager.FillandCleanTimeline();
            m_Manager.ResetTimeline();
        }
    }
} // namespace TiltBrush.FrameAnimation
