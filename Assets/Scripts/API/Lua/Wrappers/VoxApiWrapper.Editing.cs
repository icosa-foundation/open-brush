// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    public partial class VoxDocumentApiWrapper
    {
        [MoonSharpHidden]
        internal bool Mutate(Func<bool> mutation)
        {
            RequireValidDocument();
            if (VoxScriptEditGroup.IsPreview) return false;
            if (!SuppressDirtyTracking) Widget?.CommitVoxRegionSelection();
            if (Widget == null || SuppressDirtyTracking)
            {
                bool changed = mutation();
                if (changed) OnDocumentMutated();
                return changed;
            }
            var group = ApiManager.Instance?.GetVoxEditGroup();
            if (group != null) return group.Mutate(this, mutation);

            // Calls outside a gesture still get an undo step for existing widgets.
            group = new VoxScriptEditGroup(new BaseCommand());
            bool edited = group.Mutate(this, mutation);
            group.Complete();
            if (edited) SketchMemoryScript.m_Instance.PerformAndRecordCommand(group.Root);
            return edited;
        }
    }
}
