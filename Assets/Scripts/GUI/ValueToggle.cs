// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class ValueToggle : ActionToggleButton
    {
        [SerializeField] private TextMeshPro m_Label;
        private Action<bool> m_Changed;
        private Action m_Finished;

        public void Configure(string label, bool value, Action<bool> changed, Action finished)
        {
            m_Changed = changed;
            m_Finished = finished;
            m_Label.text = label;
            SetDescriptionText(label);
            ToggleState = value;
        }

        protected override void OnButtonPressed()
        {
            if (m_Changed == null) return;
            // Capture before finishing: finishing a gesture may rebind this row.
            var changed = m_Changed;
            var finished = m_Finished;
            bool value = !ToggleState;
            finished?.Invoke();
            changed(value);
            finished?.Invoke();
        }
    }
}
