// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class RuntimeVoxDocument
    {
        private static readonly Vector3Int[] s_FaceNeighbors =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up,
            Vector3Int.down, Vector3Int.forward, Vector3Int.back,
        };

        private readonly struct PlacedCell
        {
            public readonly RuntimeModel Model;
            public readonly Vector3Int LocalPosition;
            public readonly byte PaletteIndex;

            public PlacedCell(RuntimeModel model, Vector3Int localPosition, byte paletteIndex)
            {
                Model = model;
                LocalPosition = localPosition;
                PaletteIndex = paletteIndex;
            }
        }

        // All instances within a widget use one voxel scale. Signed axis permutations
        // put their cell centers on the same integer VOX-world lattice, before Z-up conversion.
        public IReadOnlyList<RuntimeVoxDocument> CreateFaceConnectedRegions()
        {
            var placed = new Dictionary<Vector3Int, List<PlacedCell>>();
            foreach (RuntimeModel model in m_models)
            {
                if (!model.IsVisible) continue;
                foreach (var cell in model.Voxels)
                {
                    Vector3 world = model.TransformOffset + model.GlobalRotation.MultiplyVector(cell.Key);
                    Vector3Int position = Vector3Int.RoundToInt(world);
                    if ((world - (Vector3)position).sqrMagnitude > 0.000001f)
                        throw new InvalidOperationException("VOX region splitting requires cell-aligned placements.");
                    if (!placed.TryGetValue(position, out var occupants))
                    {
                        occupants = new List<PlacedCell>();
                        placed.Add(position, occupants);
                    }
                    occupants.Add(new PlacedCell(model, cell.Key, cell.Value));
                }
            }

            var remaining = new HashSet<Vector3Int>(placed.Keys);
            var components = new List<List<PlacedCell>>();
            var queue = new Queue<Vector3Int>();
            // Dictionary order fixes component ordering for a given document, without
            // sorting every voxel or depending on an arbitrary HashSet enumeration.
            foreach (Vector3Int seed in placed.Keys)
            {
                if (!remaining.Remove(seed)) continue;
                var component = new List<PlacedCell>();
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    Vector3Int position = queue.Dequeue();
                    component.AddRange(placed[position]);
                    foreach (Vector3Int direction in s_FaceNeighbors)
                    {
                        Vector3Int neighbor = position + direction;
                        if (remaining.Remove(neighbor)) queue.Enqueue(neighbor);
                    }
                }
                components.Add(component);
            }
            if (components.Count <= 1) return Array.Empty<RuntimeVoxDocument>();

            var result = new List<RuntimeVoxDocument>(components.Count);
            foreach (List<PlacedCell> component in components)
            {
                var selectedCells = component.GroupBy(cell => cell.Model).ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(cell => cell.LocalPosition, cell => cell.PaletteIndex));
                result.Add(CopyDocument(selectedCells));
            }
            return result;
        }

        public RuntimeVoxDocument CloneDocument() => CopyDocument(null);

        private RuntimeVoxDocument CopyDocument(
            IReadOnlyDictionary<RuntimeModel, Dictionary<Vector3Int, byte>> selectedCells)
        {
            var copy = new RuntimeVoxDocument { m_sourceVoxBytes = m_sourceVoxBytes };
            Array.Copy(Palette, copy.Palette, Palette.Length);
            foreach (var material in m_materials) copy.m_materials.Add(material.Key, material.Value);
            var sharedDefinitions = new Dictionary<VoxelDefinition, VoxelDefinition>();
            foreach (RuntimeModel model in m_models)
            {
                VoxelDefinition definition;
                if (selectedCells != null && model.IsVisible)
                {
                    if (!selectedCells.TryGetValue(model, out var cells)) continue;
                    // Clipped instances must own definitions independently of other placements.
                    definition = new VoxelDefinition(model.Size, model.SourceModelId,
                        new Dictionary<Vector3Int, byte>(cells));
                }
                else if (!sharedDefinitions.TryGetValue(model.Definition, out definition))
                {
                    definition = new VoxelDefinition(model.Size, model.SourceModelId,
                        new Dictionary<Vector3Int, byte>(model.Voxels));
                    sharedDefinitions.Add(model.Definition, definition);
                }
                var instance = new RuntimeModel(model.Name, model.Size, model.SourceModelId,
                    model.IsCopy, localTransformOffset: model.LocalTransformOffset,
                    globalRotation: model.GlobalRotation, localRotation: model.LocalRotation,
                    isVisible: model.IsVisible, definition: definition,
                    sceneNodeId: model.SceneNodeId, sceneModelSlot: model.SceneModelSlot)
                {
                    TransformOffset = model.TransformOffset,
                };
                copy.m_models.Add(instance);
            }
            return copy;
        }
    }
}
