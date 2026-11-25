// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

namespace TiltBrush
{
    // Group conversion with the edit or split that requires it. The library model
    // remains attached to the widget so undo can restore its external content.
    public sealed class EmbedVoxModelCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private readonly RuntimeVoxDocument m_Document;

        public EmbedVoxModelCommand(ModelWidget widget, RuntimeVoxDocument document, BaseCommand parent = null)
            : base(parent)
        {
            m_Widget = widget;
            m_Document = document;
        }

        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;

        protected override void OnRedo()
        {
            m_Widget.AdoptEditableVoxDocument(m_Document);
            SaveLoadScript.m_Instance?.SketchChanged();
        }

        protected override void OnUndo()
        {
            m_Widget.RestoreExternalVoxContent();
            SaveLoadScript.m_Instance?.SketchChanged();
        }
    }
}
