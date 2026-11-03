// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

namespace TiltBrush
{
    // Floating geometry and stamp history are shared immutable references. Recording
    // or replaying a movement must not copy/rebuild the composed voxel document.
    internal sealed class MoveVoxRegionCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private readonly VoxRegionSelection m_Before, m_After;

        internal MoveVoxRegionCommand(ModelWidget widget, VoxRegionSelection before,
            VoxRegionSelection after, BaseCommand parent = null) : base(parent)
        {
            m_Widget = widget;
            m_Before = before;
            m_After = after;
        }

        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;
        protected override void OnUndo()
        {
            m_Widget.VoxSelection = m_Before;
            SaveLoadScript.m_Instance?.SketchChanged();
        }
        protected override void OnRedo()
        {
            m_Widget.VoxSelection = m_After;
            SaveLoadScript.m_Instance?.SketchChanged();
        }
    }
}