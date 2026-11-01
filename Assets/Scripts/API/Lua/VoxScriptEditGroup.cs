// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using System.Collections.Generic;

namespace TiltBrush
{
    // One snapshot pair per edited document and undo gesture, not per cell or frame.
    internal sealed class VoxScriptEditGroup
    {
        private sealed class Edit
        {
            internal RuntimeVoxDocument.Snapshot Before;
            internal ModifyVoxDocumentCommand Command;
            internal ModelWidget Widget;
            internal bool BeforeShowing;
            internal TrTransform BeforeTransform;
        }

        private readonly Dictionary<RuntimeVoxDocument, Edit> m_Edits = new();
        internal BaseCommand Root { get; }
        internal static bool IsPreview { get; private set; }

        internal VoxScriptEditGroup(BaseCommand root) => Root = root;

        internal static IDisposable BeginExecution(bool preview) => new ExecutionScope(preview);

        private sealed class ExecutionScope : IDisposable
        {
            private readonly bool m_Previous;
            internal ExecutionScope(bool preview)
            {
                m_Previous = IsPreview;
                IsPreview = m_Previous || preview;
            }
            public void Dispose() => IsPreview = m_Previous;
        }

        internal bool Mutate(VoxDocumentApiWrapper wrapper, Func<bool> mutation)
        {
            var document = wrapper._Document;
            if (!m_Edits.TryGetValue(document, out var edit))
            {
                edit = new Edit { Before = document.CaptureSnapshot(), Widget = wrapper.Widget,
                    BeforeShowing = wrapper.Widget.Showing, BeforeTransform = GetTransform(wrapper.Widget) };
                m_Edits.Add(document, edit);
            }
            if (!mutation()) return false;
            if (edit.Command == null)
            {
                BaseCommand parent = Root;
                if (wrapper.Widget.IsExternalVox)
                {
                    var embedding = new EmbedVoxModelCommand(wrapper.Widget, document, Root);
                    embedding.Redo();
                    parent = embedding;
                }
                // Complete replaces the temporary end state before the root is recorded.
                edit.Command = new ModifyVoxDocumentCommand(wrapper.Widget, edit.Before, edit.Before, parent,
                    beforeShowing: edit.BeforeShowing, beforeTransform: edit.BeforeTransform);
            }
            wrapper.OnDocumentMutated(undoRecorded: true);
            return true;
        }

        internal void Complete()
        {
            foreach (var entry in m_Edits)
                entry.Value.Command?.UpdateEndState(entry.Key.CaptureSnapshot(), entry.Value.Widget.Showing,
                    GetTransform(entry.Value.Widget));
            m_Edits.Clear();
        }

        internal bool HasChanges(RuntimeVoxDocument document)
            => m_Edits.TryGetValue(document, out var edit) && edit.Command != null;

        private static TrTransform GetTransform(ModelWidget widget)
        {
            var transform = TrTransform.FromLocalTransform(widget.transform);
            transform.scale = widget.GetSignedWidgetSize();
            return transform;
        }
    }
}
