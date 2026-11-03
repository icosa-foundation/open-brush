// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
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
