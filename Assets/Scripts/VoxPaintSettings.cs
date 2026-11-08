// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TiltBrush
{
    internal sealed class VoxPaintSettings
    {
        private static readonly ConditionalWeakTable<ModelWidget, VoxPaintSettings> s_PerModel = new();
        internal static VoxPaintSettings For(ModelWidget widget) => s_PerModel.GetValue(widget, _ => new VoxPaintSettings());
        // Zero follows the brush; VOX palette slots are one-based.
        internal int PaletteIndex;
        internal int LastPaletteIndex = 1;
        internal IReadOnlyDictionary<string, string> DrawingMaterial = new Dictionary<string, string>();
    }
}
