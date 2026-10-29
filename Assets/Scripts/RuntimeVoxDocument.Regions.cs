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

        // Split each placed grid independently in its own local coordinates. Touching
        // or overlapping grids never merge, and each result fits its original dimensions.
        public IReadOnlyList<RuntimeVoxDocument> CreateFaceConnectedRegions()
        {
            var components = new List<(RuntimeModel Model, Dictionary<Vector3Int, byte> Cells)>();
            var queue = new Queue<Vector3Int>();
            foreach (RuntimeModel model in m_models)
            {
                if (!model.IsVisible) continue;
                var remaining = new HashSet<Vector3Int>(model.Voxels.Keys);
                foreach (Vector3Int seed in model.Voxels.Keys)
                {
                    if (!remaining.Remove(seed)) continue;
                    var cells = new Dictionary<Vector3Int, byte>();
                    queue.Enqueue(seed);
                    while (queue.Count > 0)
                    {
                        Vector3Int position = queue.Dequeue();
                        cells.Add(position, model.Voxels[position]);
                        foreach (Vector3Int direction in s_FaceNeighbors)
                        {
                            Vector3Int neighbor = position + direction;
                            if (remaining.Remove(neighbor)) queue.Enqueue(neighbor);
                        }
                    }
                    components.Add((model, cells));
                }
            }
            if (components.Count <= 1) return Array.Empty<RuntimeVoxDocument>();

            var result = new List<RuntimeVoxDocument>(components.Count);
            foreach (var component in components)
            {
                RuntimeModel model = component.Model;
                // A split output is fresh internal content, not a filtered source file.
                var region = new RuntimeVoxDocument();
                Array.Copy(Palette, region.Palette, Palette.Length);
                foreach (byte index in component.Cells.Values.Distinct())
                {
                    if (m_materials.TryGetValue(index, out RuntimeMaterial material))
                        region.m_materials.Add(index, material);
                }
                string name = string.IsNullOrWhiteSpace(model.Name) ? "Voxel" : model.Name;
                var instance = new RuntimeModel($"{name} Region {result.Count + 1}",
                    model.Size, sourceModelId: -1, isCopy: false, voxels: component.Cells,
                    localTransformOffset: GetSceneTranslation(model),
                    globalRotation: model.GlobalRotation, localRotation: model.GlobalRotation)
                {
                    TransformOffset = model.TransformOffset,
                };
                region.m_models.Add(instance);
                result.Add(region);
            }
            return result;
        }

        public RuntimeVoxDocument CloneDocument()
        {
            var copy = new RuntimeVoxDocument { m_sourceVoxBytes = m_sourceVoxBytes };
            Array.Copy(Palette, copy.Palette, Palette.Length);
            foreach (var material in m_materials) copy.m_materials.Add(material.Key, material.Value);
            var sharedDefinitions = new Dictionary<VoxelDefinition, VoxelDefinition>();
            foreach (RuntimeModel model in m_models)
            {
                VoxelDefinition definition;
                if (!sharedDefinitions.TryGetValue(model.Definition, out definition))
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
