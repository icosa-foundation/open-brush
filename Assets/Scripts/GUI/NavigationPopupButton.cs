// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using UnityEngine;

namespace TiltBrush
{
    public class NavigationPopupButton : PopupButton
    {
        [SerializeField] private Texture2D m_ViewTexture;
        [SerializeField] private Texture2D m_FlyTexture;
        [SerializeField] private Texture2D m_TeleportTexture;
        private BaseTool.ToolType? m_LastTool;

        protected override void Awake()
        {
            base.Awake();
            App.Switchboard.ToolChanged += UpdateVisuals;
        }

        protected override void OnDestroy()
        {
            App.Switchboard.ToolChanged -= UpdateVisuals;
            base.OnDestroy();
        }

        public override void UpdateVisuals()
        {
            base.UpdateVisuals();
            if (SketchSurfacePanel.m_Instance == null) return;

            var tool = SketchSurfacePanel.m_Instance.ActiveToolType;
            if (tool == m_LastTool) return;
            m_LastTool = tool;
            SetButtonTexture(tool == BaseTool.ToolType.FlyTool ? m_FlyTexture :
                tool == BaseTool.ToolType.TeleportTool ? m_TeleportTexture : m_ViewTexture);
        }
    }
}
