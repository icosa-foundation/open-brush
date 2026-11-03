// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

namespace TiltBrush
{
    // Selecting/finalizing changes runtime layer ownership, not the composed save
    // image. Undoing finalization restores the covered background with the layer.
    internal sealed class SetVoxRegionSelectionCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private readonly VoxRegionSelection m_Before, m_After;

        internal SetVoxRegionSelectionCommand(ModelWidget widget, VoxRegionSelection after)
        {
            m_Widget = widget;
            m_Before = widget.VoxSelection;
            m_After = after;
        }

        public override bool IsAvailable => m_Widget != null;
        protected override void OnUndo() { m_Widget.ApplyVoxRegionState(m_Before); m_Widget.RefreshVoxRegionVisuals(false); }
        protected override void OnRedo() { m_Widget.ApplyVoxRegionState(m_After); m_Widget.RefreshVoxRegionVisuals(false); }
    }
}
