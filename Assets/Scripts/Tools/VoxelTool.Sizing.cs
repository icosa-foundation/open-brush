// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using UnityEngine;

namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
        [SerializeField, Range(1, 127)] private int m_MaxBrushRadius = 7;
        [SerializeField] private float m_HapticSizeUp = 0.01f;
        [SerializeField] private float m_HapticSizeDown = 0.01f;
        private float m_BrushSizeRatio;
        private int m_BrushSizeStep;
        private int MaxBrushSizeStep => Mathf.Clamp(m_MaxBrushRadius, 1, 127) * 2;
        private VoxBrushFootprint BrushFootprint => new VoxBrushFootprint(m_BrushSizeStep);

        public override void UpdateSize(float adjustAmount)
        {
            if (IsEatingInput || ToolHidden() || m_GestureActive) return;
            m_BrushSizeRatio = Mathf.Clamp01(m_BrushSizeRatio + adjustAmount);
            int next = Mathf.RoundToInt(m_BrushSizeRatio * MaxBrushSizeStep);
            if (next == m_BrushSizeStep) return;
            InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush,
                next > m_BrushSizeStep ? m_HapticSizeUp : m_HapticSizeDown);
            m_BrushSizeStep = next;
        }

        public override float GetSize01() => (float)m_BrushSizeStep / MaxBrushSizeStep;
        public override float GetSize() => BrushFootprint.Extent * 2 + 1;
    }
}
