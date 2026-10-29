// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    // Keep created widgets alive while undone: later commands and retained Lua handles
    // must refer to the same objects after redo, rather than replacement clones.
    public sealed class BreakVoxApartCommand : BaseCommand
    {
        private readonly ModelWidget m_Original;
        private readonly RuntimeVoxDocument m_Document;
        private readonly IReadOnlyList<RuntimeVoxDocument> m_Regions;
        private readonly List<ModelWidget> m_Widgets = new List<ModelWidget>();

        public BreakVoxApartCommand(ModelWidget widget, BaseCommand parent = null) : base(parent)
        {
            m_Original = widget;
            m_Document = widget.EditableVoxDocument;
            m_Regions = m_Document.CreateFaceConnectedRegions();
        }

        public override bool NeedsSave => m_Regions.Count > 1;
        public override bool IsAvailable => m_Original != null && m_Regions.Count > 1 &&
            ReferenceEquals(m_Original.EditableVoxDocument, m_Document);

        protected override void OnRedo()
        {
            bool creating = m_Widgets.Count == 0;
            if (creating)
            {
                try
                {
                    for (int i = 0; i < m_Regions.Count; ++i)
                    {
                        ModelWidget widget = m_Original.CloneWithVoxDocument(m_Regions[i]);
                        m_Widgets.Add(widget);
                        widget.name = $"{m_Original.name} Region {i + 1}";
                        widget.Group = m_Original.Group;
                        if (m_Original.Pinned) widget.PinFromSave();
                    }
                }
                catch
                {
                    foreach (ModelWidget widget in m_Widgets)
                    {
                        TiltMeterScript.m_Instance.AdjustMeterWithWidget(widget.GetTiltMeterCost(), up: false);
                        WidgetManager.m_Instance.UnregisterGrabWidget(widget.gameObject);
                        Object.Destroy(widget.gameObject);
                    }
                    m_Widgets.Clear();
                    throw;
                }
            }
            SelectionManager.m_Instance.DeselectWidgets(new List<GrabWidget> { m_Original });
            m_Original.gameObject.SetActive(false);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_Original.GetTiltMeterCost(), up: false);
            foreach (ModelWidget widget in m_Widgets)
            {
                widget.gameObject.SetActive(true);
                if (!creating) TiltMeterScript.m_Instance.AdjustMeterWithWidget(widget.GetTiltMeterCost(), up: true);
                SelectionManager.m_Instance.SelectWidget(widget);
            }
            SelectionManager.m_Instance.UpdateSelectionWidget();
            App.Switchboard.TriggerSelectionChanged();
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
            SaveLoadScript.m_Instance?.SketchChanged();
        }

        protected override void OnUndo()
        {
            SelectionManager.m_Instance.DeselectWidgets(m_Widgets);
            foreach (ModelWidget widget in m_Widgets)
            {
                widget.gameObject.SetActive(false);
                TiltMeterScript.m_Instance.AdjustMeterWithWidget(widget.GetTiltMeterCost(), up: false);
            }
            m_Original.gameObject.SetActive(true);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_Original.GetTiltMeterCost(), up: true);
            SelectionManager.m_Instance.SelectWidget(m_Original);
            SelectionManager.m_Instance.UpdateSelectionWidget();
            App.Switchboard.TriggerSelectionChanged();
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
            SaveLoadScript.m_Instance?.SketchChanged();
        }

        protected override void OnDispose()
        {
            foreach (ModelWidget widget in m_Widgets)
            {
                if (widget == null) continue;
                WidgetManager.m_Instance.UnregisterGrabWidget(widget.gameObject);
                Object.Destroy(widget.gameObject);
            }
            m_Widgets.Clear();
        }
    }
}
