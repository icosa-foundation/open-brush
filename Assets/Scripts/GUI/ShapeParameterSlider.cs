// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class ShapeParameterSlider : AdvancedSlider
    {
        [SerializeField] private TextMeshPro m_Label;
        private ShapesPanel m_Panel;
        private ProceduralShapes.Parameter m_Parameter;
        private bool m_Editing;

        protected override void Awake()
        {
            base.Awake();
            onUpdateValue.AddListener(OnValueChanged);
        }

        public void Configure(ShapesPanel panel, ProceduralShapes.Parameter parameter, GeometryRecipeData recipe)
        {
            m_Panel = panel;
            m_Parameter = parameter;
            float value = parameter.Read(recipe);
            SliderType = parameter.Integer ? SliderTypes.Int : SliderTypes.Float;
            m_DisplayDecimalPlaces = 3;
            SetMin(Mathf.Min(parameter.Min, value));
            SetMax(Mathf.Max(parameter.Max, value));
            SetInitialValueAndUpdate(value);
            m_Label.text = parameter.Label;
            SetDescriptionText(parameter.Label);
        }

        protected override string FormatValue(float value) => m_Parameter != null ? m_Parameter.Format(value) : base.FormatValue(value);

        private void OnValueChanged(Vector3 value)
        {
            if (m_Panel != null && m_Parameter != null)
            {
                m_Editing = true;
                m_Panel.ChangeParameter(m_Parameter, value.z);
            }
        }

        public override void ButtonReleased()
        {
            base.ButtonReleased();
            FinishOwnChange();
        }

        private void FinishOwnChange()
        {
            if (!m_Editing) { return; }
            m_Editing = false;
            m_Panel?.FinishChange();
        }

        public override void ResetState()
        {
            base.ResetState();
            FinishOwnChange();
        }
    }
}
