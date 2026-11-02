// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

namespace TiltBrush
{
    // Selection coordinates and voxel snapshots must travel through undo together,
    // because growing bounds rebases cell indices without moving the artwork.
    internal sealed class MoveVoxRegionCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private readonly VoxRegionSelection m_Before, m_After;

        internal MoveVoxRegionCommand(ModelWidget widget, RuntimeVoxDocument.Snapshot before,
            RuntimeVoxDocument.Snapshot after, VoxRegionSelection beforeSelection,
            VoxRegionSelection afterSelection, BaseCommand parent = null) : base(parent)
        {
            m_Widget = widget;
            m_Before = beforeSelection;
            m_After = afterSelection;
            _ = new ModifyVoxDocumentCommand(widget, before, after, this);
        }

        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;
        protected override void OnUndo() => m_Widget.VoxSelection = m_Before;
        protected override void OnRedo() => m_Widget.VoxSelection = m_After;
    }
}
