using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// Endpoint-based movement of unselected peers. The driver is moved by its parent command.
    public sealed class TransformWidgetPeersCommand : BaseCommand
    {
        private readonly List<GrabWidget> m_Widgets = new List<GrabWidget>();
        private readonly List<WidgetPlacement> m_Before = new List<WidgetPlacement>();
        private readonly List<WidgetPlacement> m_After = new List<WidgetPlacement>();

        public TransformWidgetPeersCommand(GrabWidget source, TrTransform delta,
            BaseCommand parent = null, Vector3? dimensions = null) : base(parent)
        {
            if (source.SymmetryPeerGroup == null || !delta.IsFinite()) { return; }
            foreach (var peer in source.SymmetryPeerGroup.ActiveMembers)
            {
                if (peer == source || SelectionManager.m_Instance.IsWidgetSelected(peer) ||
                    !SymmetryWidgetGroup.TryGetPeerTransform(source, peer, out var toPeer)) { continue; }
                var before = WidgetPlacement.Capture(peer);
                var after = before;
                after.Transform = toPeer * delta * toPeer.inverse * before.Transform;
                if (dimensions.HasValue) { after.Dimensions = dimensions.Value; }
                if (!after.Transform.IsFinite()) { continue; }
                m_Widgets.Add(peer);
                m_Before.Add(before);
                m_After.Add(after);
            }
        }

        internal void UpdateEnd(TransformWidgetPeersCommand next)
        {
            for (int i = 0; i < m_Widgets.Count; ++i)
            {
                int index = next.m_Widgets.IndexOf(m_Widgets[i]);
                if (index >= 0) { m_After[i] = next.m_After[index]; }
            }
        }

        public override bool NeedsSave => m_Widgets.Count > 0;

        internal static void ForEdits(IEnumerable<(GrabWidget widget, TrTransform delta)> edits, BaseCommand parent)
        {
            var groups = new HashSet<SymmetryWidgetGroup>();
            foreach (var (widget, delta) in edits)
            {
                var group = widget.SymmetryPeerGroup;
                if (group == null || !groups.Add(group)) { continue; }
                var basis = group.Mirror.Canvas.Pose.inverse * widget.Canvas.Pose;
                new TransformWidgetPeersCommand(widget, basis * delta * basis.inverse, parent);
            }
        }
        protected override void OnRedo() { Restore(m_After); }
        protected override void OnUndo() { Restore(m_Before); }

        private void Restore(List<WidgetPlacement> states)
        {
            for (int i = 0; i < m_Widgets.Count; ++i)
            {
                if (m_Widgets[i] != null) { SymmetryWidgetPreview.RestorePlacement(m_Widgets[i], states[i]); }
            }
        }
    }
}
