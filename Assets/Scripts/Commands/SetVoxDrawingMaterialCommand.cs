// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System.Collections.Generic;

namespace TiltBrush
{
    internal sealed class SetVoxDrawingMaterialCommand : BaseCommand
    {
        private readonly VoxPaintSettings m_Settings;
        private readonly IReadOnlyDictionary<string, string> m_Before;
        private IReadOnlyDictionary<string, string> m_After;
        private readonly object m_Gesture;
        internal SetVoxDrawingMaterialCommand(VoxPaintSettings settings,
            IReadOnlyDictionary<string, string> material, object gesture)
        {
            m_Settings = settings; m_Before = settings.DrawingMaterial; m_After = material; m_Gesture = gesture;
        }
        public override bool NeedsSave => false;
        protected override void OnUndo() => m_Settings.DrawingMaterial = m_Before;
        protected override void OnRedo() => m_Settings.DrawingMaterial = m_After;
        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) return true;
            if (!(other is SetVoxDrawingMaterialCommand edit) || edit.m_Settings != m_Settings ||
                m_Gesture == null || !ReferenceEquals(m_Gesture, edit.m_Gesture)) return false;
            m_After = edit.m_After; return true;
        }
    }
}
