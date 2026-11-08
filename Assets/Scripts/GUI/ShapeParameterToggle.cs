// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class ShapeParameterToggle : ActionToggleButton
    {
        [SerializeField] private TextMeshPro m_Label;
        private ShapesPanel m_Panel;
        private ProceduralShapes.Parameter m_Parameter;
        private ShapeMaterialParameter m_MaterialParameter;

        public void Configure(ShapesPanel panel, ProceduralShapes.Parameter parameter, GeometryRecipeData recipe)
        {
            m_Panel = panel;
            m_Parameter = parameter;
            m_MaterialParameter = null;
            m_Label.text = parameter.Label;
            SetDescriptionText(parameter.Label);
            ToggleState = parameter.Ellipse ? panel.EllipseEnabled : parameter.Read(recipe) != 0;
        }

        protected override void OnButtonPressed()
        {
            if (m_Panel == null) { return; }
            m_Panel.FinishChange();
            if (m_MaterialParameter != null)
                m_Panel.ChangeMaterialParameter(m_MaterialParameter, ToggleState ? 0 : 1);
            else if (m_Parameter != null)
                m_Panel.ChangeParameter(m_Parameter, ToggleState ? 0 : 1);
            m_Panel.FinishChange();
        }

        public void Configure(ShapesPanel panel, ShapeMaterialParameter parameter, EditableModelMaterialData material)
        {
            m_Panel = panel;
            m_Parameter = null;
            m_MaterialParameter = parameter;
            m_Label.text = parameter.Label;
            SetDescriptionText(parameter.Label);
            ToggleState = parameter.Read(material) != 0;
        }
    }
}
