// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
        internal bool TrySamplePalette(VoxToolTarget.Hit sample)
        {
            if (!TryGetPaletteTarget(out var widget, out var document)) return false;
            var settings = VoxPaintSettings.For(widget);
            byte index = sample.PaletteIndex;
            if (sample.Widget != widget)
            {
                var color = sample.Document.Palette[index - 1];
                var properties = sample.Document.Materials.TryGetValue(index, out var material) ? material.Properties : null;
                index = document.FindSampledPaletteIndex(color, properties);
                if (index == 0)
                {
                    if (OpenBrush.Multiplayer.MultiplayerManager.m_Instance.IsViewOnly) return false;
                    index = document.FindUnusedPaletteIndex(properties);
                    if (index == 0)
                    {
                        m_ReportedPaletteFull = false;
                        ReportPaletteFull();
                        return false;
                    }
                    var command = new SetVoxPaletteEntryCommand(widget, document, index, color, properties, null);
                    _ = new SelectVoxPaletteEntryCommand(settings, index, command);
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                    return true;
                }
            }
            settings.PaletteIndex = index;
            settings.LastPaletteIndex = index;
            return true;
        }

        internal bool TryGetPaletteTarget(out ModelWidget widget, out RuntimeVoxDocument document)
        {
            EndGesture();
            EnsureCurrentTarget();
            widget = CurrentTargetWidget;
            document = HasCurrentTarget ? m_CurrentDocument._Document : null;
            return document != null && !CreatingNewModel;
        }
    }
}
