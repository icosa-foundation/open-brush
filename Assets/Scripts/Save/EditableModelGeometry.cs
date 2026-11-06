using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Polyhydra.Core;
using UnityEngine;

namespace TiltBrush
{
    // Base polygon topology, rather than the triangulated result of applying modifiers.
    // Deflate keeps these entries compact even when the containing .tilt uses stored entries.
    internal static class EditableModelGeometry
    {
        public static void Write(Stream stream, EditableModelDefinitionData savedDefinition)
        {
            var definition = PolyRecipeSerialization.LegacyView(savedDefinition);
            using var compressed = new DeflateStream(stream, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true);
            using var writer = new BinaryWriter(compressed, Encoding.UTF8, leaveOpen: true);
            WriteList(writer, definition.Vertices, vertex =>
            {
                writer.Write(vertex.x);
                writer.Write(vertex.y);
                writer.Write(vertex.z);
            });
            WriteList(writer, definition.Faces, face => WriteList(writer, face, writer.Write));
            WriteList(writer, definition.FaceRoles, role => writer.Write((int)role));
            WriteList(writer, definition.VertexRoles, role => writer.Write((int)role));
            WriteList(writer, definition.FaceTags, tags =>
                WriteList(writer, tags?.OrderBy(tag => tag, StringComparer.Ordinal).ToArray(), writer.Write));
        }

        public static void Read(Stream stream, EditableModelDefinitionData savedDefinition)
        {
            var definition = PolyRecipeSerialization.LegacyView(savedDefinition);
            using var compressed = new DeflateStream(stream, CompressionMode.Decompress, leaveOpen: true);
            using var reader = new BinaryReader(compressed, Encoding.UTF8, leaveOpen: true);
            definition.Vertices = ReadList(reader,
                () => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()))?.ToArray();
            definition.Faces = ReadList(reader, () => ReadList(reader, reader.ReadInt32))?.ToArray();
            definition.FaceRoles = ReadList(reader, () => (Roles)reader.ReadInt32());
            definition.VertexRoles = ReadList(reader, () => (Roles)reader.ReadInt32());
            definition.FaceTags = ReadList(reader, () =>
            {
                var tags = ReadList(reader, reader.ReadString);
                return tags == null ? new HashSet<string>() : new HashSet<string>(tags);
            });
            if (definition.Vertices == null || definition.Faces == null ||
                definition.Faces.Any(face => face == null || face.Count < 3 ||
                    face.Any(index => index < 0 || index >= definition.Vertices.Length)))
            {
                throw new InvalidDataException("Invalid editable model polygon geometry.");
            }
            PolyRecipeSerialization.StoreGeometry(savedDefinition, definition);
        }

        private static void WriteList<T>(BinaryWriter writer, ICollection<T> values, Action<T> write)
        {
            writer.Write(values?.Count ?? -1);
            if (values == null) { return; }
            foreach (T value in values) { write(value); }
        }

        private static List<T> ReadList<T>(BinaryReader reader, Func<T> read)
        {
            int count = reader.ReadInt32();
            if (count == -1) { return null; }
            if (count < 0) { throw new InvalidDataException($"Invalid geometry element count: {count}"); }
            // Grow as data is read, so a corrupt count cannot trigger a large initial allocation.
            var values = new List<T>();
            for (int i = 0; i < count; i++) { values.Add(read()); }
            return values;
        }
    }
}
