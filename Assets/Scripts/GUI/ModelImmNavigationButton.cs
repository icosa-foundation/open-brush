// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using UnityEngine;

namespace TiltBrush
{
    public class ModelImmNavigationButton : BaseButton
    {
        [SerializeField] private bool m_IsNext;
        [SerializeField] private bool m_IsViewpoint;

        public bool IsViewpoint => m_IsViewpoint;

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            var tab = GetComponentInParent<ReferencePanelModelTab>();
            if (m_IsViewpoint) tab?.CycleViewpoint(m_IsNext ? 1 : -1);
            else tab?.CycleChapter(m_IsNext ? 1 : -1);
        }
    }
}
