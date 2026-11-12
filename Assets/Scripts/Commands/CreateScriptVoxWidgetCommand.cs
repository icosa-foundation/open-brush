// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    // The script has already populated the document and created its widget.
    // Retain both while undone so later edits and Lua handles survive redo.
    public sealed class CreateScriptVoxWidgetCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private bool m_Undone;

        public CreateScriptVoxWidgetCommand(ModelWidget widget, BaseCommand parent = null)
            : base(parent)
        {
            m_Widget = widget;
        }

        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;

        protected override void OnRedo()
        {
            // Initial recording follows creation, which already paid the meter cost.
            if (!m_Undone) return;
            m_Widget.gameObject.SetActive(true);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_Widget.GetTiltMeterCost(), up: true);
            m_Undone = false;
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
            // Do not animate hiding: deactivation preserves the widget's pose and state.
            m_Widget.gameObject.SetActive(false);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_Widget.GetTiltMeterCost(), up: false);
            m_Undone = true;
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
            SaveLoadScript.m_Instance?.SketchChanged();
        }

        protected override void OnDispose()
        {
            if (!m_Undone || m_Widget == null) return;
            WidgetManager.m_Instance.UnregisterGrabWidget(m_Widget.gameObject);
            Object.Destroy(m_Widget.gameObject);
        }
    }
}
