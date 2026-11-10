// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenBrush.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace TiltBrush
{
    public sealed class VoxPalettePopUpWindow : ColorPickerPopUpWindow
    {
        private const int PageSize = 64, Columns = 8, PageCount = 4;
        private static readonly RuntimeVoxDocument.MaterialType[] MaterialTypes =
        {
            RuntimeVoxDocument.MaterialType.Diffuse, RuntimeVoxDocument.MaterialType.Metal,
            RuntimeVoxDocument.MaterialType.Glass, RuntimeVoxDocument.MaterialType.Emit,
            RuntimeVoxDocument.MaterialType.Blend
        };
        [SerializeField] private VoxPaletteButton m_ButtonPrefab;
        [SerializeField] private ActionButton m_Previous;
        [SerializeField] private ActionButton m_Next;
        [SerializeField] private ActionButton m_PreviousMaterial;
        [SerializeField] private ActionButton m_NextMaterial;
        [SerializeField] private TextActionButton m_ColourTab;
        [SerializeField] private TextActionButton m_MaterialTabButton;
        [SerializeField] private GameObject m_MaterialArea;
        [SerializeField] private TextMeshPro m_TypeLabel;
        [SerializeField] private ValueSlider[] m_MaterialSliders;
        [FormerlySerializedAs("m_Apply")]
        [SerializeField] private ActionToggleButton m_BrushColor;
        [SerializeField] private ActionButton m_ClearUnused;
        [SerializeField] private ActionButton m_Sort;
        [SerializeField] private Texture2D m_SwatchTexture;
        [SerializeField] private Transform m_SwatchGrid;
        [SerializeField] private float m_SwatchSpacing = .15f;
        [SerializeField] private float m_SwatchSize = .13f;
        private readonly VoxPaletteButton[] m_Swatches = new VoxPaletteButton[PageSize];
        private readonly int[] m_DisplayOrder = new int[byte.MaxValue];
        private ModelWidget m_Widget;
        private Model m_Source;
        private RuntimeVoxDocument m_Document;
        private VoxPaintSettings m_Settings;
        private SketchMemoryScript m_Memory;
        private ColorController m_BrushController;
        private int m_Page, m_Selected = 1;
        private bool m_MaterialTab, m_Applying;
        private object m_Gesture;
        private bool ValidTarget => m_Widget != null && m_Widget.Showing && m_Widget.gameObject.activeInHierarchy &&
            m_Document != null && (ReferenceEquals(m_Widget.EditableVoxDocument, m_Document) ||
                (m_Widget.IsExternalVox && m_Widget.Model == m_Source));
        private bool CanEdit => ValidTarget && !MultiplayerManager.m_Instance.IsViewOnly;
        private bool BrushMode => m_Settings == null || m_Settings.PaletteIndex == 0;
        private IReadOnlyDictionary<string, string> Properties => BrushMode ? m_Settings.DrawingMaterial :
            m_Document.Materials.TryGetValue(m_Selected, out var material) ? material.Properties : null;

        internal bool IsActionAvailable(VoxPaletteButton.Action action) => action switch
        {
            VoxPaletteButton.Action.Previous => ValidTarget && m_Page > 0,
            VoxPaletteButton.Action.Next => ValidTarget && m_Page < PageCount - 1,
            VoxPaletteButton.Action.ClearUnused => CanEdit,
            VoxPaletteButton.Action.PreviousMaterial or VoxPaletteButton.Action.NextMaterial => CanEdit,
            _ => ValidTarget
        };

        public override void Init(GameObject parent, string text)
        {
            for (int i = 0; i < m_DisplayOrder.Length; ++i) m_DisplayOrder[i] = i + 1;
            base.Init(parent, text);
            for (int i = 0; i < PageSize; ++i)
                m_Swatches[i] = CreateSwatch(new Vector3(i % Columns * m_SwatchSpacing,
                    -(i / Columns) * m_SwatchSpacing, 0));
            ColorPicker.ColorPicked += OnColorPicked;
            ColorPicker.ColorFinalized += FinishChange;
            m_BrushController = App.BrushColor;
            m_BrushController.CurrentColorSet += OnBrushColorChanged;
            m_Memory = SketchMemoryScript.m_Instance;
            m_Memory.OperationStackChanged += OnHistoryChanged;
        }
        private VoxPaletteButton CreateSwatch(Vector3 position)
        {
            var button = Instantiate(m_ButtonPrefab, m_SwatchGrid);
            button.transform.localPosition = position; button.transform.localRotation = Quaternion.identity;
            button.transform.localScale = Vector3.one * m_SwatchSize;
            button.RegisterComponent();
            button.Configure(this, VoxPaletteButton.Action.Swatch, m_SwatchTexture, "Palette slot");
            return button;
        }
        internal void Bind(ModelWidget widget, RuntimeVoxDocument document)
        {
            m_Widget = widget; m_Source = widget.Model; m_Document = document; m_Settings = VoxPaintSettings.For(widget);
            m_Selected = m_Settings.PaletteIndex > 0 ? m_Settings.PaletteIndex : m_Settings.LastPaletteIndex;
            GroupUsedColorsFirst(m_DisplayOrder, document.GetUsedPaletteIndices());
            m_Page = Array.IndexOf(m_DisplayOrder, m_Selected) / PageSize; RefreshControls();
        }
        internal void SelectColor(int index)
        {
            if (!ValidTarget || index < 1 || index > byte.MaxValue) return;
            FinishChange(); m_Selected = index; m_Settings.PaletteIndex = index; m_Settings.LastPaletteIndex = index;
            m_Page = Array.IndexOf(m_DisplayOrder, index) / PageSize; RefreshControls();
        }
        public void UseBrushColor()
        {
            if (!ValidTarget) return;
            FinishChange(); m_Settings.PaletteIndex = 0; RefreshControls();
        }
        public void ShowMaterial(bool material)
        { FinishChange(); m_MaterialTab = material; RefreshControls(); }
        public void ChangePage(int delta)
        { if (!ValidTarget) return; FinishChange(); m_Page = Mathf.Clamp(m_Page + delta, 0, PageCount - 1); RefreshControls(); }
        public void ChangeMaterialType(int direction)
        {
            if (!CanEdit) return;
            FinishChange();
            int current = Mathf.Max(0, Array.IndexOf(MaterialTypes, VoxMaterialAdapter.Describe(Properties).Type));
            int next = (current + direction + MaterialTypes.Length) % MaterialTypes.Length;
            SetMaterial(VoxMaterialAdapter.WithType(Properties, (int)MaterialTypes[next])); FinishChange();
        }
        private void OnColorPicked(Color color)
        {
            if (!CanEdit || m_Applying) return;
            if (BrushMode)
            {
                color.a = App.BrushColor.CurrentColor.a;
                if (color == App.BrushColor.CurrentColor) return;
                Record(new SetVoxBrushColorCommand(color, Gesture()));
            }
            else
            {
                Color32 entry = color; entry.a = m_Document.Palette[m_Selected - 1].a;
                if (entry.Equals(m_Document.Palette[m_Selected - 1])) return;
                Record(new SetVoxPaletteEntryCommand(m_Widget, m_Document, m_Selected, entry, Properties, Gesture()));
                int slot = Array.IndexOf(m_DisplayOrder, m_Selected) % PageSize;
                m_Swatches[slot].SetSwatch(m_Selected, entry, true);
            }
        }
        private object Gesture() => m_Gesture ??= new object();
        private void SetMaterial(IReadOnlyDictionary<string, string> properties)
        {
            if (!CanEdit) return;
            if (BrushMode) Record(new SetVoxDrawingMaterialCommand(m_Settings, properties, Gesture()));
            else Record(new SetVoxPaletteEntryCommand(m_Widget, m_Document, m_Selected, m_Document.Palette[m_Selected - 1], properties, Gesture()));
        }
        private void Record(BaseCommand command)
        { m_Applying = true; try { m_Memory.PerformAndRecordCommand(command); } finally { m_Applying = false; } }
        public void FinishChange()
        { m_Gesture = null; if (m_Settings != null && !m_Applying) RefreshControls(); }
        public void ClearUnusedColors()
        {
            if (!CanEdit) return; FinishChange();
            BaseCommand embedding = m_Widget.IsExternalVox ? new EmbedVoxModelCommand(m_Widget, m_Document) : null;
            var command = new ClearUnusedVoxPaletteCommand(m_Widget, m_Document, embedding);
            if (command.HasChanges) Record(embedding ?? command);
            GroupUsedColorsFirst(m_DisplayOrder, m_Document.GetUsedPaletteIndices()); RefreshControls();
        }
        private static void GroupUsedColorsFirst(int[] displayOrder, bool[] used)
        {
            // Stable partition: retain the current order within each group.
            var grouped = new int[displayOrder.Length];
            int slot = 0;
            foreach (int index in displayOrder)
                if (used[index]) grouped[slot++] = index;
            foreach (int index in displayOrder)
                if (!used[index]) grouped[slot++] = index;
            Array.Copy(grouped, displayOrder, grouped.Length);
        }

        public void SortColors()
        {
            if (!ValidTarget) return;
            bool[] used = m_Document.GetUsedPaletteIndices();
            Array.Sort(m_DisplayOrder, (a, b) =>
            {
                int order = used[b].CompareTo(used[a]);
                if (order == 0) order = ComparePaletteColors(m_Document.Palette[a - 1], m_Document.Palette[b - 1]);
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
        { if (m_Applying) return; m_Gesture = null; RefreshControls(); }
        private void OnBrushColorChanged(ColorPickerMode mode, Vector3 value)
        { if (!m_Applying && BrushMode && m_Gesture == null) RefreshControls(); }
        private void RefreshControls()
        {
            bool valid = ValidTarget;
            for (int i = 0; i < PageSize; ++i)
            {
                int slot = m_Page * PageSize + i; var swatch = m_Swatches[i]; swatch.gameObject.SetActive(slot < m_DisplayOrder.Length);
                if (slot >= m_DisplayOrder.Length) continue;
                int index = m_DisplayOrder[slot]; swatch.SetButtonAvailable(valid);
                if (valid) swatch.SetSwatch(index, m_Document.Palette[index - 1], !BrushMode && index == m_Selected);
            }
            m_Previous.SetButtonAvailable(valid && m_Page > 0); m_Next.SetButtonAvailable(valid && m_Page < PageCount - 1);
            m_BrushColor.SetButtonAvailable(valid); m_BrushColor.ToggleState = BrushMode;
            m_PreviousMaterial.SetButtonAvailable(CanEdit); m_NextMaterial.SetButtonAvailable(CanEdit);
            m_ColourTab.SetButtonAvailable(valid); m_MaterialTabButton.SetButtonAvailable(valid);
            m_ColourTab.SetButtonSelected(!m_MaterialTab); m_MaterialTabButton.SetButtonSelected(m_MaterialTab);
            m_ClearUnused.SetButtonAvailable(CanEdit); m_Sort.SetButtonAvailable(valid);
            ColorPicker.gameObject.SetActive(!m_MaterialTab); m_MaterialArea.SetActive(m_MaterialTab && valid);
            m_WindowText.text = valid ? $"VOX palette — {(BrushMode ? "Drawing material" : $"Palette slot {m_Selected}")} — {m_Page + 1}/{PageCount}" : "VOX model unavailable";
            if (!valid) return;
            ColorPicker.Controller.CurrentColor = BrushMode ? App.BrushColor.CurrentColor : m_Document.Palette[m_Selected - 1];
            if (m_MaterialTab) RefreshMaterialControls();
        }
        private void RefreshMaterialControls()
        {
            var native = VoxMaterialAdapter.Describe(Properties); var values = VoxMaterialAdapter.Read(native);
            int type = (int)native.Type;
            bool blend = native.Type == RuntimeVoxDocument.MaterialType.Blend;
            m_TypeLabel.text = blend ? "Blend" : type < 4 ? new[] { "Standard", "Metal", "Glass", "Emissive" }[type] : "Imported material";
            var mask = PbrMaterialParameter.Controls.Glossiness;
            if (type == 1) mask |= PbrMaterialParameter.Controls.Metallic;
            if (type == 2) mask |= PbrMaterialParameter.Controls.Opacity;
            var parameters = type < 3 ? PbrMaterialParameter.For(values, mask) : new List<PbrMaterialParameter>();
            if (blend)
            {
                // Opacity remains editable at 1, so opaque Blend materials can become transparent.
                parameters.Add(new PbrMaterialParameter { Key = PbrMaterialParameter.Property.Metallic, Label = "Metallic" });
                parameters.Add(new PbrMaterialParameter { Key = PbrMaterialParameter.Property.Glossiness, Label = "Glossiness" });
                parameters.Add(new PbrMaterialParameter { Key = PbrMaterialParameter.Property.Opacity, Label = "Opacity" });
            }
            for (int i = 0; i < m_MaterialSliders.Length; ++i)
            {
                var slider = m_MaterialSliders[i]; bool visible = i < parameters.Count || type == 3 && i == 0;
                slider.gameObject.SetActive(visible); if (!visible) continue;
                foreach (var collider in slider.GetComponentsInChildren<Collider>(true)) collider.enabled = CanEdit;
                if (type == 3) slider.Configure("Emission strength", VoxMaterialAdapter.Emission(native), value => SetMaterial(VoxMaterialAdapter.WithEmission(Properties, value)), FinishChange);
                else
                {
                    var parameter = parameters[i];
                    slider.Configure(parameter.Label, parameter.Read(values), value => SetMaterial(VoxMaterialAdapter.WithValue(Properties, parameter.Key, value)), FinishChange,
                        min: parameter.Key == PbrMaterialParameter.Property.Opacity ? .05f : 0f);
                }
            }
        }
        private void OnDestroy()
        {
            if (ColorPicker != null) { ColorPicker.ColorPicked -= OnColorPicked; ColorPicker.ColorFinalized -= FinishChange; }
            if (m_BrushController != null) m_BrushController.CurrentColorSet -= OnBrushColorChanged;
            if (m_Memory != null) m_Memory.OperationStackChanged -= OnHistoryChanged;
        }
    }
}
