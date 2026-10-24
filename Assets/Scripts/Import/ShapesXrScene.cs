// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class ShapesXrObjectState
    {
        // Native Group/GroupPreset protocol identity; no vendor prefab is required.
        private static readonly Guid kGroupPreset = new Guid("03ddfa60-93c1-4fb1-acef-d632575a106c");
        public ShapesXrEntity Entity { get; }
        public JObject Properties { get; }
        public bool HasSnapshot { get; }
        public bool IsGroup { get; }
        public Guid StateId { get; }
        public Guid ParentId { get; }
        public ShapesXrObjectState Parent { get; internal set; }
        public bool Visible => HasSnapshot && ((bool?)Properties["obj-isa"] ?? true) && (Parent?.Visible ?? true);
        public bool Locked => (bool?)Properties["obj-l"] ?? false;
        internal ShapesXrObjectState(ShapesXrEntity entity, JObject properties, bool hasSnapshot, Guid stateId)
        {
            Entity = entity; Properties = properties; HasSnapshot = hasSnapshot;
            StateId = stateId;
            IsGroup = properties["a"] != null && ShapesXrDocument.ReadGuid(properties["a"]) == kGroupPreset;
            if (properties["pi"] is JArray parent)
            {
                if (parent.Count != 2 || parent[1].Type != JTokenType.Boolean) { throw new InvalidDataException("Invalid ShapesXR parent info"); }
                ParentId = ShapesXrDocument.ReadGuid(parent[0]);
            }
        }

        public NativePlacement Placement() => new NativePlacement(Transforms());

        private IEnumerable<(Vector3 Position, Quaternion Rotation, Vector3 Scale)> Transforms()
        {
            for (var current = this; current != null; current = current.Parent)
            {
                var value = current.Properties["t"] as JArray ?? throw new InvalidDataException($"Missing ShapesXR transform for {current.Entity.Id}");
                if (value.Count != 3) { throw new InvalidDataException("Invalid ShapesXR transform"); }
                var p = Vector(value[0], 3); var r = Vector(value[1], 4); var s = Vector(value[2], 3);
                yield return (new Vector3(p[0], p[1], p[2]), new Quaternion(r[0], r[1], r[2], r[3]), new Vector3(s[0], s[1], s[2]));
            }
        }

        private static float[] Vector(JToken value, int count)
        {
            if (!(value is JArray array) || array.Count != count) { throw new InvalidDataException("Invalid ShapesXR transform vector"); }
            var result = new float[count];
            for (int i = 0; i < count; ++i)
            {
                if (array[i].Type != JTokenType.Integer && array[i].Type != JTokenType.Float) { throw new InvalidDataException("Expected numeric ShapesXR transform components"); }
                result[i] = (float)array[i];
            }
            return result;
        }
    }

    /// Resolves one document state for import. It does not implement frame navigation or playback.
    public sealed class ShapesXrScene
    {
        private static readonly Guid kBackgroundScene = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public Guid SceneId { get; }
        public Guid StateId { get; }
        public string Name { get; }
        public IReadOnlyDictionary<Guid, ShapesXrObjectState> Objects => m_Objects;
        private readonly Dictionary<Guid, ShapesXrObjectState> m_Objects = new Dictionary<Guid, ShapesXrObjectState>();

        public ShapesXrScene(ShapesXrDocument document, Guid? sceneId = null, Guid? stateId = null)
        {
            if (document.Version == 0) { throw new NotSupportedException("Unversioned ShapesXR state migration is not implemented"); }
            var space = document.OfKind(0).SingleOrDefault() ?? throw new InvalidDataException("ShapesXR space entity is missing");
            var order = space.Properties["sp-sco"] as JArray;
            SceneId = sceneId ?? (order != null && order.Count > 0 ? ShapesXrDocument.ReadGuid(order[0]) :
                document.OfKind(1).OrderBy(entity => entity.Id).FirstOrDefault()?.Id ?? throw new InvalidDataException("ShapesXR space has no scenes"));
            if (!document.Entities.TryGetValue(SceneId, out var scene) || !scene.Exists || scene.Kind != 1)
            {
                throw new InvalidDataException($"Missing ShapesXR scene: {SceneId}");
            }
            StateId = stateId ?? InitialState(document, scene);
            bool validState = document.Version == 3 ? StateId == SceneId :
                document.Entities.TryGetValue(StateId, out var state) && state.Exists && state.Kind == 2 &&
                ShapesXrDocument.ReadGuid(state.Properties["st-z"]) == SceneId;
            if (!validState)
            {
                throw new InvalidDataException("ShapesXR state does not belong to the selected scene");
            }
            Name = (string)scene.Properties["sc-nm"] ?? "ShapesXR scene";
            Guid backgroundState = Guid.Empty;
            if (document.Entities.TryGetValue(kBackgroundScene, out var background) && background.Exists && background.Kind == 1)
            {
                backgroundState = InitialState(document, background);
            }
            var snapshots = new HashSet<(Guid State, Guid Target)>();
            foreach (var snapshot in document.OfKind(4))
            {
                Guid snapshotState = ShapesXrDocument.ReadGuid(snapshot.Properties["snp-st"]);
                if (snapshotState != StateId && snapshotState != backgroundState) { continue; }
                Guid target = ShapesXrDocument.ReadGuid(snapshot.Properties["snp-ent"]);
                if (!snapshots.Add((snapshotState, target))) { throw new InvalidDataException($"Duplicate ShapesXR snapshot for {target}"); }
            }
            foreach (var entity in document.OfKind(3))
            {
                string sceneProperty = document.Version == 3 ? "z" : "obj-z";
                Guid owner = entity.Properties[sceneProperty] == null ? Guid.Empty : ShapesXrDocument.ReadGuid(entity.Properties[sceneProperty]);
                Guid sourceState = owner == kBackgroundScene && backgroundState != Guid.Empty ? backgroundState : StateId;
                bool included = owner == SceneId || owner == kBackgroundScene || (document.Version >= 4 && owner == Guid.Empty);
                bool exists = included && (document.Version == 3 || snapshots.Contains((sourceState, entity.Id)));
                m_Objects.Add(entity.Id, new ShapesXrObjectState(entity, document.ResolveProperties(entity.Id, sourceState), exists, sourceState));
            }
            foreach (var entity in m_Objects.Values)
            {
                if (entity.ParentId != Guid.Empty)
                {
                    if (!m_Objects.TryGetValue(entity.ParentId, out var parent)) { throw new InvalidDataException($"Missing ShapesXR parent: {entity.ParentId}"); }
                    entity.Parent = parent;
                }
            }
            foreach (var entity in m_Objects.Values)
            {
                var ancestors = new HashSet<Guid>();
                for (var current = entity; current != null; current = current.Parent)
                {
                    if (!ancestors.Add(current.Entity.Id)) { throw new InvalidDataException("ShapesXR parent cycle"); }
                }
            }
        }

        private static Guid InitialState(ShapesXrDocument document, ShapesXrEntity scene)
        {
            if (document.Version == 3) { return scene.Id; }
            if (!(scene.Properties["sc-n"] is JArray states) || states.Count == 0) { throw new InvalidDataException("ShapesXR scene has no states"); }
            Guid id = ShapesXrDocument.ReadGuid(states[0]);
            if (!document.Entities.TryGetValue(id, out var state) || !state.Exists || state.Kind != 2 ||
                ShapesXrDocument.ReadGuid(state.Properties["st-z"]) != scene.Id)
            {
                throw new InvalidDataException("Invalid ShapesXR initial state");
            }
            return id;
        }
    }
}
