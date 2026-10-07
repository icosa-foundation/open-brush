using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Copies sharing editable content and placement relative to one mirror.
    public sealed class SymmetryWidgetGroup
    {
        private readonly List<GrabWidget> m_Members = new List<GrabWidget>();
        public SymmetryMirror Mirror { get; }
        public IReadOnlyList<GrabWidget> Members => m_Members;
        public IEnumerable<GrabWidget> ActiveMembers => m_Members.Where(w => w != null && w.IsAvailable());

        public SymmetryWidgetGroup(SymmetryMirror mirror) { Mirror = mirror; }

        internal void Add(GrabWidget widget)
        {
            Mirror.Canvas ??= EffectiveCanvas(widget);
            m_Members.Add(widget);
        }

        internal void Remove(GrabWidget widget) { m_Members.Remove(widget); }

        internal static CanvasScript EffectiveCanvas(GrabWidget widget) =>
            widget.Canvas == App.Scene.SelectionCanvas && widget.m_PreviousCanvas != null
                ? widget.m_PreviousCanvas : widget.Canvas;

        internal static IEnumerable<GrabWidget> ActiveWidgets =>
            WidgetManager.m_Instance.ActiveGrabWidgets.Select(data => data.m_WidgetScript)
                .Where(w => w != null && w.IsAvailable());

        internal static IEnumerable<SymmetryWidgetGroup> GroupsOf(SymmetryMirror mirror) =>
            ActiveWidgets.Select(w => w.SymmetryPeerGroup)
                .Where(g => g != null && ReferenceEquals(g.Mirror, mirror)).Distinct();

        internal static bool CanLink(GrabWidget widget) => widget is MediaWidget || widget is PortalWidgetBase || widget is GaussianCaptureWidgetBase;

        internal static bool HasLinkedUsers(Model model) => model != null && ActiveWidgets
            .OfType<ModelWidget>().Any(w => w.Model == model && w.SymmetryPeerGroup != null);

        internal static bool TryGetPeerTransform(GrabWidget source, GrabWidget peer, out TrTransform xf)
        {
            xf = TrTransform.identity;
            var group = source.SymmetryPeerGroup;
            var transforms = group?.Mirror.Settings?.PointerTransforms;
            int from = source.SymmetryPointerIndex, to = peer.SymmetryPointerIndex;
            if (group == null || peer.SymmetryPeerGroup != group || transforms == null ||
                from < 0 || to < 0 || from >= transforms.Count || to >= transforms.Count ||
                EffectiveCanvas(source) != group.Mirror.Canvas ||
                EffectiveCanvas(peer) != group.Mirror.Canvas) { return false; }
            xf = transforms[to] * transforms[from].inverse;
            return xf.IsFinite();
        }
    }

    internal struct WidgetPlacement
    {
        internal TrTransform Transform;
        internal Vector3 Dimensions;

        internal static WidgetPlacement Capture(GrabWidget widget) => new WidgetPlacement
        {
            Transform = widget.LocalTransform,
            Dimensions = widget.CustomDimension
        };

        internal void Restore(GrabWidget widget)
        {
            widget.LocalTransform = Transform;
            widget.CustomDimension = Dimensions;
        }
    }

    /// Temporary selection movement, restored before commands capture their endpoints.
    internal static class SymmetryWidgetPreview
    {
        private sealed class Peer
        {
            internal GrabWidget Widget;
            internal WidgetPlacement Before;
            internal TrTransform ToPeer, Joined, Applied;
        }
        private static readonly List<Peer> m_Peers = new List<Peer>();
        internal static bool IsShowing => m_Peers.Count > 0;

        internal static void Show()
        {
            Hide();
            var selection = SelectionManager.m_Instance;
            var groups = new HashSet<SymmetryWidgetGroup>();
            foreach (var source in selection.SelectedWidgets)
            {
                var group = source.SymmetryPeerGroup;
                if (group == null || !groups.Add(group)) { continue; }
                foreach (var peer in group.ActiveMembers)
                {
                    if (selection.IsWidgetSelected(peer) ||
                        !SymmetryWidgetGroup.TryGetPeerTransform(source, peer, out var toPeer)) { continue; }
                    m_Peers.Add(new Peer
                    {
                        Widget = peer, Before = WidgetPlacement.Capture(peer), ToPeer = toPeer,
                        Joined = selection.SelectionTransformWhenSelected(source)
                    });
                }
            }
        }

        internal static void Update(TrTransform selection)
        {
            foreach (var peer in m_Peers)
            {
                if (peer.Widget == null) { continue; }
                var moved = SymmetryPeerEditing.PeerSelectionMovement(peer.ToPeer, selection, peer.Joined);
                peer.Applied = moved;
                var state = peer.Before;
                state.Transform = moved * state.Transform;
                state.Restore(peer.Widget);
            }
        }

        internal static void Hide()
        {
            foreach (var peer in m_Peers)
            {
                if (peer.Widget != null) { peer.Before.Restore(peer.Widget); }
            }
            Forget();
        }

        internal static void Forget() { m_Peers.Clear(); }

        internal static void RestorePlacement(GrabWidget widget, WidgetPlacement placement)
        {
            var peer = m_Peers.Find(p => p.Widget == widget);
            if (peer != null)
            {
                peer.Before = placement;
                peer.Before.Transform = peer.Applied.inverse * placement.Transform;
            }
            placement.Restore(widget);
        }
    }
}
