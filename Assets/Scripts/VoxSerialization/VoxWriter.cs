// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TiltBrush
{
    internal static class VoxWriter
    {
        private const int VoxVersion = 150;

        public static byte[] Write(RuntimeVoxDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            byte[] preservedSourceData = document.GetPreservedSourceData();
            if (preservedSourceData != null)
            {
                return WritePreservingSourceChunks(document, preservedSourceData);
            }

            if (document.Models.Count == 0)
            {
                throw new InvalidOperationException("RuntimeVoxDocument must contain at least one model.");
            }

            var contentChunks = new List<byte[]>();

            if (document.Models.Count > 1)
            {
                contentChunks.Add(BuildPackChunk(document.Models.Count));
            }

            for (int i = 0; i < document.Models.Count; i++)
            {
                RuntimeVoxDocument.RuntimeModel model = document.Models[i];
                contentChunks.Add(BuildSizeChunk(model.Size.x, model.Size.y, model.Size.z));
                contentChunks.Add(BuildXyziChunk(model));
            }

            if (document.Models.Count > 1 || document.Models.Any(model => model.TransformOffset != Vector3.zero))
            {
                contentChunks.AddRange(BuildSceneGraphChunks(document.Models));
            }

            byte[] rgbaChunk = BuildRgbaChunk(document.Palette);
            contentChunks.Add(rgbaChunk);

            int childrenLength = contentChunks.Sum(chunk => chunk.Length);

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("VOX "));
                writer.Write(VoxVersion);

                writer.Write(Encoding.ASCII.GetBytes("MAIN"));
                writer.Write(0);
                writer.Write(childrenLength);

                foreach (byte[] chunk in contentChunks)
                {
                    writer.Write(chunk);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] BuildPackChunk(int modelCount)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(modelCount);
                return WrapChunk("PACK", stream.ToArray());
            }
        }

        private static byte[] BuildSizeChunk(int x, int y, int z)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
                return WrapChunk("SIZE", stream.ToArray());
            }
        }

        private static byte[] BuildXyziChunk(RuntimeVoxDocument.RuntimeModel model)
            => BuildXyziChunk(model, null);

        private static byte[] BuildXyziChunk(
            RuntimeVoxDocument.RuntimeModel model,
            IReadOnlyList<byte> logicalToRawPalette)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var voxels = new List<KeyValuePair<UnityEngine.Vector3Int, byte>>(model.Voxels);
                voxels.Sort((a, b) =>
                {
                    int xCompare = a.Key.x.CompareTo(b.Key.x);
                    if (xCompare != 0)
                    {
                        return xCompare;
                    }

                    int yCompare = a.Key.y.CompareTo(b.Key.y);
                    if (yCompare != 0)
                    {
                        return yCompare;
                    }

                    return a.Key.z.CompareTo(b.Key.z);
                });

                writer.Write(voxels.Count);
                foreach (KeyValuePair<UnityEngine.Vector3Int, byte> voxel in voxels)
                {
                    if (voxel.Key.x < 0 || voxel.Key.x > 255 ||
                        voxel.Key.y < 0 || voxel.Key.y > 255 ||
                        voxel.Key.z < 0 || voxel.Key.z > 255)
                    {
                        throw new InvalidOperationException("VOX writer currently supports voxel coordinates in [0,255].");
                    }

                    writer.Write((byte)voxel.Key.x);
                    writer.Write((byte)voxel.Key.y);
                    writer.Write((byte)voxel.Key.z);
                    byte paletteIndex = logicalToRawPalette == null
                        ? voxel.Value
                        : logicalToRawPalette[voxel.Value - 1];
                    writer.Write(paletteIndex);
                }

                return WrapChunk("XYZI", stream.ToArray());
            }
        }

        private static IEnumerable<byte[]> BuildSceneGraphChunks(IReadOnlyList<RuntimeVoxDocument.RuntimeModel> models)
        {
            const int rootTransformNodeId = 0;
            const int rootGroupNodeId = 1;
            const int modelTransformNodeStart = 10;
            const int modelShapeNodeStart = 1000;

            int[] modelTransformIds = new int[models.Count];
            for (int i = 0; i < models.Count; i++)
            {
                modelTransformIds[i] = modelTransformNodeStart + i;
            }

            yield return BuildTransformNodeChunk(
                rootTransformNodeId,
                attributes: new Dictionary<string, string>(),
                childNodeId: rootGroupNodeId,
                layerId: -1,
                translation: new Vector3Int(0, 0, 0),
                name: null);

            yield return BuildGroupNodeChunk(rootGroupNodeId, modelTransformIds);

            for (int i = 0; i < models.Count; i++)
            {
                int transformNodeId = modelTransformNodeStart + i;
                int shapeNodeId = modelShapeNodeStart + i;
                RuntimeVoxDocument.RuntimeModel model = models[i];

                yield return BuildTransformNodeChunk(
                    transformNodeId,
                    attributes: new Dictionary<string, string>(),
                    childNodeId: shapeNodeId,
                    layerId: -1,
                    // VOX translations are integers; round to the nearest voxel on export.
                    translation: Vector3Int.RoundToInt(model.TransformOffset),
                    name: model.Name);

                yield return BuildShapeNodeChunk(shapeNodeId, i);
            }
        }

        private static byte[] BuildTransformNodeChunk(
            int nodeId,
            IDictionary<string, string> attributes,
            int childNodeId,
            int layerId,
            Vector3Int translation,
            string name)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(nodeId);

                var mergedAttributes = new Dictionary<string, string>(attributes);
                if (!string.IsNullOrEmpty(name))
                {
                    mergedAttributes["_name"] = name;
                }

                WriteDictionary(writer, mergedAttributes);
                writer.Write(childNodeId);
                writer.Write(-1); // Reserved id.
                writer.Write(layerId);
                writer.Write(1); // Frame count.

                var frameDictionary = new Dictionary<string, string>();
                if (translation != Vector3Int.zero)
                {
                    frameDictionary["_t"] = $"{translation.x} {translation.y} {translation.z}";
                }

                WriteDictionary(writer, frameDictionary);
                return WrapChunk("nTRN", stream.ToArray());
            }
        }

        private static byte[] BuildGroupNodeChunk(int nodeId, IReadOnlyList<int> childNodeIds)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(nodeId);
                WriteDictionary(writer, new Dictionary<string, string>());
                writer.Write(childNodeIds.Count);
                foreach (int childNodeId in childNodeIds)
                {
                    writer.Write(childNodeId);
                }

                return WrapChunk("nGRP", stream.ToArray());
            }
        }

        private static byte[] BuildShapeNodeChunk(int nodeId, int modelId)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(nodeId);
                WriteDictionary(writer, new Dictionary<string, string>());
                writer.Write(1); // Model count.
                writer.Write(modelId);
                WriteDictionary(writer, new Dictionary<string, string>());
                return WrapChunk("nSHP", stream.ToArray());
            }
        }

        private static byte[] BuildRgbaChunk(UnityEngine.Color32[] palette)
        {
            if (palette == null || palette.Length != 256)
            {
                throw new InvalidOperationException("Palette must contain exactly 256 colors.");
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < palette.Length; i++)
                {
                    UnityEngine.Color32 color = palette[i];
                    writer.Write(color.r);
                    writer.Write(color.g);
                    writer.Write(color.b);
                    writer.Write(color.a);
                }

                return WrapChunk("RGBA", stream.ToArray());
            }
        }

        private static byte[] WritePreservingSourceChunks(
            RuntimeVoxDocument document,
            byte[] sourceData)
        {
            PreservedVoxFile source = PreservedVoxFile.Parse(sourceData);
            List<PreservedChunk> chunks = source.Main.Children;
            List<PreservedChunk> sizeChunks = chunks.Where(chunk => chunk.Id == "SIZE").ToList();
            List<PreservedChunk> voxelChunks = chunks.Where(chunk => chunk.Id == "XYZI").ToList();

            if (sizeChunks.Count != voxelChunks.Count)
            {
                throw new InvalidDataException(
                    "Cannot preserve VOX source because its SIZE and XYZI chunk counts differ.");
            }

            var modelsBySourceId = new Dictionary<int, RuntimeVoxDocument.RuntimeModel>();
            foreach (RuntimeVoxDocument.RuntimeModel model in document.Models)
            {
                if (model.SourceModelId < 0 || model.SourceModelId >= sizeChunks.Count)
                {
                    throw new InvalidOperationException(
                        "Adding model definitions to an imported VOX scene is not supported yet.");
                }
                modelsBySourceId.TryAdd(model.SourceModelId, model);
            }

            byte[] logicalToRawPalette = Enumerable.Range(1, 255).Select(value => (byte)value).ToArray();
            PreservedChunk indexMap = chunks.FirstOrDefault(chunk => chunk.Id == "IMAP");
            if (indexMap != null)
            {
                if (indexMap.Content.Length < byte.MaxValue)
                {
                    throw new InvalidDataException("VOX IMAP chunk is shorter than 255 entries.");
                }
                Array.Copy(indexMap.Content, logicalToRawPalette, logicalToRawPalette.Length);
            }

            int sizeIndex = 0;
            int voxelIndex = 0;
            foreach (PreservedChunk chunk in chunks)
            {
                if (chunk.Id == "SIZE")
                {
                    if (modelsBySourceId.TryGetValue(
                            sizeIndex,
                            out RuntimeVoxDocument.RuntimeModel model))
                    {
                        chunk.Content = ExtractChunkContent(BuildSizeChunk(
                            model.Size.x,
                            model.Size.y,
                            model.Size.z));
                    }
                    sizeIndex++;
                }
                else if (chunk.Id == "XYZI")
                {
                    if (modelsBySourceId.TryGetValue(
                            voxelIndex,
                            out RuntimeVoxDocument.RuntimeModel model))
                    {
                        chunk.Content = ExtractChunkContent(BuildXyziChunk(
                            model,
                            logicalToRawPalette));
                    }
                    voxelIndex++;
                }
                else if (chunk.Id == "RGBA")
                {
                    chunk.Content = BuildPreservedRgbaContent(
                        document.Palette,
                        chunk.Content,
                        logicalToRawPalette);
                }
            }

            return source.Write();
        }

        private static byte[] BuildPreservedRgbaContent(
            Color32[] palette,
            byte[] originalContent,
            IReadOnlyList<byte> logicalToRawPalette)
        {
            if (palette == null || palette.Length != 256)
            {
                throw new InvalidOperationException("Palette must contain exactly 256 colors.");
            }
            if (originalContent == null || originalContent.Length != 1024)
            {
                throw new InvalidDataException("VOX RGBA chunk must contain exactly 256 colors.");
            }

            byte[] content = (byte[])originalContent.Clone();
            for (int logicalIndex = 0; logicalIndex < byte.MaxValue; logicalIndex++)
            {
                int rawIndex = logicalToRawPalette[logicalIndex] - 1;
                if (rawIndex < 0 || rawIndex >= byte.MaxValue)
                {
                    throw new InvalidDataException("VOX IMAP contains an invalid palette index.");
                }

                Color32 color = palette[logicalIndex];
                int offset = rawIndex * 4;
                content[offset] = color.r;
                content[offset + 1] = color.g;
                content[offset + 2] = color.b;
                content[offset + 3] = color.a;
            }
            return content;
        }

        private static byte[] ExtractChunkContent(byte[] chunk)
        {
            int contentLength = BitConverter.ToInt32(chunk, 4);
            var content = new byte[contentLength];
            Buffer.BlockCopy(chunk, 12, content, 0, contentLength);
            return content;
        }

        private sealed class PreservedVoxFile
        {
            public int Version { get; private set; }
            public PreservedChunk Main { get; private set; }
            public byte[] TrailingData { get; private set; }

            public static PreservedVoxFile Parse(byte[] data)
            {
                if (data == null || data.Length < 20)
                {
                    throw new InvalidDataException("VOX source is too short.");
                }

                using (var stream = new MemoryStream(data, writable: false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
                {
                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "VOX ")
                    {
                        throw new InvalidDataException("VOX source has an invalid header.");
                    }

                    var result = new PreservedVoxFile
                    {
                        Version = reader.ReadInt32(),
                        Main = PreservedChunk.Read(reader, stream.Length),
                    };
                    if (result.Main.Id != "MAIN")
                    {
                        throw new InvalidDataException("VOX source does not contain a MAIN root chunk.");
                    }
                    result.TrailingData = reader.ReadBytes(checked((int)(stream.Length - stream.Position)));
                    return result;
                }
            }

            public byte[] Write()
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Encoding.ASCII.GetBytes("VOX "));
                    writer.Write(Version);
                    Main.Write(writer);
                    writer.Write(TrailingData);
                    writer.Flush();
                    return stream.ToArray();
                }
            }
        }

        private sealed class PreservedChunk
        {
            public string Id { get; private set; }
            public byte[] Content { get; set; }
            public List<PreservedChunk> Children { get; private set; }

            public static PreservedChunk Read(BinaryReader reader, long containingEnd)
            {
                if (containingEnd - reader.BaseStream.Position < 12)
                {
                    throw new InvalidDataException("VOX chunk header is truncated.");
                }

                string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
                int contentLength = reader.ReadInt32();
                int childrenLength = reader.ReadInt32();
                if (contentLength < 0 || childrenLength < 0)
                {
                    throw new InvalidDataException("VOX chunk has a negative length.");
                }

                long chunkEnd = checked(reader.BaseStream.Position + contentLength + childrenLength);
                if (chunkEnd > containingEnd)
                {
                    throw new InvalidDataException($"VOX chunk '{id}' exceeds its container.");
                }

                byte[] content = reader.ReadBytes(contentLength);
                long childrenEnd = reader.BaseStream.Position + childrenLength;
                var children = new List<PreservedChunk>();
                while (reader.BaseStream.Position < childrenEnd)
                {
                    children.Add(Read(reader, childrenEnd));
                }
                if (reader.BaseStream.Position != childrenEnd)
                {
                    throw new InvalidDataException($"VOX chunk '{id}' has malformed children.");
                }

                return new PreservedChunk
                {
                    Id = id,
                    Content = content,
                    Children = children,
                };
            }

            public void Write(BinaryWriter writer)
            {
                using (var childrenStream = new MemoryStream())
                using (var childrenWriter = new BinaryWriter(childrenStream))
                {
                    foreach (PreservedChunk child in Children)
                    {
                        child.Write(childrenWriter);
                    }
                    childrenWriter.Flush();
                    byte[] childrenData = childrenStream.ToArray();

                    writer.Write(Encoding.ASCII.GetBytes(Id));
                    writer.Write(Content.Length);
                    writer.Write(childrenData.Length);
                    writer.Write(Content);
                    writer.Write(childrenData);
                }
            }
        }

        private static byte[] WrapChunk(string id, byte[] content)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes(id));
                writer.Write(content.Length);
                writer.Write(0);
                writer.Write(content);
                return stream.ToArray();
            }
        }

        private static void WriteDictionary(BinaryWriter writer, IDictionary<string, string> dictionary)
        {
            writer.Write(dictionary.Count);
            foreach (KeyValuePair<string, string> pair in dictionary)
            {
                WriteString(writer, pair.Key);
                WriteString(writer, pair.Value);
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
