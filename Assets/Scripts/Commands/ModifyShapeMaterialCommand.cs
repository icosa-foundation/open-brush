// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public sealed class ModifyShapeMaterialCommand :
        WidgetPropertyCommand<EditableModelWidget, (EditableModelMaterialData Material, Color? Color)>
    {
        public ModifyShapeMaterialCommand(EditableModelWidget widget, EditableModelMaterialData material, object gesture)
            : base(widget, (material.Clone(), null), peer => (peer.MaterialSettings?.Clone(), peer.m_Color), gesture)
        {
            material.Validate();
        }

        protected override void Apply(IReadOnlyList<EditableModelWidget> widgets,
            (EditableModelMaterialData Material, Color? Color) value)
        {
            foreach (var widget in widgets) { widget.SetMaterialSettings(value.Material, value.Color); }
        }
    }
}
