// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using UnityEngine;

namespace TiltBrush
{
    internal sealed class SetVoxPaletteColorCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private readonly RuntimeVoxDocument m_Document;
        private readonly int m_Index;
        private readonly Color32 m_Before, m_After;

        internal SetVoxPaletteColorCommand(ModelWidget widget, RuntimeVoxDocument document,
            int index, Color32 color, BaseCommand parent = null) : base(parent)
        {
            m_Widget = widget;
            m_Document = document;
            m_Index = index;
            m_Before = document.Palette[index - 1];
            m_After = color;
        }

        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;
        protected override void OnUndo() => Apply(m_Before);
        protected override void OnRedo() => Apply(m_After);
        private void Apply(Color32 color)
        {
            _ = new VoxDocumentApiWrapper(m_Document, m_Widget);
            m_Document.ReplacePaletteEntry(m_Index, color);
            m_Widget.RefreshVoxPaletteVisuals();
            SaveLoadScript.m_Instance?.SketchChanged();
        }
    }
}
