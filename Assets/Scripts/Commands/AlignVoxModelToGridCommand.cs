// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

namespace TiltBrush
{
    public sealed class AlignVoxModelToGridCommand : MoveWidgetCommand
    {
        public AlignVoxModelToGridCommand(ModelWidget widget, TrTransform transform)
            : base(widget, transform, widget.CustomDimension, final: true) { }

        protected override void OnRedo()
        {
            base.OnRedo();
            RefreshSelectionBounds();
        }

        protected override void OnUndo()
        {
            base.OnUndo();
            RefreshSelectionBounds();
        }

        private void RefreshSelectionBounds()
        {
            var selection = SelectionManager.m_Instance;
            if (selection != null && selection.IsWidgetSelected(Widget)) selection.UpdateSelectionWidget();
        }
    }
}
