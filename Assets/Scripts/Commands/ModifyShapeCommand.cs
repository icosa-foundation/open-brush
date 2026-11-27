// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;

namespace TiltBrush
{
    public enum ShapeEditScope { Instance, Definition }

    public sealed class ModifyShapeCommand : BaseCommand
    {
        private readonly EditableModelWidget m_Driver;
        private readonly ShapeEditScope m_Scope;
        private readonly object m_Gesture;
        private readonly Dictionary<EditableModelWidget, EditableModelDefinition> m_Before;
        private Dictionary<EditableModelWidget, EditableModelDefinition> m_After;
        private PolyMesh m_PreparedMesh;
        private readonly bool m_MergeWidgetMovement;
        private MoveWidgetCommand m_Movement;

        public ModifyShapeCommand(EditableModelWidget widget, GeometryRecipeData recipe, ShapeEditScope scope,
            object gesture, bool mergeWidgetMovement = false)
        {
            m_Driver = widget;
            m_Scope = scope;
            m_Gesture = gesture;
            m_MergeWidgetMovement = mergeWidgetMovement;
            // Reject invalid settings before the undo stack clears redo or merges this command.
            m_PreparedMesh = EditableModelManager.Evaluate(recipe);
            if (m_PreparedMesh.Vertices.Count == 0 || m_PreparedMesh.Faces.Count == 0)
            {
                throw new InvalidOperationException("The shape has no geometry");
            }

            var instances = WidgetManager.m_Instance.EditableModelWidgets;
            var targets = scope == ShapeEditScope.Definition
                ? instances.Where(peer => peer.Definition?.Id == widget.Definition.Id)
                : WidgetPropertyCommand.GetTargets(widget);
            m_Before = targets.ToDictionary(peer => peer, peer => peer.Definition);
            // Inactive instances (including undoable deletions) and opaque retained records
            // still own their binding even though the user cannot currently edit them.
            var otherIds = instances.Where(peer => peer.Definition != null && !m_Before.ContainsKey(peer))
                .Select(peer => peer.Definition.Id).ToHashSet();
            m_After = new Dictionary<EditableModelWidget, EditableModelDefinition>();
            foreach (var group in m_Before.GroupBy(pair => pair.Value.Id))
            {
                bool fork = scope == ShapeEditScope.Instance &&
                    (otherIds.Contains(group.Key) || EditableModelManager.RetainedDefinitions.ContainsKey(group.Key));
                var definition = new EditableModelDefinition(group.First().Value.Data.WithRecipe(recipe),
                    fork ? null : group.Key);
                foreach (var pair in group) { m_After.Add(pair.Key, definition); }
            }
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            Apply(m_After, m_PreparedMesh);
            m_PreparedMesh = null;
        }

        protected override void OnUndo() => Apply(m_Before, null);

        private static void Apply(Dictionary<EditableModelWidget, EditableModelDefinition> bindings, PolyMesh prepared)
        {
            // A redo has one recipe across all targets. An undo can restore different recipes
            // and different definition IDs even when several peers had identical geometry.
            foreach (var group in bindings.GroupBy(pair => pair.Value.Data.Recipe))
            {
                var poly = prepared ?? EditableModelManager.Evaluate(group.Key);
                EditableModelManager.GetOrCreate().RegenerateMeshes(group.Select(pair => pair.Key), poly);
                foreach (var pair in group)
                {
                    if (pair.Key != null) { pair.Key.Definition = pair.Value; }
                    if (EditableModelManager.RetainedDefinitions.ContainsKey(pair.Value.Id))
                    {
                        EditableModelManager.RetainedDefinitions[pair.Value.Id] = pair.Value.Data;
                    }
                }
            }
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
            if (other is ModifyShapeCommand next && next.m_Driver == m_Driver && next.m_Scope == m_Scope &&
                m_Gesture != null && ReferenceEquals(m_Gesture, next.m_Gesture) &&
                m_After.Count == next.m_Before.Count && m_After.All(pair =>
                    next.m_Before.TryGetValue(pair.Key, out var before) && before == pair.Value))
            {
                m_After = next.m_After;
                return true;
            }
            return base.Merge(other);
        }
    }
}
