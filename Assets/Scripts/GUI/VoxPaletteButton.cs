// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using UnityEngine;

namespace TiltBrush
{
    public sealed class VoxPaletteButton : BaseButton
    {
        internal enum Action { Swatch, Previous, Next, Apply, ClearUnused, Sort }
        private VoxPalettePopUpWindow m_Popup;
        private Action m_Action;
        private int m_Index;
        private Material m_Material;

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
            m_ToggleButton = action == Action.Swatch;
            if (m_Material != null && action != Action.Apply) m_Material.SetColor("_Tint", Color.white);
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
            SetDescriptionText($"Palette color {index} — #{color.r:X2}{color.g:X2}{color.b:X2}");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_Material != null) Destroy(m_Material);
        }

        protected override void OnButtonPressed()
        {
            switch (m_Action)
            {
                case Action.Swatch: m_Popup.SelectColor(m_Index); break;
                case Action.Previous: m_Popup.ChangePage(-1); break;
                case Action.Next: m_Popup.ChangePage(1); break;
                case Action.Apply: m_Popup.ApplyColor(); break;
                case Action.ClearUnused: m_Popup.ClearUnusedColors(); break;
                case Action.Sort: m_Popup.SortColors(); break;
            }
        }
    }
}
