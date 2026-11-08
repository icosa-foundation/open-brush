// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using UnityEngine;

namespace TiltBrush
{
    public class ShapeColorPickerPopUpWindow : ColorPickerPopUpWindow
    {
        [SerializeField] private ShapeMaterialButton m_ApplyButton;
        private EditableModelWidget m_Target;
        private Color m_Draft;

        public void SetTarget(EditableModelWidget target)
        {
            m_Target = target;
            m_Draft = target.m_Color;
            ColorPicker.Controller.CurrentColor = m_Draft;
            ColorPicker.ColorPicked += OnColorPicked;
            m_ApplyButton.SetButtonAvailable(false);
        }

        private void OnColorPicked(Color color)
        {
            color.a = m_Target != null ? m_Target.m_Color.a : m_Draft.a;
            m_Draft = color;
            m_ApplyButton.SetButtonAvailable(m_Target != null && color != m_Target.m_Color);
        }

        public void ApplyColor()
        {
            if (m_Target == null || !m_Target.gameObject.activeInHierarchy) return;
            m_Draft.a = m_Target.m_Color.a;
            if (m_Draft != m_Target.m_Color)
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new RepaintShapeCommand(m_Target, m_Draft));
            RequestClose();
        }

        private void OnDestroy()
        {
            if (ColorPicker != null) ColorPicker.ColorPicked -= OnColorPicked;
        }
    }
}
