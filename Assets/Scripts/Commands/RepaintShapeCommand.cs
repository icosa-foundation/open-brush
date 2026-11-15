// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public sealed class RepaintShapeCommand : WidgetPropertyCommand<EditableModelWidget, Color>
    {
        public RepaintShapeCommand(EditableModelWidget widget, Color color, BaseCommand parent = null)
            : base(widget, color, peer => peer.m_Color, parent: parent) { }

        protected override void Apply(IReadOnlyList<EditableModelWidget> widgets, Color color)
        {
            foreach (var widget in widgets) { widget.SetColor(color); }
        }
    }
}
