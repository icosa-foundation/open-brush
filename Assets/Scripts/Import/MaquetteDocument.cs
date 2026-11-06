// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public enum MaquetteEntityKind { Primitive, Procedural, Stroke, Text, Light, Hotspot, Control, Group }

    public sealed class MaquetteEntity
    {
        public string Id { get; }
        public MaquetteEntityKind Kind { get; }
        public JObject Data { get; }
        internal MaquetteEntity(string id, MaquetteEntityKind kind, JObject data)
        {
            Id = id;
            Kind = kind;
            Data = data;
        }
    }

    public sealed class MaquetteScene
    {
        public string Id => (string)Data["sceneGuid"];
        public string Name => (string)Data["sceneName"];
        public int Order => (int?)Data["sequenceIndex"] ?? -1;
        public string Collection { get; }
        public JObject Data { get; }
        internal MaquetteScene(string collection, JObject data) { Collection = collection; Data = data; }
    }

    /// State transforms are retained in their native coordinate frame. Root transforms are
    /// canvas-relative; nested state transforms are local. Conversion belongs in the scene adapter.
    public sealed class MaquetteState
    {
        public string Id => (string)Data["entityStateGuid"];
        public string EntityId => (string)Data["targetEntityGuid"];
        public MaquetteEntity Entity { get; }
        public MaquetteState Parent { get; }
        public JObject Data { get; }
        public bool Visible => ((bool?)Data["visible"] ?? true) && (Parent?.Visible ?? true);

        /// Maquette's builders multiply ancestor scale components; they do not use lossyScale
        /// or matrix column lengths. Rotation must therefore not change these construction inputs.
        public float[] GetBuilderScale()
        {
            var scale = new[] { 1f, 1f, 1f };
            for (var state = this; state != null; state = state.Parent)
            {
                var values = state.Data["cachedTransform"]?["scale"];
                for (int i = 0; i < 3; ++i)
                {
                    var value = values?["xyz"[i].ToString()];
                    if (value == null) { continue; }
                    if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)
                    {
                        throw new InvalidDataException("Maquette builder scale must be numeric");
                    }
                    scale[i] *= (float)value;
                    if (float.IsNaN(scale[i]) || float.IsInfinity(scale[i]))
                    {
                        throw new InvalidDataException("Maquette builder scale must be finite");
                    }
                }
            }
            return scale;
        }
        internal MaquetteState(JObject data, MaquetteEntity entity, MaquetteState parent)
        {
            Data = data;
            Entity = entity;
            Parent = parent;
        }
    }

    /// Reads native state and resource data; does not invoke vendor assemblies or copy vendor assets.
    /// Original JSON sections remain available, including fields not understood by this reader.
    public sealed class MaquetteDocument : IDisposable
    {
        public NativeProjectSource Source { get; }
        public IReadOnlyDictionary<string, JObject> Sections => m_Sections;
        public IReadOnlyDictionary<string, MaquetteEntity> Entities => m_Entities;
        public IReadOnlyList<MaquetteScene> Scenes => m_Scenes;
        public JObject Manifest => m_Sections["manifest.json.mq"];
        public int Version => (int?)m_Sections["scenes.json"]["version"] ?? 0;
        public string SelectedSceneId => (string)m_Sections["scenes.json"]["lastSelectedSceneGuid"];
        private readonly Dictionary<string, JObject> m_Sections = new Dictionary<string, JObject>();
        private readonly Dictionary<string, MaquetteEntity> m_Entities = new Dictionary<string, MaquetteEntity>(StringComparer.OrdinalIgnoreCase);
        private readonly List<MaquetteScene> m_Scenes = new List<MaquetteScene>();

        public MaquetteDocument(string path)
        {
            Source = new NativeProjectSource(path);
            try
            {
                m_Sections.Add("manifest.json.mq", Source.ReadJson("manifest.json.mq"));
                m_Sections.Add("scenes.json", Source.ReadJson("scenes.json"));
                ReadEntities("primitives.json", "primitives", MaquetteEntityKind.Primitive);
                ReadEntities("proceduralmeshes.json", "proceduralMeshes", MaquetteEntityKind.Procedural);
                ReadEntities("brushes.json", "strokes", MaquetteEntityKind.Stroke);
                ReadEntities("textindex.json", "textEntities", MaquetteEntityKind.Text);
                ReadEntities("lights.json", "lights", MaquetteEntityKind.Light);
                ReadEntities("hotspots.json", "hotspots", MaquetteEntityKind.Hotspot);
                ReadEntities("uicontrols.json", "uiControls", MaquetteEntityKind.Control);
                ReadEntities("named_groups.json", "groups", MaquetteEntityKind.Group);
                foreach (string collection in new[] { "scenes", "slides", "stages" })
                {
                    foreach (var scene in Objects(m_Sections["scenes.json"][collection], collection))
                    {
                        string id = RequiredId(scene, "sceneGuid");
                        if (m_Scenes.Any(existing => string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)))
                        {
                            throw new InvalidDataException($"Duplicate Maquette scene: {id}");
                        }
                        m_Scenes.Add(new MaquetteScene(collection, scene));
                    }
                }
                foreach (string name in Source.Files.Where(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                             && !name.Contains("/")).ToArray())
                {
                    if (!m_Sections.ContainsKey(name)) { m_Sections.Add(name, Source.ReadJson(name)); }
                }
            }
            catch { Source.Dispose(); throw; }
        }

        public MaquetteScene SelectScene(string sceneId = null)
        {
            string id = sceneId ?? SelectedSceneId;
            var selected = m_Scenes.FirstOrDefault(scene => string.Equals(scene.Id, id, StringComparison.OrdinalIgnoreCase));
            if (selected != null) { return selected; }
            if (sceneId != null) { throw new InvalidDataException($"Maquette scene not found: {sceneId}"); }
            return m_Scenes.OrderBy(scene => scene.Collection == "stages" ? 1 : 0)
                       .ThenBy(scene => scene.Order).FirstOrDefault()
                   ?? throw new InvalidDataException("Maquette project has no scenes");
        }

        public IReadOnlyList<MaquetteState> ReadStates(MaquetteScene scene)
        {
            if (!m_Scenes.Contains(scene)) { throw new ArgumentException("Scene belongs to another document", nameof(scene)); }
            var states = new List<MaquetteState>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Visit(scene.Data["treeRoots"], null, states, ids);
            return states;
        }

        /// Resolve the static view of a slide, including master-stage content. The source
        /// restores the slide after the stage, so shared entities use the slide's state.
        /// This does not add scene switching or alter the original source records.
        public IReadOnlyList<MaquetteState> ReadComposedStates(MaquetteScene scene)
        {
            var selected = ReadStates(scene);
            var master = m_Scenes.FirstOrDefault(value => value.Collection == "stages");
            if (scene.Collection != "slides" || master == null) { return selected; }
            var states = new Dictionary<string, MaquetteState>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            foreach (var state in ReadStates(master).Concat(selected))
            {
                if (!states.ContainsKey(state.EntityId)) { order.Add(state.EntityId); }
                states[state.EntityId] = state;
            }
            var resolved = new Dictionary<string, MaquetteState>(StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            MaquetteState Resolve(string id)
            {
                if (resolved.TryGetValue(id, out var result)) { return result; }
                if (!visiting.Add(id)) { throw new InvalidDataException("Maquette composed hierarchy contains a cycle"); }
                var source = states[id];
                var parent = source.Parent == null ? null : Resolve(source.Parent.EntityId);
                result = new MaquetteState(source.Data, source.Entity, parent);
                resolved.Add(id, result);
                visiting.Remove(id);
                return result;
            }
            return order.Select(Resolve).ToArray();
        }

        private void Visit(JToken nodes, MaquetteState parent, List<MaquetteState> states, HashSet<string> ids)
        {
            foreach (var data in Objects(nodes, "treeRoots/children"))
            {
                string id = RequiredId(data, "entityStateGuid");
                if (!ids.Add(id)) { throw new InvalidDataException($"Duplicate Maquette entity state: {id}"); }
                string target = RequiredId(data, "targetEntityGuid");
                m_Entities.TryGetValue(target, out var entity);
                var state = new MaquetteState(data, entity, parent);
                states.Add(state);
                Visit(data["children"], state, states, ids);
            }
        }

        private void ReadEntities(string section, string key, MaquetteEntityKind kind)
        {
            if (!Source.Contains(section)) { return; }
            var data = Source.ReadJson(section);
            m_Sections.Add(section, data);
            foreach (var item in Objects(data[key], key))
            {
                string id = RequiredId(item, "guid");
                if (m_Entities.ContainsKey(id)) { throw new InvalidDataException($"Duplicate Maquette entity: {id}"); }
                m_Entities.Add(id, new MaquetteEntity(id, kind, item));
            }
        }

        private static IEnumerable<JObject> Objects(JToken value, string label)
        {
            if (value == null || value.Type == JTokenType.Null) { yield break; }
            if (!(value is JArray array)) { throw new InvalidDataException($"Expected Maquette array: {label}"); }
            foreach (var item in array)
            {
                if (!(item is JObject data)) { throw new InvalidDataException($"Expected Maquette object: {label}"); }
                yield return data;
            }
        }

        private static string RequiredId(JObject data, string key)
        {
            string id = (string)data[key];
            if (string.IsNullOrEmpty(id)) { throw new InvalidDataException($"Missing Maquette {key}"); }
            return id;
        }

        public void Dispose() { Source.Dispose(); }
    }
}
