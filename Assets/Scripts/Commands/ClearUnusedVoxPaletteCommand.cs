// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    internal sealed class ClearUnusedVoxPaletteCommand : BaseCommand
    {
        private static readonly Color32 s_EmptyColor = new Color32(0, 0, 0, 255);
        private readonly ModelWidget m_Widget;
        private readonly RuntimeVoxDocument m_Document;
        private readonly List<(int Index, Color32 Color)> m_Before = new();

        internal ClearUnusedVoxPaletteCommand(ModelWidget widget, RuntimeVoxDocument document,
            BaseCommand parent = null) : base(parent)
        {
            m_Widget = widget;
            m_Document = document;
            bool[] used = document.GetUsedPaletteIndices();
            for (int index = 1; index <= byte.MaxValue; ++index)
                if (!used[index] && !document.Palette[index - 1].Equals(s_EmptyColor))
                    m_Before.Add((index, document.Palette[index - 1]));
        }

        internal bool HasChanges => m_Before.Count != 0;
        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;
        protected override void OnUndo() => Apply(false);
        protected override void OnRedo() => Apply(true);

        private void Apply(bool clear)
        {
            _ = new VoxDocumentApiWrapper(m_Document, m_Widget);
            foreach (var entry in m_Before)
                m_Document.ReplacePaletteEntry(entry.Index, clear ? s_EmptyColor : entry.Color);
            // These entries have no cells, so no voxel meshes need refreshing.
            SaveLoadScript.m_Instance?.SketchChanged();
        }
    }
}
