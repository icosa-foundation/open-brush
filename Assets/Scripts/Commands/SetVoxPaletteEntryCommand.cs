// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    internal sealed class SetVoxPaletteEntryCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private readonly RuntimeVoxDocument m_Document;
        private readonly int m_Index;
        private readonly Color32 m_BeforeColor;
        private Color32 m_AfterColor;
        private readonly IReadOnlyDictionary<string, string> m_BeforeMaterial;
        private IReadOnlyDictionary<string, string> m_AfterMaterial;
        private readonly object m_Gesture;
        private readonly EmbedVoxModelCommand m_Embedding;

        internal SetVoxPaletteEntryCommand(ModelWidget widget, RuntimeVoxDocument document, int index,
            Color32 color, IReadOnlyDictionary<string, string> material, object gesture)
        {
            if (widget.IsExternalVox) m_Embedding = new EmbedVoxModelCommand(widget, document);
            m_Widget = widget; m_Document = document; m_Index = index; m_Gesture = gesture;
            m_BeforeColor = document.Palette[index - 1]; m_AfterColor = color;
            m_BeforeMaterial = document.Materials.TryGetValue(index, out var old) ? old.Properties : null;
            m_AfterMaterial = material;
        }
        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;
        protected override void OnUndo() { Apply(m_BeforeColor, m_BeforeMaterial); m_Embedding?.Undo(); }
        protected override void OnRedo() { m_Embedding?.Redo(); Apply(m_AfterColor, m_AfterMaterial); }
        private void Apply(Color32 color, IReadOnlyDictionary<string, string> material)
        {
            _ = new VoxDocumentApiWrapper(m_Document, m_Widget);
            m_Document.ReplacePaletteEntry(m_Index, color);
            m_Document.SetPaletteMaterial(m_Index, material);
            m_Widget.RefreshVoxPaletteVisuals();
            SaveLoadScript.m_Instance?.SketchChanged();
        }
        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) return true;
            if (!(other is SetVoxPaletteEntryCommand edit) || edit.m_Widget != m_Widget ||
                edit.m_Document != m_Document || edit.m_Index != m_Index || m_Gesture == null ||
                !ReferenceEquals(m_Gesture, edit.m_Gesture)) return false;
            m_AfterColor = edit.m_AfterColor; m_AfterMaterial = edit.m_AfterMaterial;
            return true;
        }
    }
}
