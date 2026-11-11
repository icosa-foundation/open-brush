// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using UnityEngine;

namespace TiltBrush
{
    public sealed class RepaintShapeCommand : BaseCommand
    {
        private readonly EditableModelWidget m_Widget;
        private readonly Color m_Before;
        private readonly Color m_After;

        public RepaintShapeCommand(EditableModelWidget widget, Color color, BaseCommand parent = null) : base(parent)
        {
            m_Widget = widget;
            m_Before = widget.m_Color;
            m_After = color;
        }

        public override bool NeedsSave => true;
        protected override void OnRedo() { if (m_Widget != null) { m_Widget.SetColor(m_After); } }
        protected override void OnUndo() { if (m_Widget != null) { m_Widget.SetColor(m_Before); } }
    }
}
