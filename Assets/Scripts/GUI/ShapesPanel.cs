// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class ShapesPanel : BasePanel
    {
        [SerializeField] private TextMeshPro m_TargetLabel;
        [SerializeField] private TextMeshPro m_PageLabel;
        [SerializeField] private TextMeshPro m_StatusLabel;
        [SerializeField] private ShapeParameterSlider[] m_ParameterSliders;
        [SerializeField] private ShapeParameterToggle[] m_ParameterToggles;
        [SerializeField] private ShapePageButton m_PreviousPage;
        [SerializeField] private ShapePageButton m_NextPage;

        private EditableModelWidget m_Target;
        private GeometryRecipeData m_DisplayedRecipe;
        private List<ProceduralShapes.Parameter> m_Parameters = new List<ProceduralShapes.Parameter>();
        private int m_Page;
        private object m_Gesture;
        public bool EllipseEnabled => m_Target != null && m_Target.EllipseEnabled;

        public override void InitPanel()
        {
            base.InitPanel();
            Refresh();
        }

        private void Update()
        {
            BaseUpdate();
            if (m_ParameterSliders == null || m_Gesture != null) { return; }
            var last = EditableModelWidget.LastActivated;
            if (last != null && last.gameObject.activeInHierarchy && last != m_Target)
            {
                m_Target = last;
                m_Page = 0;
                Refresh();
            }
            else if ((m_Target != null && !m_Target.gameObject.activeInHierarchy) ||
                (m_Target == null && m_DisplayedRecipe != null))
            {
                m_Target = null;
                Refresh();
            }
            else if (m_Target != null && m_Target.m_PolyRecipe != m_DisplayedRecipe)
            {
                Refresh();
            }
        }

        public void CreateShape(int preset, Transform button)
        {
            if (!ProceduralShapes.IsPresetVisible(preset)) { return; }
            FinishChange();
            try
            {
                var recipe = ProceduralShapes.CreateRecipe(preset);
                var spawn = Coords.AsGlobal[button] * TrTransform.R(Quaternion.AngleAxis(180, Vector3.up));
                // Match model-library placement, with a 30 cm initial working size.
                spawn.scale = App.Scene.Pose.scale;
                var widget = EditableModelManager.GetOrCreate().GeneratePolyMesh(
                    EditableModelManager.Evaluate(recipe), recipe, spawn, Quaternion.identity,
                    forceTransform: false, snapGrid: SelectionManager.m_Instance.SnappingGridSize,
                    snapAngle: SelectionManager.m_Instance.SnappingAngle,
                    color: PointerManager.m_Instance.MainPointer.GetCurrentColor());
                widget.Activate(true);
                m_Target = widget;
                m_Page = 0;
                WidgetManager.m_Instance.WidgetsDormant = false;
                SketchControlsScript.m_Instance.EatGazeObjectInput();
                SelectionManager.m_Instance.RemoveFromSelection(false);
                Refresh();
            }
            catch (Exception e) { ShowError(e); }
        }

        public void ChangePage(int direction)
        {
            FinishChange();
            int pages = Mathf.Max(1, Mathf.CeilToInt((float)m_Parameters.Count / m_ParameterSliders.Length));
            m_Page = Mathf.Clamp(m_Page + direction, 0, pages - 1);
            Refresh();
        }

        public void ChangeParameter(ProceduralShapes.Parameter parameter, float value)
        {
            if (m_Target == null || !m_Target.gameObject.activeInHierarchy) { return; }
            float current = parameter.Ellipse ? (EllipseEnabled ? 1f : 0f) : parameter.Read(m_Target.m_PolyRecipe);
            if (Mathf.Approximately(current, value)) { return; }
            if (parameter.Ellipse)
            {
                m_Target.EllipseEnabled = value >= .5f;
                if (value >= .5f || !ProceduralShapes.IsElliptical(m_Target.m_PolyRecipe))
                {
                    Refresh();
                    return;
                }
            }
            if (m_Gesture == null) { m_Gesture = new object(); }
            try
            {
                var recipe = ProceduralShapes.WithValue(m_Target.m_PolyRecipe, parameter, value);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ModifyShapeCommand(m_Target, recipe, m_Gesture));
                m_DisplayedRecipe = recipe;
                m_StatusLabel.text = "";
            }
            catch (Exception e) { ShowError(e); }
        }

        public void FinishChange()
        {
            if (m_Gesture == null) { return; }
            m_Gesture = null;
            if (m_ParameterSliders != null) { Refresh(); }
        }

        protected override void OnDisablePanel()
        {
            FinishChange();
            base.OnDisablePanel();
        }

        private void Refresh()
        {
            m_DisplayedRecipe = m_Target != null ? m_Target.m_PolyRecipe : null;
            m_Parameters = m_DisplayedRecipe != null
                ? ProceduralShapes.GetParameters(m_DisplayedRecipe, EllipseEnabled) : new List<ProceduralShapes.Parameter>();
            int pages = Mathf.Max(1, Mathf.CeilToInt((float)m_Parameters.Count / m_ParameterSliders.Length));
            m_Page = Mathf.Clamp(m_Page, 0, pages - 1);
            m_TargetLabel.text = m_Target != null ? $"Editing: {ProceduralShapes.DisplayName(m_DisplayedRecipe)}" : "Create a shape or point at one";
            m_PageLabel.text = m_Parameters.Count > 0 ? $"{m_Page + 1} / {pages}" : "";
            m_StatusLabel.text = "";
            m_PreviousPage.SetButtonAvailable(m_Parameters.Count > 0 && m_Page > 0);
            m_NextPage.SetButtonAvailable(m_Page + 1 < pages);
            for (int i = 0; i < m_ParameterSliders.Length; ++i)
            {
                int index = m_Page * m_ParameterSliders.Length + i;
                bool visible = index < m_Parameters.Count;
                bool boolean = visible && m_Parameters[index].Boolean;
                m_ParameterSliders[i].gameObject.SetActive(visible && !boolean);
                m_ParameterToggles[i].gameObject.SetActive(boolean);
                if (boolean) { m_ParameterToggles[i].Configure(this, m_Parameters[index], m_DisplayedRecipe); }
                else if (visible) { m_ParameterSliders[i].Configure(this, m_Parameters[index], m_DisplayedRecipe); }
            }
        }

        private void ShowError(Exception e)
        {
            m_StatusLabel.text = "Those settings cannot produce a shape";
            Debug.LogWarning($"[OB_SHAPES] {e.Message}");
        }
    }
}
