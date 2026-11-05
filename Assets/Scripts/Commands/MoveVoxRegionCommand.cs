// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System;
using System.Collections.Generic;

namespace TiltBrush
{
    internal sealed class MoveVoxRegionCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private readonly VoxRegionSelection m_Before, m_After;
        private readonly IReadOnlyList<VoxCellChange> m_Cells;
        private readonly RuntimeVoxDocument.RuntimeModel m_Model;
        private readonly VoxRegionLift m_Lift;
        internal MoveVoxRegionCommand(ModelWidget widget, VoxRegionSelection before, VoxRegionSelection after,
            IReadOnlyList<VoxCellChange> cells = null, BaseCommand parent = null, VoxRegionLift lift = null) : base(parent)
        {
            m_Widget = widget;
            m_Before = before;
            m_After = after;
            m_Model = (before ?? after).Model;
            m_Cells = cells ?? Array.Empty<VoxCellChange>();
            m_Lift = lift;
        }
        public override bool NeedsSave => m_Lift != null || m_Cells.Count != 0 ||
            (m_Before != null && m_After != null && m_Before.Floating.Pose != m_After.Floating.Pose);
        public override bool IsAvailable => m_Widget != null;
        private void Apply(bool after)
        {
            if (m_Lift != null)
            {
                if (after) m_Lift.Redo();
                else m_Lift.Undo();
            }
            else VoxCellChange.Apply(m_Model, m_Cells, after);
            m_Widget.ApplyVoxRegionState(after ? m_After : m_Before);
            m_Widget.RefreshVoxRegionVisuals(m_Lift != null || m_Cells.Count != 0);
            SaveLoadScript.m_Instance?.SketchChanged();
        }
        protected override void OnUndo() => Apply(false);
        protected override void OnRedo() => Apply(true);
    }
}
