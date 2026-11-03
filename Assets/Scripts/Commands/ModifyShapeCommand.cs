// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using Polyhydra.Core;
using System;

namespace TiltBrush
{
    public sealed class ModifyShapeCommand : BaseCommand
    {
        private readonly EditableModelWidget m_Widget;
        private readonly GeometryRecipeData m_Before;
        private GeometryRecipeData m_After;
        private readonly object m_Gesture;
        private PolyMesh m_PreparedMesh;

        public ModifyShapeCommand(EditableModelWidget widget, GeometryRecipeData recipe, object gesture)
        {
            m_Widget = widget;
            m_Before = widget.m_PolyRecipe;
            m_After = recipe;
            m_Gesture = gesture;
            // Reject invalid settings before the undo stack clears redo or merges this command.
            m_PreparedMesh = EditableModelManager.Evaluate(recipe);
            if (m_PreparedMesh.Vertices.Count == 0 || m_PreparedMesh.Faces.Count == 0)
            {
                throw new InvalidOperationException("The shape has no geometry");
            }
        }

        public override bool NeedsSave => true;
        protected override void OnUndo() => Apply(m_Before);
        protected override void OnRedo()
        {
            Apply(m_After, m_PreparedMesh);
            m_PreparedMesh = null;
        }

        private void Apply(GeometryRecipeData recipe, PolyMesh preparedMesh = null)
        {
            EditableModelManager.GetOrCreate().RegenerateMesh(m_Widget, preparedMesh ?? EditableModelManager.Evaluate(recipe));
            m_Widget.m_PolyRecipe = recipe;
        }

        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) { return true; }
            if (!(other is ModifyShapeCommand shape) || shape.m_Widget != m_Widget ||
                m_Gesture == null || !ReferenceEquals(shape.m_Gesture, m_Gesture)) { return false; }
            m_After = shape.m_After;
            return true;
        }
    }
}
