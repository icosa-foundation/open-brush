// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using Polyhydra.Core;
using System;
using System.Linq;

namespace TiltBrush
{
    public sealed class ModifyShapeCommand : BaseCommand
    {
        private readonly EditableModelWidget m_Widget;
        private readonly EditableModelWidget[] m_Widgets;
        private readonly GeometryRecipeData[] m_Before;
        private GeometryRecipeData m_After;
        private readonly object m_Gesture;
        private PolyMesh m_PreparedMesh;
        private readonly bool m_MergeWidgetMovement;
        private MoveWidgetCommand m_Movement;

        public ModifyShapeCommand(EditableModelWidget widget, GeometryRecipeData recipe, object gesture,
            bool mergeWidgetMovement = false)
        {
            m_Widget = widget;
            m_Widgets = EditableModelManager.GetShapeEditTargets(widget);
            m_Before = m_Widgets.Select(peer => peer.m_PolyRecipe).ToArray();
            m_After = recipe;
            m_Gesture = gesture;
            m_MergeWidgetMovement = mergeWidgetMovement;
            // Reject invalid settings before the undo stack clears redo or merges this command.
            m_PreparedMesh = EditableModelManager.Evaluate(recipe);
            if (m_PreparedMesh.Vertices.Count == 0 || m_PreparedMesh.Faces.Count == 0)
            {
                throw new InvalidOperationException("The shape has no geometry");
            }
        }

        public override bool NeedsSave => true;
        protected override void OnUndo()
        {
            // Peers can have different recipes after an earlier independent edit. Undo builds
            // each distinct recipe once and shares it among the widgets that originally used it.
            foreach (var group in m_Widgets.Select((widget, i) => (Widget: widget, Recipe: m_Before[i]))
                         .GroupBy(before => before.Recipe.ToJson()))
            {
                Apply(group.Select(before => before.Widget).ToArray(), group.First().Recipe);
            }
        }
        protected override void OnRedo()
        {
            Apply(m_Widgets, m_After, m_PreparedMesh);
            m_PreparedMesh = null;
        }

        private void Apply(EditableModelWidget[] widgets, GeometryRecipeData recipe, PolyMesh preparedMesh = null)
        {
            EditableModelManager.GetOrCreate().RegenerateMeshes(widgets,
                preparedMesh ?? EditableModelManager.Evaluate(recipe), recipe);
        }

        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) { return true; }
            if (m_MergeWidgetMovement && other is MoveWidgetCommand move && move.Widget == m_Widget)
            {
                if (m_Movement != null) { return m_Movement.Merge(move); }
                m_Movement = move;
                m_Children.Add(move);
                return true;
            }
            if (!(other is ModifyShapeCommand shape) || shape.m_Widget != m_Widget ||
                !m_Widgets.SequenceEqual(shape.m_Widgets) ||
                m_Gesture == null || !ReferenceEquals(shape.m_Gesture, m_Gesture)) { return false; }
            m_After = shape.m_After;
            return true;
        }
    }
}
