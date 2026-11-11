// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using UnityEngine;

namespace TiltBrush
{
    public class ShapePageButton : BaseButton
    {
        [SerializeField] private int m_Direction;
        protected override void OnButtonPressed() => GetComponentInParent<ShapesPanel>().ChangePage(m_Direction);
    }
}
