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
        private (int, int) m_TimelineLocation;
        private AnimationUI_Manager.KeyFrameOperation m_Operation;
        private BaseCommand m_DeselectCommand;
        AnimationUI_Manager m_Manager;
        bool m_IsApplied;
        bool m_ExpandTimeline;
        bool m_JustMoved = true;
        int m_FrameOnStart;
        AnimationUI_Manager.DeletedFrame m_DeletedFrame;

        public DuplicateFrameCommand()
        {
            m_Manager = App.Scene.animationUI_manager;
            m_TimelineLocation = m_Manager.GetCanvasLocation(App.Scene.ActiveCanvas);
            if (SelectionManager.m_Instance.HasSelection)
            {
                m_DeselectCommand = SelectionManager.m_Instance.CreateEndSelectionCommand();
            }
        }

        public override bool NeedsSave => true;
        public override bool IsAvailable =>
            m_Manager.CanDuplicateKeyFrame(m_TimelineLocation.Item1, m_TimelineLocation.Item2);

        protected override void OnRedo()
        {
            // Settle selected content onto its authored canvases before copying the drawing.
            m_DeselectCommand?.Redo();
            m_Operation = m_Manager.DuplicateKeyFrame(m_TimelineLocation.Item1, m_TimelineLocation.Item2);
            m_IsApplied = m_Operation.Succeeded;
        }

        protected override void OnDispose()
        {
            m_DeselectCommand?.Dispose();
            if (m_IsApplied)
            {
                m_Manager.DiscardKeyFrameOperationUndoState(m_Operation);
            }
        }

        protected override void OnUndo()
        {
            m_Manager.UndoKeyFrameOperation(m_Operation, m_TimelineLocation);
            m_IsApplied = false;
            // Restore the selection only after the original drawing is current again.
            m_DeselectCommand?.Undo();
        }
    }
} // namespace TiltBrush.FrameAnimation
