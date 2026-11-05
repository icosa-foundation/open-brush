// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    public sealed class VoxPalettePopUpWindow : PopUpWindow
    {
        private const int PageSize = 64, Columns = 8, PageCount = 4;
        [SerializeField] private VoxPaletteButton m_ButtonPrefab;
        [SerializeField] private ColorPicker m_ColorPicker;
        [SerializeField] private VoxPaletteButton m_Apply;
        [SerializeField] private VoxPaletteButton m_ClearUnused;
        [SerializeField] private VoxPaletteButton m_Sort;
        [SerializeField] private Texture2D m_SwatchTexture, m_PreviousTexture, m_NextTexture;
        [SerializeField] private Vector3 m_GridOrigin = new Vector3(-1.38f, .6f, -.06f);
        [SerializeField] private float m_SwatchSpacing = .15f;
        [SerializeField] private float m_SwatchSize = .13f;
        private readonly VoxPaletteButton[] m_Swatches = new VoxPaletteButton[PageSize];
        private readonly int[] m_DisplayOrder = new int[byte.MaxValue];
        private VoxPaletteButton m_Previous, m_Next;
        private ModelWidget m_Widget;
        private Model m_Source;
        private RuntimeVoxDocument m_Document;
        private SketchMemoryScript m_Memory;
        private int m_Page, m_Selected = 1;
        private Color32 m_Draft;

        private ColorPicker ColorPicker => m_ColorPicker;
        private bool ValidTarget => m_Widget != null && m_Widget.Showing &&
            m_Widget.gameObject.activeInHierarchy && m_Document != null &&
            (ReferenceEquals(m_Widget.EditableVoxDocument, m_Document) ||
                (m_Widget.IsExternalVox && m_Widget.Model == m_Source));
        private bool CanEdit => ValidTarget && !MultiplayerManager.m_Instance.IsViewOnly;

        internal bool IsActionAvailable(VoxPaletteButton.Action action) => action switch
        {
            VoxPaletteButton.Action.Previous => ValidTarget && m_Page > 0,
            VoxPaletteButton.Action.Next => ValidTarget && m_Page < PageCount - 1,
            VoxPaletteButton.Action.Apply => CanEdit && !m_Draft.Equals(m_Document.Palette[m_Selected - 1]),
            VoxPaletteButton.Action.ClearUnused => CanEdit,
            _ => ValidTarget,
        };

        public override void Init(GameObject parent, string text)
        {
            for (int i = 0; i < m_DisplayOrder.Length; ++i) m_DisplayOrder[i] = i + 1;
            base.Init(parent, text);
            for (int i = 0; i < PageSize; ++i)
                m_Swatches[i] = CreateButton(m_GridOrigin + new Vector3(i % Columns * m_SwatchSpacing,
                    -(i / Columns) * m_SwatchSpacing, 0), VoxPaletteButton.Action.Swatch, m_SwatchTexture, "Palette color");
            m_Previous = CreateButton(new Vector3(-1.15f, -.72f, -.06f), VoxPaletteButton.Action.Previous, m_PreviousTexture, "Previous palette page");
            m_Next = CreateButton(new Vector3(-.55f, -.72f, -.06f), VoxPaletteButton.Action.Next, m_NextTexture, "Next palette page");
            m_Apply.Configure(this, VoxPaletteButton.Action.Apply, null, null);
            m_ClearUnused.Configure(this, VoxPaletteButton.Action.ClearUnused, null, "Clear unused palette colors");
            m_Sort.Configure(this, VoxPaletteButton.Action.Sort, null, "Sort palette colors by hue");
            ColorPicker.ColorPicked += OnColorPicked;
            m_Memory = SketchMemoryScript.m_Instance;
            m_Memory.OperationStackChanged += OnHistoryChanged;
        }

        private VoxPaletteButton CreateButton(Vector3 position, VoxPaletteButton.Action action, Texture2D texture, string description)
        {
            var button = Instantiate(m_ButtonPrefab, transform);
            button.transform.localPosition = position;
            button.transform.localRotation = Quaternion.identity;
            button.transform.localScale = Vector3.one * m_SwatchSize;
            button.RegisterComponent();
            button.Configure(this, action, texture, description);
            return button;
        }

        internal void Bind(ModelWidget widget, RuntimeVoxDocument document)
        {
            m_Widget = widget;
            m_Source = widget.Model;
            m_Document = document;
            SelectColor(1);
        }

        internal void SelectColor(int index)
        {
            if (!ValidTarget || index < 1 || index > byte.MaxValue) return;
            m_Selected = index;
            m_Page = Array.IndexOf(m_DisplayOrder, index) / PageSize;
            m_Draft = m_Document.Palette[index - 1];
            ColorPicker.Controller.CurrentColor = m_Draft;
            RefreshControls();
        }

        internal void ChangePage(int delta)
        {
            if (!ValidTarget) return;
            m_Page = Mathf.Clamp(m_Page + delta, 0, PageCount - 1);
            SelectColor(m_DisplayOrder[m_Page * PageSize]);
        }

        private void OnColorPicked(Color color)
        {
            if (!CanEdit) return;
            byte alpha = m_Document.Palette[m_Selected - 1].a;
            Color32 draft = color;
            draft.a = alpha;
            if (m_Draft.Equals(draft)) return;
            m_Draft = draft;
            ColorPicker.Controller.CurrentColor = m_Draft;
            m_Swatches[Array.IndexOf(m_DisplayOrder, m_Selected) % PageSize].SetSwatch(m_Selected, m_Draft, true);
            m_Apply.SetButtonAvailable(!m_Draft.Equals(m_Document.Palette[m_Selected - 1]));
        }

        internal void ApplyColor()
        {
            if (!IsActionAvailable(VoxPaletteButton.Action.Apply)) return;
            // Palette edits retain floating cells. Store one entry, not a voxel snapshot.
            BaseCommand embedding = m_Widget.IsExternalVox ? new EmbedVoxModelCommand(m_Widget, m_Document) : null;
            var command = new SetVoxPaletteColorCommand(m_Widget, m_Document, m_Selected, m_Draft, embedding);
            m_Memory.PerformAndRecordCommand(embedding ?? command);
            SelectColor(m_Selected);
        }

        internal void ClearUnusedColors()
        {
            if (!CanEdit) return;
            BaseCommand embedding = m_Widget.IsExternalVox ? new EmbedVoxModelCommand(m_Widget, m_Document) : null;
            var command = new ClearUnusedVoxPaletteCommand(m_Widget, m_Document, embedding);
            if (!command.HasChanges) return;
            m_Memory.PerformAndRecordCommand(embedding ?? command);
            SelectColor(m_Selected);
        }

        internal void SortColors()
        {
            if (!ValidTarget) return;
            Array.Sort(m_DisplayOrder, (a, b) =>
            {
                int order = ComparePaletteColors(m_Document.Palette[a - 1], m_Document.Palette[b - 1]);
                return order != 0 ? order : a.CompareTo(b);
            });
            // Keep editing the same palette entry, including an unapplied draft.
            m_Page = Array.IndexOf(m_DisplayOrder, m_Selected) / PageSize;
            RefreshControls();
        }

        internal static int ComparePaletteColors(Color a, Color b)
        {
            Color.RGBToHSV(a, out float hueA, out float saturationA, out float brightnessA);
            Color.RGBToHSV(b, out float hueB, out float saturationB, out float brightnessB);
            bool greyA = saturationA <= .05f, greyB = saturationB <= .05f;
            int order = greyA.CompareTo(greyB);
            if (order != 0) return order;
            if (!greyA)
            {
                order = hueA.CompareTo(hueB);
                if (order != 0) return order;
                order = saturationA.CompareTo(saturationB);
                if (order != 0) return order;
            }
            return brightnessA.CompareTo(brightnessB);
        }

        private void OnHistoryChanged()
        {
            if (ValidTarget) SelectColor(m_Selected);
            else RefreshControls();
        }

        private void RefreshControls()
        {
            bool valid = ValidTarget;
            for (int i = 0; i < PageSize; ++i)
            {
                int slot = m_Page * PageSize + i;
                var swatch = m_Swatches[i];
                swatch.gameObject.SetActive(slot < m_DisplayOrder.Length);
                if (slot >= m_DisplayOrder.Length) continue;
                int index = m_DisplayOrder[slot];
                swatch.SetButtonAvailable(valid);
                if (valid) swatch.SetSwatch(index, index == m_Selected ? m_Draft : m_Document.Palette[index - 1], index == m_Selected);
            }
            m_Previous.SetButtonAvailable(valid && m_Page > 0);
            m_Next.SetButtonAvailable(valid && m_Page < PageCount - 1);
            m_Apply.SetButtonAvailable(CanEdit && !m_Draft.Equals(m_Document.Palette[m_Selected - 1]));
            m_ClearUnused.SetButtonAvailable(CanEdit);
            m_Sort.SetButtonAvailable(valid);
            m_WindowText.text = valid ? $"VOX palette — Color {m_Selected} — Page {m_Page + 1}/{PageCount}" : "VOX model unavailable";
        }

        private void OnDestroy()
        {
            if (ColorPicker != null) ColorPicker.ColorPicked -= OnColorPicked;
            if (m_Memory != null) m_Memory.OperationStackChanged -= OnHistoryChanged;
        }
    }
}
