// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
namespace TiltBrush
{
    internal sealed class SelectVoxPaletteEntryCommand : BaseCommand
    {
        private readonly VoxPaintSettings m_Settings;
        private readonly int m_Index;
        private readonly int m_PreviousIndex;
        private readonly int m_PreviousLastIndex;

        internal SelectVoxPaletteEntryCommand(VoxPaintSettings settings, int index, BaseCommand parent)
            : base(parent)
        {
            m_Settings = settings;
            m_Index = index;
            m_PreviousIndex = settings.PaletteIndex;
            m_PreviousLastIndex = settings.LastPaletteIndex;
        }

        public override bool NeedsSave => false;
        protected override void OnRedo()
        {
            m_Settings.PaletteIndex = m_Index;
            m_Settings.LastPaletteIndex = m_Index;
        }
        protected override void OnUndo()
        {
            m_Settings.PaletteIndex = m_PreviousIndex;
            m_Settings.LastPaletteIndex = m_PreviousLastIndex;
        }
    }
}
