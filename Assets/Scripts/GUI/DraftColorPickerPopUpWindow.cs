// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using UnityEngine;

namespace TiltBrush
{
    public class DraftColorPickerPopUpWindow : ColorPickerPopUpWindow
    {
        [SerializeField] private BaseButton m_ApplyButton;
        private Func<Color?> m_ReadColor;
        private Action<Color> m_Apply;
        private Func<Color, Color> m_Normalize;
        private Func<bool> m_CanApply;
        private BaseButton m_BoundApplyButton;
        private bool m_CloseOnApply;
        public Color DraftColor { get; private set; }

        public void BindColor(Func<Color?> readColor, Action<Color> apply, BaseButton applyButton = null,
            bool closeOnApply = true, Func<Color, Color> normalize = null, Func<bool> canApply = null)
        {
            ColorPicker.ColorPicked -= OnColorPicked;
            m_ReadColor = readColor;
            m_Apply = apply;
            m_Normalize = normalize;
            m_CanApply = canApply;
            m_BoundApplyButton = applyButton != null ? applyButton : m_ApplyButton;
            m_CloseOnApply = closeOnApply;
            var current = m_ReadColor();
            if (current.HasValue)
            {
                DraftColor = current.Value;
                ColorPicker.Controller.CurrentColor = DraftColor;
            }
            ColorPicker.ColorPicked += OnColorPicked;
            RefreshApplyButton();
        }

        private void OnColorPicked(Color color)
        {
            var current = m_ReadColor?.Invoke();
            if (!current.HasValue || m_CanApply?.Invoke() == false) return;
            if (m_Normalize != null) color = m_Normalize(color);
            color.a = current.Value.a;
            DraftColor = color;
            ColorPicker.Controller.CurrentColor = color;
            RefreshApplyButton();
            OnDraftColorChanged();
        }

        protected virtual void OnDraftColorChanged() { }

        protected void RefreshApplyButton()
        {
            var current = m_ReadColor?.Invoke();
            if (m_BoundApplyButton != null)
                m_BoundApplyButton.SetButtonAvailable(current.HasValue && m_CanApply?.Invoke() != false && DraftColor != current.Value);
        }

        public void ApplyColor()
        {
            var current = m_ReadColor?.Invoke();
            if (!current.HasValue || m_CanApply?.Invoke() == false) return;
            Color color = DraftColor;
            color.a = current.Value.a;
            if (color != current.Value) m_Apply?.Invoke(color);
            if (m_CloseOnApply) RequestClose();
            else RefreshApplyButton();
        }

        protected virtual void OnDestroy()
        {
            if (ColorPicker != null) ColorPicker.ColorPicked -= OnColorPicked;
        }
    }
}
