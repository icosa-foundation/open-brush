// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using UnityEngine;
namespace TiltBrush
{
    internal sealed class SetVoxBrushColorCommand : BaseCommand
    {
        private readonly Color m_Before;
        private Color m_After;
        private readonly object m_Gesture;
        internal SetVoxBrushColorCommand(Color color, object gesture)
        { m_Before = App.BrushColor.CurrentColor; m_After = color; m_Gesture = gesture; }
        public override bool NeedsSave => false;
        protected override void OnUndo() => App.BrushColor.CurrentColor = m_Before;
        protected override void OnRedo() => App.BrushColor.CurrentColor = m_After;
        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) return true;
            if (!(other is SetVoxBrushColorCommand edit) || m_Gesture == null || !ReferenceEquals(m_Gesture, edit.m_Gesture)) return false;
            m_After = edit.m_After; return true;
        }
    }
}
