// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using UnityEngine;

namespace TiltBrush
{
    public sealed class ModifyShapeMaterialCommand : BaseCommand
    {
        private readonly EditableModelWidget m_Widget;
        private readonly EditableModelMaterialData m_Before;
        private EditableModelMaterialData m_After;
        private readonly Color m_BeforeColor;
        private readonly object m_Gesture;

        public ModifyShapeMaterialCommand(EditableModelWidget widget, EditableModelMaterialData material, object gesture)
        {
            material.Validate();
            m_Widget = widget;
            m_Before = widget.MaterialSettings?.Clone();
            m_BeforeColor = widget.m_Color;
            m_After = material.Clone();
            m_Gesture = gesture;
        }

        public override bool NeedsSave => true;
        protected override void OnRedo() => m_Widget.SetMaterialSettings(m_After);
        protected override void OnUndo() => m_Widget.SetMaterialSettings(m_Before, m_BeforeColor);
        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) return true;
            if (!(other is ModifyShapeMaterialCommand command) || command.m_Widget != m_Widget ||
                m_Gesture == null || !ReferenceEquals(m_Gesture, command.m_Gesture)) return false;
            m_After = command.m_After.Clone();
            return true;
        }
    }
}
