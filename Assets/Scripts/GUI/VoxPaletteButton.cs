// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using UnityEngine;

namespace TiltBrush
{
    public sealed class VoxPaletteButton : BaseButton
    {
        internal enum Action { Swatch, Previous, Next, BrushColour, ClearUnused, Sort, ColourTab, MaterialTab, PreviousMaterial, NextMaterial }
        private VoxPalettePopUpWindow m_Popup;
        private Action m_Action;
        private int m_Index;
        private Material m_Material;
        [SerializeField] private Texture2D m_SelectedTexture;
        private GameObject m_SelectedOverlay;
        private Material m_SelectedMaterial;

        public override bool IsAvailable() => m_Popup != null ? m_Popup.IsActionAvailable(m_Action) : base.IsAvailable();

        protected override void Awake()
        {
            base.Awake();
            if (!m_AtlasTexture) m_Material = m_ButtonRenderer.material;
        }

        internal void Configure(VoxPalettePopUpWindow popup, Action action, Texture2D texture, string description)
        {
            m_Popup = popup;
            m_Action = action;
            m_ToggleButton = action == Action.Swatch || action == Action.BrushColour || action == Action.ColourTab || action == Action.MaterialTab;
            if (m_Material != null && m_Material.HasProperty("_Tint")) m_Material.SetColor("_Tint", Color.white);
            SetButtonActivated(false);
            if (texture != null) SetButtonTexture(texture);
            if (description != null) SetDescriptionText(description);
        }

        internal void SetSwatch(int index, Color32 color, bool selected)
        {
            m_Index = index;
            m_Material.SetColor("_Tint", color);
            m_ToggleActive = selected;
            SetButtonActivated(selected);
            if (selected && m_SelectedOverlay == null && m_SelectedTexture != null)
            {
                m_SelectedOverlay = new GameObject("Selected palette slot");
                m_SelectedOverlay.layer = gameObject.layer;
                m_SelectedOverlay.transform.SetParent(transform, false);
                m_SelectedOverlay.transform.localPosition = new Vector3(0,0,-.01f);
                m_SelectedOverlay.AddComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
                m_SelectedMaterial = new Material(m_Material);
                m_SelectedMaterial.SetColor("_Tint", Color.white);
                m_SelectedMaterial.SetColor("_Color", Color.white);
                m_SelectedMaterial.SetTexture("_MainTex", m_SelectedTexture);
                m_SelectedOverlay.AddComponent<MeshRenderer>().sharedMaterial = m_SelectedMaterial;
            }
            if (m_SelectedOverlay != null) m_SelectedOverlay.SetActive(selected);
            SetDescriptionText($"Palette color {index} — #{color.r:X2}{color.g:X2}{color.b:X2}");
        }

        internal void SetActionSelected(bool selected)
        {
            m_ToggleActive = selected;
            SetButtonActivated(selected);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_Material != null) Destroy(m_Material);
            if (m_SelectedMaterial != null) Destroy(m_SelectedMaterial);
        }

        protected override void OnButtonPressed()
        {
            switch (m_Action)
            {
                case Action.Swatch: m_Popup.SelectColor(m_Index); break;
                case Action.Previous: m_Popup.ChangePage(-1); break;
                case Action.Next: m_Popup.ChangePage(1); break;
                case Action.BrushColour: m_Popup.UseBrushColor(); break;
                case Action.ColourTab: m_Popup.ShowMaterial(false); break;
                case Action.MaterialTab: m_Popup.ShowMaterial(true); break;
                case Action.PreviousMaterial: m_Popup.ChangeMaterialType(-1); break;
                case Action.NextMaterial: m_Popup.ChangeMaterialType(1); break;
                case Action.ClearUnused: m_Popup.ClearUnusedColors(); break;
                case Action.Sort: m_Popup.SortColors(); break;
            }
        }
    }
}
