using System;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush
{
    /// Command entry point for existing widget content controls.
    internal static class WidgetPropertyCommand
    {
        internal static void Set<TWidget, TValue>(TWidget driver, string property, TValue value,
            Func<TWidget, TValue> read, Action<TWidget, TValue> write) where TWidget : GrabWidget
        {
            if (driver.SymmetryPeerGroup == null) { write(driver, value); return; }
            SymmetryPeerPreview.Hide();
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new Change<TWidget, TValue>(driver, property, value, read, write));
        }

        private sealed class Change<TWidget, TValue> : BaseCommand where TWidget : GrabWidget
        {
            private readonly TWidget m_Driver;
            private readonly string m_Property;
            private readonly List<TWidget> m_Widgets;
            private readonly List<TValue> m_Before;
            private readonly Action<TWidget, TValue> m_Write;
            private TValue m_After;

            internal Change(TWidget driver, string property, TValue value,
                Func<TWidget, TValue> read, Action<TWidget, TValue> write)
            {
                m_Driver = driver;
                m_Property = property;
                m_Widgets = driver.SymmetryPeerGroup.ActiveMembers.OfType<TWidget>().ToList();
                m_Before = m_Widgets.Select(read).ToList();
                m_Write = write;
                m_After = value;
            }

            public override bool NeedsSave => true;
            protected override void OnRedo()
            {
                foreach (var widget in m_Widgets) { m_Write(widget, m_After); }
            }
            protected override void OnUndo()
            {
                for (int i = 0; i < m_Widgets.Count; ++i) { m_Write(m_Widgets[i], m_Before[i]); }
            }
            public override bool Merge(BaseCommand other)
            {
                if (other is Change<TWidget, TValue> next && next.m_Driver == m_Driver &&
                    next.m_Property == m_Property)
                {
                    m_After = next.m_After;
                    return true;
                }
                return base.Merge(other);
            }
        }
    }
}
