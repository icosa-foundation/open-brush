// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    // The cut-out stays a normal visible voxel widget. Retain it while undone so
    // later commands and Lua handles address the same object after redo.
    public sealed class CreateVoxRegionWidgetCommand : BaseCommand
    {
        private readonly ModelWidget m_Source;
        private readonly RuntimeVoxDocument m_Region;
        private ModelWidget m_Widget;

        public CreateVoxRegionWidgetCommand(ModelWidget source, RuntimeVoxDocument region, BaseCommand parent)
            : base(parent)
        {
            m_Source = source;
            m_Region = region;
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            if (m_Widget == null)
            {
                m_Widget = m_Source.CloneWithVoxDocument(m_Region);
                m_Widget.name = $"{m_Source.name} Region";
                m_Widget.Group = m_Source.Group;
            }
            else
            {
                m_Widget.gameObject.SetActive(true);
                TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_Widget.GetTiltMeterCost(), up: true);
            }
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
            SaveLoadScript.m_Instance?.SketchChanged();
        }

        protected override void OnUndo()
        {
            if (SelectionManager.m_Instance.IsWidgetSelected(m_Widget))
            {
                SelectionManager.m_Instance.DeselectWidgets(new List<GrabWidget> { m_Widget });
                SelectionManager.m_Instance.UpdateSelectionWidget();
                App.Switchboard.TriggerSelectionChanged();
            }
            m_Widget.gameObject.SetActive(false);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_Widget.GetTiltMeterCost(), up: false);
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
            SaveLoadScript.m_Instance?.SketchChanged();
        }

        protected override void OnDispose()
        {
            // Discarding an undone cut releases its hidden widget. Clearing undo
            // history must not remove a cut-out that is still visible in the sketch.
            if (m_Widget == null || m_Widget.gameObject.activeSelf) return;
            WidgetManager.m_Instance.UnregisterGrabWidget(m_Widget.gameObject);
            Object.Destroy(m_Widget.gameObject);
        }
    }
}
