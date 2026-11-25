// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class ValueSlider : AdvancedSlider
    {
        [SerializeField] private TextMeshPro m_Label;
        private Action<float> m_Changed;
        private Action m_Finished;
        private Func<float, string> m_Format;
        private bool m_Editing;

        protected override void Awake()
        {
            base.Awake();
            onUpdateValue.AddListener(OnValueChanged);
        }

        public void Configure(string label, float value, Action<float> changed, Action finished,
            float min = 0f, float max = 1f, bool integer = false, Func<float, string> format = null, int decimals = 2)
        {
            m_Changed = null;
            m_Finished = finished;
            m_Format = format;
            SliderType = integer ? SliderTypes.Int : SliderTypes.Float;
            m_DisplayDecimalPlaces = decimals;
            SetMin(Mathf.Min(min, value));
            SetMax(Mathf.Max(max, value));
            SetInitialValueAndUpdate(value);
            m_Label.text = label;
            SetDescriptionText(label);
            m_Changed = changed;
        }

        protected override string FormatValue(float value) => m_Format != null ? m_Format(value) : base.FormatValue(value);

        private void OnValueChanged(Vector3 value)
        {
            if (m_Changed == null) return;
            m_Editing = true;
            m_Changed(value.z);
        }

        public override void ButtonReleased()
        {
            base.ButtonReleased();
            FinishChange();
        }

        private void FinishChange()
        {
            if (!m_Editing) return;
            m_Editing = false;
            m_Finished?.Invoke();
        }

        public override void ResetState()
        {
            base.ResetState();
            FinishChange();
        }
    }
}
