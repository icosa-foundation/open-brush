using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Mirror movement endpoints, shared by live dragging and settings commands.
    internal sealed class MirrorWidgetEdits
    {
        private sealed class Member
        {
            internal GrabWidget Widget;
            internal WidgetPlacement Before, After;
            internal int Index;
        }
        private readonly List<Member> m_Members = new List<Member>();
        private readonly SymmetrySettingsSnapshot m_Before;

        internal MirrorWidgetEdits(SymmetryMirror mirror, SymmetrySettingsSnapshot before)
        {
            m_Before = before;
            foreach (var group in SymmetryWidgetGroup.GroupsOf(mirror))
            {
                foreach (var widget in group.ActiveMembers)
                {
                    var state = WidgetPlacement.Capture(widget);
                    m_Members.Add(new Member
                    {
                        Widget = widget, Before = state, After = state, Index = widget.SymmetryPointerIndex
                    });
                }
            }
        }

        internal void Update(SymmetrySettingsSnapshot after, bool holdFirstPointer)
        {
            foreach (var member in m_Members)
            {
                int index = member.Index;
                if (member.Widget == null || index >= after.PointerTransforms.Count ||
                    index >= m_Before.PointerTransforms.Count || (holdFirstPointer && index == 0)) { continue; }
                var step = after.PointerTransforms[index] * m_Before.PointerTransforms[index].inverse;
                var state = member.Before;
                state.Transform = step * state.Transform;
                if (!state.Transform.IsFinite()) { continue; }
                member.After = state;
                state.Restore(member.Widget);
            }
        }

        internal void Restore(bool after)
        {
            foreach (var member in m_Members)
            {
                if (member.Widget != null) { (after ? member.After : member.Before).Restore(member.Widget); }
            }
        }
    }

    /// Retains surviving objects and owns additions/removals for undo and redo.
    internal sealed class RegenerateMirrorWidgetsCommand : BaseCommand
    {
        private sealed class Member
        {
            internal GrabWidget Widget;
            internal SymmetryWidgetGroup Group;
            internal int Index;
            internal WidgetPlacement Before, After;
            internal bool Added, Removed;
        }
        private readonly List<Member> m_Members = new List<Member>();
        private bool m_Applied;

        internal RegenerateMirrorWidgetsCommand(SymmetryMirror mirror,
            SymmetrySettingsSnapshot before, SymmetrySettingsSnapshot after, BaseCommand parent) : base(parent)
        {
            foreach (var group in SymmetryWidgetGroup.GroupsOf(mirror).ToList())
            {
                var members = group.ActiveMembers.OrderBy(w => w.SymmetryPointerIndex).ToList();
                var source = members.FirstOrDefault();
                if (source == null) { continue; }
                var canonical = before.PointerTransforms[source.SymmetryPointerIndex].inverse * source.LocalTransform;
                var byIndex = members.ToDictionary(w => w.SymmetryPointerIndex);
                foreach (var widget in members)
                {
                    int index = widget.SymmetryPointerIndex;
                    var state = WidgetPlacement.Capture(widget);
                    var next = state;
                    bool removed = index >= after.PointerTransforms.Count;
                    if (!removed) { next.Transform = after.PointerTransforms[index] * canonical; }
                    m_Members.Add(new Member
                    {
                        Widget = widget, Group = group, Index = index, Before = state, After = next, Removed = removed
                    });
                }
                for (int i = 0; i < after.PointerTransforms.Count; ++i)
                {
                    if (byIndex.ContainsKey(i)) { continue; }
                    var copy = source.Clone();
                    copy.SetCanvas(group.Mirror.Canvas);
                    var state = WidgetPlacement.Capture(copy);
                    state.Transform = after.PointerTransforms[i] * canonical;
                    state.Restore(copy);
                    copy.Hide();
                    TiltMeterScript.m_Instance.AdjustMeterWithWidget(copy.GetTiltMeterCost(), up: false);
                    m_Members.Add(new Member { Widget = copy, Group = group, Index = i, After = state, Added = true });
                }
            }
        }

        protected override void OnRedo()
        {
            foreach (var member in m_Members)
            {
                if (member.Removed)
                {
                    member.Widget.Hide();
                    member.Widget.SetSymmetryGroup(null, -1);
                    TiltMeterScript.m_Instance.AdjustMeterWithWidget(member.Widget.GetTiltMeterCost(), up: false);
                }
                else
                {
                    if (member.Added)
                    {
                        member.Widget.RestoreFromToss();
                        member.Widget.SetSymmetryGroup(member.Group, member.Index);
                        TiltMeterScript.m_Instance.AdjustMeterWithWidget(member.Widget.GetTiltMeterCost(), up: true);
                    }
                    member.After.Restore(member.Widget);
                }
            }
            m_Applied = true;
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
        }

        protected override void OnUndo()
        {
            foreach (var member in m_Members)
            {
                if (member.Added)
                {
                    member.Widget.Hide();
                    member.Widget.SetSymmetryGroup(null, -1);
                    TiltMeterScript.m_Instance.AdjustMeterWithWidget(member.Widget.GetTiltMeterCost(), up: false);
                }
                else
                {
                    if (member.Removed)
                    {
                        member.Widget.RestoreFromToss();
                        member.Widget.SetSymmetryGroup(member.Group, member.Index);
                        TiltMeterScript.m_Instance.AdjustMeterWithWidget(member.Widget.GetTiltMeterCost(), up: true);
                    }
                    member.Before.Restore(member.Widget);
                }
            }
            m_Applied = false;
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
        }

        protected override void OnDispose()
        {
            foreach (var member in m_Members)
            {
                if ((member.Added && !m_Applied) || (member.Removed && m_Applied))
                { Object.Destroy(member.Widget.gameObject); }
            }
        }
    }
}
