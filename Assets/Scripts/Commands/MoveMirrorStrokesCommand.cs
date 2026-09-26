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

using System.Collections.Generic;

namespace TiltBrush
{
    /// Owns a mirror drag from grab to release, including its final widget snap.
    public class MoveMirrorStrokesCommand : BaseCommand
    {
        private readonly SymmetryWidget m_Widget;
        private readonly MoveWidgetCommand m_WidgetMove;
        private readonly SymmetryMirror m_Mirror;
        private readonly SymmetrySettingsSnapshot m_Before;
        private SymmetrySettingsSnapshot m_After;
        private readonly List<SymmetryMirrorMove.GroupMove> m_Groups;
        private readonly List<SymmetryPeerEditing.BrokenLink> m_SkippedLinks;
        private bool m_BrokeSkippedLinks;
        private bool m_Complete;

        internal MoveMirrorStrokesCommand(SymmetryWidget widget, SymmetryMirror mirror,
            SymmetrySettingsSnapshot before, List<SymmetryMirrorMove.GroupMove> groups,
            List<SymmetryPeerEditing.BrokenLink> skippedLinks)
        {
            m_Widget = widget;
            m_WidgetMove = new MoveWidgetCommand(widget, widget.LocalTransform,
                widget.CustomDimension, final: true);
            m_Mirror = mirror;
            m_Before = before;
            m_After = before;
            m_Groups = groups;
            m_SkippedLinks = skippedLinks;
        }

        public override bool NeedsSave => true;

        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) { return true; }
            if (m_Complete) { return false; }
            if (other is MoveWidgetCommand move && move.Widget == m_Widget)
            {
                m_WidgetMove.CopyMirrorEnd(move);
                return true;
            }
            // Close before a different operation changes any of the captured state.
            SymmetryMirrorMove.End();
            return false;
        }

        internal void Complete(SymmetrySettingsSnapshot after, bool brokeSkippedLinks)
        {
            if (m_Complete) { return; }
            m_WidgetMove.UpdateMirrorEnd(m_Widget.LocalTransform, m_Widget.CustomDimension);
            m_After = after;
            m_BrokeSkippedLinks = brokeSkippedLinks;
            foreach (var group in m_Groups) { group.Complete(); }
            m_Complete = true;
        }

        protected override void OnRedo()
        {
            if (m_BrokeSkippedLinks)
            {
                foreach (var link in m_SkippedLinks) { link.Break(); }
            }
            foreach (var group in m_Groups) { group.Restore(after: true); }
            m_WidgetMove.Redo();
            m_Mirror.Settings = m_After;
        }

        protected override void OnUndo()
        {
            // Undo may interrupt a held mirror.
            if (!m_Complete) { SymmetryMirrorMove.End(); }
            foreach (var group in m_Groups) { group.Restore(after: false); }
            m_WidgetMove.Undo();
            m_Mirror.Settings = m_Before;
            if (m_BrokeSkippedLinks)
            {
                foreach (var link in m_SkippedLinks) { link.Restore(); }
            }
        }
    }
} // namespace TiltBrush
