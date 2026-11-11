// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using UnityEngine;

namespace TiltBrush
{
    public class ShapeMaterialButton : BaseButton
    {
        public enum Action { Shape, Material, Color, ApplyColor }
        [SerializeField] private Action m_Action;
        private Material m_SwatchMaterial;

        protected override void Awake()
        {
            base.Awake();
            if (m_Action == Action.Color) m_SwatchMaterial = m_ButtonRenderer.material;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_SwatchMaterial != null) Destroy(m_SwatchMaterial);
        }

        public void Refresh(bool materialTab, bool hasTarget, Color color)
        {
            SetButtonAvailable(m_Action != Action.Color || hasTarget);
            m_ToggleActive = m_Action == Action.Material ? materialTab : m_Action == Action.Shape && !materialTab;
            SetButtonActivated(m_ToggleActive);
            if (m_SwatchMaterial != null)
            {
                color.a = 1f;
                m_SwatchMaterial.SetColor("_Tint", color);
            }
        }

        protected override void OnButtonPressed()
        {
            if (m_Action == Action.ApplyColor)
            {
                GetComponentInParent<DraftColorPickerPopUpWindow>().ApplyColor();
                return;
            }
            var panel = GetComponentInParent<ShapesPanel>();
            if (m_Action == Action.Color) panel.OpenColorPicker();
            else panel.ShowMaterial(m_Action == Action.Material);
        }
    }
}
