// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using UnityEngine;

namespace TiltBrush
{
    public class ShapePresetButton : BaseButton
    {
        [SerializeField] private int m_Preset;
        protected override void Awake()
        {
            base.Awake();
            gameObject.SetActive(MeshEditing.ProceduralShapes.IsPresetVisible(m_Preset));
        }

        protected override void OnButtonPressed() => GetComponentInParent<ShapesPanel>().CreateShape(m_Preset, transform);
    }
}
