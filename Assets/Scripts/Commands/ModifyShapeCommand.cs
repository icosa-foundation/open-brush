// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using Polyhydra.Core;
using System;
using System.Collections.Generic;

namespace TiltBrush
{
    public sealed class ModifyShapeCommand : WidgetPropertyCommand<EditableModelWidget, GeometryRecipeData>
    {
        private PolyMesh m_PreparedMesh;
        private readonly bool m_MergeWidgetMovement;
        private MoveWidgetCommand m_Movement;

        public ModifyShapeCommand(EditableModelWidget widget, GeometryRecipeData recipe, object gesture,
            bool mergeWidgetMovement = false)
            : base(widget, recipe, peer => peer.m_PolyRecipe, gesture)
        {
            m_MergeWidgetMovement = mergeWidgetMovement;
            // Reject invalid settings before the undo stack clears redo or merges this command.
            m_PreparedMesh = EditableModelManager.Evaluate(recipe);
            if (m_PreparedMesh.Vertices.Count == 0 || m_PreparedMesh.Faces.Count == 0)
            {
                throw new InvalidOperationException("The shape has no geometry");
            }
        }

        protected override object GetUndoKey(GeometryRecipeData recipe) => recipe.ToJson();

        protected override void Apply(IReadOnlyList<EditableModelWidget> widgets, GeometryRecipeData recipe)
        {
            EditableModelManager.GetOrCreate().RegenerateMeshes(widgets,
                m_PreparedMesh ?? EditableModelManager.Evaluate(recipe), recipe);
            m_PreparedMesh = null;
        }

        public override bool Merge(BaseCommand other)
        {
            if (m_MergeWidgetMovement && other is MoveWidgetCommand move && move.Widget == m_Driver)
            {
                if (m_Movement != null) { return m_Movement.Merge(move); }
                m_Movement = move;
                m_Children.Add(move);
                return true;
            }
            return base.Merge(other);
        }
    }
}
