// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TiltBrush.DocumentImport
{
    public sealed class ShapesXrEntity
    {
        public Guid Id { get; }
        public int Kind { get; internal set; } = -1;
        public bool Exists { get; internal set; }
        public JObject Properties { get; } = new JObject();
        internal ShapesXrEntity(Guid id) { Id = id; }
    }

    /// Native .space edit-log reader. Keeps the original data and replays creation/property edits.
    /// Source schema versions stay separate from Open Brush's document and geometry versions.
    public sealed class ShapesXrDocument
    {
        public JArray Data { get; }
        public int Version { get; }
        public int EditCount => ((JArray)Data[1]).Count;
        public IReadOnlyDictionary<Guid, ShapesXrEntity> Entities => m_Entities;
        public IReadOnlyList<JToken> UnknownEdits => m_UnknownEdits;
        private readonly Dictionary<Guid, ShapesXrEntity> m_Entities = new Dictionary<Guid, ShapesXrEntity>();
        private readonly List<JToken> m_UnknownEdits = new List<JToken>();

        public ShapesXrDocument(string path) : this(File.ReadAllBytes(path)) { }

        public ShapesXrDocument(byte[] bytes)
        {
            Data = MessagePackDataReader.Decode(bytes) as JArray;
            if (Data == null || Data.Count < 2 || !(Data[0] is JArray header) || header.Count < 1 || !(Data[1] is JArray edits))
            {
                throw new InvalidDataException("Invalid ShapesXR space header/edit log");
            }
            // Before explicit schema versions, the header was [edition, editCount].
            // Modern headers are [schemaVersion, [editionKind, editCount]].
            Version = header.Count > 1 && header[1].Type == JTokenType.Integer ? 0 : Integer(header[0], "schema version");
            if (Version != 0 && (Version < 3 || Version > 5))
            {
                throw new NotSupportedException($"ShapesXR schema version {Version} is not supported");
            }
            foreach (var value in edits)
            {
                if (!(value is JArray edit) || edit.Count != 2)
                {
                    throw new InvalidDataException("Invalid ShapesXR edit union");
                }
                int tag = Integer(edit[0], "edit type");
                if (tag < 0 || tag > 37)
                {
                    m_UnknownEdits.Add(value);
                    continue;
                }
                if (!(edit[1] is JArray fields) || fields.Count < (tag == 0 && Version == 0 ? 2 : 3))
                {
                    throw new InvalidDataException("Invalid ShapesXR edit fields");
                }
                Guid id = ReadGuid(fields[0]);
                if (!m_Entities.TryGetValue(id, out var entity))
                {
                    entity = new ShapesXrEntity(id);
                    m_Entities.Add(id, entity);
                }
                if (tag == 0)
                {
                    if (fields[1].Type != JTokenType.Boolean) { throw new InvalidDataException("Invalid ShapesXR creation flag"); }
                    if (Version != 0) { entity.Kind = Integer(fields[2], "entity kind"); }
                    entity.Exists = (bool)fields[1];
                }
                else
                {
                    if (fields[1].Type != JTokenType.String) { throw new InvalidDataException("Invalid ShapesXR property name"); }
                    entity.Properties[(string)fields[1]] = fields[2].DeepClone();
                }
            }
        }

        public IEnumerable<ShapesXrEntity> OfKind(int kind) => m_Entities.Values.Where(entity => entity.Exists && entity.Kind == kind);

        /// Resolve an entity's properties with an explicitly selected snapshot. Native field names
        /// remain intact here; geometry/material adapters translate them to the shared representation.
        public JObject ResolveProperties(Guid entityId, Guid stateId)
        {
            if (Version < 4) { throw new NotSupportedException("Pre-snapshot ShapesXR state migration is not implemented"); }
            if (!m_Entities.TryGetValue(entityId, out var entity) || !entity.Exists)
            {
                throw new InvalidDataException($"ShapesXR entity not found: {entityId}");
            }
            var result = (JObject)entity.Properties.DeepClone();
            var snapshots = OfKind(4).Where(snapshot => ReadGuid(snapshot.Properties["snp-ent"]) == entityId
                                                      && ReadGuid(snapshot.Properties["snp-st"]) == stateId).ToArray();
            if (snapshots.Length > 1) { throw new InvalidDataException($"Ambiguous ShapesXR snapshot for {entityId} in {stateId}"); }
            if (snapshots.Length == 1)
            {
                foreach (var property in snapshots[0].Properties.Properties())
                {
                    if (property.Name != "snp-ent" && property.Name != "snp-st")
                    {
                        result[property.Name] = property.Value.DeepClone();
                    }
                }
            }
            return result;
        }

        public static Guid ReadGuid(JToken value)
        {
            if (value is JValue scalar && scalar.Value is byte[] bytes && bytes.Length == 16) { return new Guid(bytes); }
            throw new InvalidDataException("Expected ShapesXR 16-byte GUID");
        }

        private static int Integer(JToken value, string label)
        {
            if (value == null || value.Type != JTokenType.Integer) { throw new InvalidDataException($"Invalid ShapesXR {label}"); }
            try { return (int)value; }
            catch (OverflowException error) { throw new InvalidDataException($"ShapesXR {label} is out of range", error); }
        }
    }
}
