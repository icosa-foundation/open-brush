using System;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush
{
    /// Command entry point for existing widget content controls.
    internal static class WidgetPropertyCommand
    {
        internal static TWidget[] GetTargets<TWidget>(TWidget driver) where TWidget : GrabWidget =>
            driver.SymmetryPeerGroup == null ? new[] { driver } : driver.SymmetryPeerGroup.ActiveMembers
                .OfType<TWidget>().Prepend(driver).Distinct().ToArray();

        internal static void Set<TWidget, TValue>(TWidget driver, string property, TValue value,
            Func<TWidget, TValue> read, Action<TWidget, TValue> write) where TWidget : GrabWidget
        {
            if (driver.SymmetryPeerGroup == null)
            {
                TValue before = read(driver);
                write(driver, value);
                if (!EqualityComparer<TValue>.Default.Equals(before, read(driver)))
                    SketchMemoryScript.m_Instance.MarkUnrecordedChange();
                return;
            }
            SymmetryPeerPreview.Hide();
            var command = new Change<TWidget, TValue>(driver, property, value, read, write);
            if (command.HasChanges) SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
        }

        private sealed class Change<TWidget, TValue> : WidgetPropertyCommand<TWidget, TValue> where TWidget : GrabWidget
        {
            private readonly Action<TWidget, TValue> m_Write;

            internal Change(TWidget driver, string property, TValue value,
                Func<TWidget, TValue> read, Action<TWidget, TValue> write)
                : base(driver, value, read, property: property, mergeWithoutGesture: true)
            {
                m_Write = write;
            }

            protected override void Apply(IReadOnlyList<TWidget> widgets, TValue value)
            {
                foreach (var widget in widgets) { m_Write(widget, value); }
            }
        }
    }

    /// Base for user-initiated widget content edits. Raw widget setters remain local so loading,
    /// cloning and command replay cannot recursively propagate edits or record extra commands.
    /// Apply receives the whole batch: prepare expensive resources once, then assign them to peers.
    public abstract class WidgetPropertyCommand<TWidget, TValue> : BaseCommand where TWidget : GrabWidget
    {
        protected readonly TWidget m_Driver;
        private readonly TWidget[] m_Widgets;
        private readonly TValue[] m_Before;
        private TValue m_After;
        private readonly string m_Property;
        private readonly object m_Gesture;
        private readonly bool m_MergeWithoutGesture;

        protected WidgetPropertyCommand(TWidget driver, TValue value, Func<TWidget, TValue> read,
            object gesture = null, BaseCommand parent = null, string property = null, bool mergeWithoutGesture = false)
            : base(parent)
        {
            m_Driver = driver;
            m_Widgets = WidgetPropertyCommand.GetTargets(driver);
            m_Before = m_Widgets.Select(read).ToArray();
            m_After = value;
            m_Property = property;
            m_Gesture = gesture;
            m_MergeWithoutGesture = mergeWithoutGesture;
        }

        internal bool HasChanges => m_Before.Any(before =>
            !EqualityComparer<TValue>.Default.Equals(before, m_After));

        public override bool NeedsSave => true;
        protected abstract void Apply(IReadOnlyList<TWidget> widgets, TValue value);
        protected virtual object GetUndoKey(TValue value) => value;

        protected override void OnRedo() => Apply(m_Widgets.Where(widget => widget != null).ToArray(), m_After);

        protected override void OnUndo()
        {
            // Restore heterogeneous prior state, preparing once per distinct prior value.
            foreach (var group in m_Widgets.Select((widget, i) => (Widget: widget, Value: m_Before[i]))
                         .Where(before => before.Widget != null).GroupBy(before => GetUndoKey(before.Value)))
            {
                Apply(group.Select(before => before.Widget).ToArray(), group.First().Value);
            }
        }

        public override bool Merge(BaseCommand other)
        {
            if (other is WidgetPropertyCommand<TWidget, TValue> next && next.GetType() == GetType() &&
                next.m_Driver == m_Driver && next.m_Property == m_Property &&
                m_Widgets.SequenceEqual(next.m_Widgets) &&
                ((m_Gesture != null && ReferenceEquals(m_Gesture, next.m_Gesture)) ||
                 (m_Gesture == null && next.m_Gesture == null && m_MergeWithoutGesture && next.m_MergeWithoutGesture)))
            {
                m_After = next.m_After;
                return true;
            }
            return base.Merge(other);
        }
    }
}
