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
using System.Text;
using UnityEngine;
using VoxReader.Interfaces;

namespace TiltBrush
{
    public sealed class RuntimeVoxDocument
    {
        // XYZI stores each voxel coordinate in one byte, so valid coordinates are
        // 0..255 and each model dimension can contain at most 256 cells.
        public const int MaxModelDimension = byte.MaxValue + 1;

        internal sealed class WidgetViewState
        {
            public ModelWidget Widget;
            public bool WidgetBacked;
            public bool WidgetWasCreated;
            public bool VisualsDirty = true;
            public bool AutoVisuals;
            public bool OptimizedMesh = true;
            public TrTransform WidgetTransform = TrTransform.identity;
        }

        public sealed class RuntimeModel
        {
            private readonly Dictionary<Vector3Int, byte> m_voxels;

            public string Name { get; }
            public Vector3Int Size { get; }
            // VOX-world position of local voxel (0, 0, 0). Together with GlobalRotation,
            // this is the affine transform used for rendering and editing.
            public Vector3 TransformOffset { get; set; }
            public Vector3 LocalTransformOffset { get; }
            public Matrix4x4 GlobalRotation { get; }
            public Matrix4x4 LocalRotation { get; }
            public int SourceModelId { get; }
            public bool IsCopy { get; }
            public bool IsVisible { get; }
            public IReadOnlyDictionary<Vector3Int, byte> Voxels => m_voxels;

            public RuntimeModel(string name, Vector3Int size)
                : this(name, size, sourceModelId: 0, isCopy: false)
            {
            }

            internal RuntimeModel(
                string name,
                Vector3Int size,
                int sourceModelId,
                bool isCopy,
                Dictionary<Vector3Int, byte> voxels = null,
                Vector3? localTransformOffset = null,
                Matrix4x4? globalRotation = null,
                Matrix4x4? localRotation = null,
                bool isVisible = true)
            {
                if (size.x <= 0 || size.y <= 0 || size.z <= 0 ||
                    size.x > MaxModelDimension ||
                    size.y > MaxModelDimension ||
                    size.z > MaxModelDimension)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(size),
                        $"Model size dimensions must be between 1 and {MaxModelDimension} cells.");
                }

                Name = name;
                Size = size;
                SourceModelId = sourceModelId;
                IsCopy = isCopy;
                m_voxels = voxels ?? new Dictionary<Vector3Int, byte>();
                LocalTransformOffset = localTransformOffset ?? Vector3.zero;
                GlobalRotation = globalRotation ?? Matrix4x4.identity;
                LocalRotation = localRotation ?? Matrix4x4.identity;
                IsVisible = isVisible;
            }

            public bool AddOrUpdateVoxel(Vector3Int position, byte paletteIndex)
            {
                if (!IsInBounds(position) || paletteIndex == 0 ||
                    (m_voxels.TryGetValue(position, out byte previous) && previous == paletteIndex))
                {
                    return false;
                }

                m_voxels[position] = paletteIndex;
                return true;
            }

            public bool RemoveVoxel(Vector3Int position)
            {
                return m_voxels.Remove(position);
            }

            public bool MoveVoxel(Vector3Int from, Vector3Int to, bool overwrite = true)
            {
                if (!IsInBounds(to) || !m_voxels.TryGetValue(from, out byte paletteIndex))
                {
                    return false;
                }

                if (!overwrite && m_voxels.ContainsKey(to))
                {
                    return false;
                }

                m_voxels.Remove(from);
                m_voxels[to] = paletteIndex;
                return true;
            }

            public bool SetVoxelColor(Vector3Int position, byte paletteIndex)
            {
                if (paletteIndex == 0 || !m_voxels.ContainsKey(position))
                {
                    return false;
                }

                m_voxels[position] = paletteIndex;
                return true;
            }

            public bool TryGetPaletteIndex(Vector3Int position, out byte paletteIndex)
            {
                return m_voxels.TryGetValue(position, out paletteIndex);
            }

            public IEnumerable<RuntimeVoxel> EnumerateVoxels(Color32[] palette)
            {
                foreach (KeyValuePair<Vector3Int, byte> pair in m_voxels)
                {
                    int paletteOffset = pair.Value - 1;
                    if (paletteOffset < 0 || paletteOffset >= palette.Length)
                    {
                        continue;
                    }

                    yield return new RuntimeVoxel(pair.Key, pair.Value, palette[paletteOffset]);
                }
            }

            public bool IsInBounds(Vector3Int position)
            {
                return position.x >= 0 && position.x < Size.x &&
                       position.y >= 0 && position.y < Size.y &&
                       position.z >= 0 && position.z < Size.z;
            }
        }

        public readonly struct RuntimeVoxel
        {
            public Vector3Int Position { get; }
            public byte PaletteIndex { get; }
            public Color32 Color { get; }

            public RuntimeVoxel(Vector3Int position, byte paletteIndex, Color32 color)
            {
                Position = position;
                PaletteIndex = paletteIndex;
                Color = color;
            }
        }

        private readonly List<RuntimeModel> m_models = new List<RuntimeModel>();
        private byte[] m_sourceVoxBytes;

        public IReadOnlyList<RuntimeModel> Models => m_models;
        public bool HasPreservedSourceData => m_sourceVoxBytes != null;
        // Transient scene/API state. This is deliberately not included in VOX serialization.
        internal WidgetViewState ViewState { get; } = new WidgetViewState();

        // Palette is 1-based from VOX perspective. Palette[0] corresponds to index 1.
        public Color32[] Palette { get; } = new Color32[256];

        public RuntimeVoxDocument()
        {
            for (int i = 0; i < Palette.Length; i++)
            {
                byte value = (byte)i;
                Palette[i] = new Color32(value, value, value, 255);
            }
        }

        public RuntimeModel CreateModel(string name, Vector3Int size)
        {
            if (HasPreservedSourceData)
            {
                throw new InvalidOperationException(
                    "Adding models to an imported VOX document is not supported because its scene graph is preserved read-only.");
            }

            var model = new RuntimeModel(
                name,
                size,
                sourceModelId: m_models.Count,
                isCopy: false);
            m_models.Add(model);
            return model;
        }

        public bool ReplacePaletteEntry(int oneBasedIndex, Color32 color)
        {
            int zeroBased = oneBasedIndex - 1;
            if (oneBasedIndex < 1 || oneBasedIndex > byte.MaxValue)
            {
                return false;
            }

            Palette[zeroBased] = color;
            return true;
        }

        // Reuse exact colors first. Only scan voxel usage when allocating a new color,
        // which normally happens when the user changes their brush color, not per frame.
        // Never overwrite a palette entry referenced by any model in the document.
        public byte GetOrAddPaletteColor(Color32 color)
        {
            int nearestIndex = 1;
            int nearestDistance = int.MaxValue;
            for (int index = 1; index <= byte.MaxValue; index++)
            {
                Color32 candidate = Palette[index - 1];
                int r = candidate.r - color.r;
                int g = candidate.g - color.g;
                int b = candidate.b - color.b;
                int a = candidate.a - color.a;
                int distance = r * r + g * g + b * b + a * a;
                if (distance == 0)
                {
                    return (byte)index;
                }
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = index;
                }
            }

            var used = new bool[byte.MaxValue + 1];
            foreach (RuntimeModel model in m_models)
            {
                foreach (byte index in model.Voxels.Values)
                {
                    used[index] = true;
                }
            }
            for (int index = 1; index <= byte.MaxValue; index++)
            {
                if (!used[index])
                {
                    Palette[index - 1] = color;
                    return (byte)index;
                }
            }

            // A VOX document can represent only 255 occupied colors.
            return (byte)nearestIndex;
        }

        public static RuntimeVoxDocument FromVoxFile(IVoxFile voxFile)
        {
            if (voxFile == null)
            {
                throw new ArgumentNullException(nameof(voxFile));
            }

            var document = new RuntimeVoxDocument();

            int paletteCount = Math.Min(byte.MaxValue, voxFile.Palette.Colors.Length);
            for (int i = 0; i < paletteCount; i++)
            {
                VoxReader.Color source = voxFile.Palette.Colors[i];
                document.Palette[i] = new Color32(source.R, source.G, source.B, source.A);
            }

            if (voxFile.Palette.RawColors.Length > byte.MaxValue)
            {
                VoxReader.Color unused = voxFile.Palette.RawColors[byte.MaxValue];
                document.Palette[byte.MaxValue] = new Color32(unused.R, unused.G, unused.B, unused.A);
            }

            var voxelDataBySourceId = new Dictionary<int, Dictionary<Vector3Int, byte>>();
            IReadOnlyList<bool> modelVisibility = GetModelVisibility(voxFile);
            int modelIndex = 0;

            foreach (IModel sourceModel in voxFile.Models)
            {
                var modelSize = new Vector3Int(
                    sourceModel.LocalSize.X,
                    sourceModel.LocalSize.Y,
                    sourceModel.LocalSize.Z);
                Matrix4x4 globalRotation = ToUnityMatrix(sourceModel.GlobalRotation);

                if (!voxelDataBySourceId.TryGetValue(
                        sourceModel.Id,
                        out Dictionary<Vector3Int, byte> sharedVoxels))
                {
                    sharedVoxels = new Dictionary<Vector3Int, byte>();
                    voxelDataBySourceId.Add(sourceModel.Id, sharedVoxels);
                }

                var runtimeModel = new RuntimeModel(
                    sourceModel.Name,
                    modelSize,
                    sourceModel.Id,
                    sourceModel.IsCopy,
                    sharedVoxels,
                    new Vector3(
                        sourceModel.LocalPosition.X,
                        sourceModel.LocalPosition.Y,
                        sourceModel.LocalPosition.Z),
                    globalRotation,
                    ToUnityMatrix(sourceModel.LocalRotation),
                    modelVisibility[modelIndex++]);
                document.m_models.Add(runtimeModel);
                runtimeModel.TransformOffset = GetTransformOffset(sourceModel, globalRotation);

                if (!sourceModel.IsCopy)
                {
                    foreach (VoxReader.Voxel voxel in sourceModel.Voxels)
                    {
                        var position = new Vector3Int(
                            voxel.LocalPosition.X,
                            voxel.LocalPosition.Y,
                            voxel.LocalPosition.Z);

                        // VoxReader exposes IMAP-adjusted color indices as 0-based values.
                        byte paletteIndex = (byte)Mathf.Clamp(voxel.ColorIndex + 1, 1, 255);
                        runtimeModel.AddOrUpdateVoxel(position, paletteIndex);
                    }
                }
            }

            return document;
        }

        public static RuntimeVoxDocument FromBytes(byte[] bytes, bool preserveSourceData = true)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            IVoxFile voxFile = VoxReader.VoxReader.Read(
                VoxWriter.CreateVoxReaderCompatibleCopy(bytes));
            RuntimeVoxDocument document = FromVoxFile(voxFile);
            if (preserveSourceData)
            {
                document.m_sourceVoxBytes = (byte[])bytes.Clone();
            }
            return document;
        }

        public static RuntimeVoxDocument FromBytes(ReadOnlyMemory<byte> bytes, bool preserveSourceData = true)
        {
            return FromBytes(bytes.ToArray(), preserveSourceData);
        }

        public static RuntimeVoxDocument FromStream(Stream stream, bool preserveSourceData = true)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return FromBytes(memoryStream.ToArray(), preserveSourceData);
            }
        }

        public byte[] ToVoxBytes()
        {
            return VoxWriter.Write(this);
        }

        internal byte[] GetPreservedSourceData()
        {
            return m_sourceVoxBytes;
        }

        // VoxReader reports GlobalPosition at the center index of the transformed bounds.
        // Recover the exact world position of local voxel zero, including the half-cell
        // convention used for negative axes by Matrix3.RotateIndex.
        internal static Vector3 GetTransformOffset(IModel sourceModel)
        {
            return GetTransformOffset(sourceModel, ToUnityMatrix(sourceModel.GlobalRotation));
        }

        private static Vector3 GetTransformOffset(IModel sourceModel, Matrix4x4 rotation)
        {
            var globalPosition = new Vector3(
                sourceModel.GlobalPosition.X,
                sourceModel.GlobalPosition.Y,
                sourceModel.GlobalPosition.Z);
            var globalSize = new Vector3Int(
                sourceModel.GlobalSize.X,
                sourceModel.GlobalSize.Y,
                sourceModel.GlobalSize.Z);
            var localSize = new Vector3Int(
                sourceModel.LocalSize.X,
                sourceModel.LocalSize.Y,
                sourceModel.LocalSize.Z);

            Vector3 origin = globalPosition - new Vector3(
                globalSize.x / 2,
                globalSize.y / 2,
                globalSize.z / 2);
            for (int worldAxis = 0; worldAxis < 3; worldAxis++)
            {
                for (int localAxis = 0; localAxis < 3; localAxis++)
                {
                    if (rotation[worldAxis, localAxis] < 0f)
                    {
                        origin[worldAxis] += localSize[localAxis] - 1;
                        break;
                    }
                }
            }
            return origin;
        }

        // Convert the affine local-origin placement back to the nTRN pivot expected by
        // MagicaVoxel. Imported documents retain their source nTRN chunks; this is used
        // when serializing newly generated documents.
        internal static Vector3 GetSceneTranslation(RuntimeModel model)
        {
            var centeredOrigin = new Vector3(
                -(model.Size.x / 2),
                -(model.Size.y / 2),
                -(model.Size.z / 2));
            Vector3 rotatedOrigin = RotateVoxelIndex(model.GlobalRotation, centeredOrigin);
            return model.TransformOffset - rotatedOrigin;
        }

        private static Vector3 RotateVoxelIndex(Matrix4x4 rotation, Vector3 position)
        {
            Vector3 rotated = rotation.MultiplyVector(position + Vector3.one * 0.5f);
            return new Vector3(
                Mathf.Floor(rotated.x),
                Mathf.Floor(rotated.y),
                Mathf.Floor(rotated.z));
        }

        internal static Matrix4x4 ToUnityMatrix(VoxReader.Matrix3 source)
        {
            var result = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    result[row, column] = source[row, column];
                }
            }
            return result;
        }

        private static IReadOnlyList<bool> GetModelVisibility(IVoxFile voxFile)
        {
            var transforms = new List<SceneTransform>();
            var transformsByChild = new Dictionary<int, SceneTransform>();
            var parentGroupByChild = new Dictionary<int, SceneGroup>();
            var shapes = new Dictionary<int, SceneShape>();
            var hiddenLayers = new HashSet<int>();

            foreach (IChunk chunk in voxFile.Chunks)
            {
                switch (chunk.Type)
                {
                    case VoxReader.ChunkType.TransformNode:
                    {
                        SceneTransform transform = ReadTransform(chunk.Content);
                        transforms.Add(transform);
                        transformsByChild[transform.ChildNodeId] = transform;
                        break;
                    }
                    case VoxReader.ChunkType.GroupNode:
                    {
                        SceneGroup group = ReadGroup(chunk.Content);
                        foreach (int childNodeId in group.ChildNodeIds)
                        {
                            parentGroupByChild[childNodeId] = group;
                        }
                        break;
                    }
                    case VoxReader.ChunkType.ShapeNode:
                    {
                        SceneShape shape = ReadShape(chunk.Content);
                        shapes[shape.NodeId] = shape;
                        break;
                    }
                    case VoxReader.ChunkType.Layer:
                    {
                        (int layerId, bool hidden) = ReadLayer(chunk.Content);
                        if (hidden)
                        {
                            hiddenLayers.Add(layerId);
                        }
                        break;
                    }
                }
            }

            if (transforms.Count == 0)
            {
                var legacyVisibility = new bool[voxFile.Models.Length];
                Array.Fill(legacyVisibility, true);
                return legacyVisibility;
            }

            var result = new List<bool>(voxFile.Models.Length);
            foreach (SceneTransform transform in transforms)
            {
                if (!shapes.TryGetValue(transform.ChildNodeId, out SceneShape shape))
                {
                    continue;
                }

                bool visible = !shape.Hidden && IsTransformVisible(
                    transform,
                    hiddenLayers,
                    parentGroupByChild,
                    transformsByChild);
                foreach (int unused in shape.ModelIds)
                {
                    result.Add(visible);
                }
            }

            if (result.Count != voxFile.Models.Length)
            {
                throw new InvalidDataException(
                    $"VOX scene visibility produced {result.Count} instances for " +
                    $"{voxFile.Models.Length} reader models.");
            }
            return result;
        }

        private static bool IsTransformVisible(
            SceneTransform transform,
            ISet<int> hiddenLayers,
            IReadOnlyDictionary<int, SceneGroup> parentGroupByChild,
            IReadOnlyDictionary<int, SceneTransform> transformsByChild)
        {
            var visited = new HashSet<int>();
            while (transform != null && visited.Add(transform.NodeId))
            {
                if (transform.Hidden || hiddenLayers.Contains(transform.LayerId))
                {
                    return false;
                }
                if (!parentGroupByChild.TryGetValue(transform.NodeId, out SceneGroup parentGroup))
                {
                    return true;
                }
                if (parentGroup.Hidden)
                {
                    return false;
                }
                if (!transformsByChild.TryGetValue(parentGroup.NodeId, out transform))
                {
                    return true;
                }
            }
            return transform == null;
        }

        private static SceneTransform ReadTransform(byte[] content)
        {
            using (var stream = new MemoryStream(content, writable: false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int nodeId = reader.ReadInt32();
                bool hidden = IsHidden(ReadDictionary(reader));
                int childNodeId = reader.ReadInt32();
                reader.ReadInt32(); // Reserved id.
                int layerId = reader.ReadInt32();
                return new SceneTransform(nodeId, childNodeId, layerId, hidden);
            }
        }

        private static SceneGroup ReadGroup(byte[] content)
        {
            using (var stream = new MemoryStream(content, writable: false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int nodeId = reader.ReadInt32();
                bool hidden = IsHidden(ReadDictionary(reader));
                int childCount = reader.ReadInt32();
                var childNodeIds = new int[childCount];
                for (int i = 0; i < childCount; i++)
                {
                    childNodeIds[i] = reader.ReadInt32();
                }
                return new SceneGroup(nodeId, childNodeIds, hidden);
            }
        }

        private static SceneShape ReadShape(byte[] content)
        {
            using (var stream = new MemoryStream(content, writable: false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int nodeId = reader.ReadInt32();
                bool hidden = IsHidden(ReadDictionary(reader));
                int modelCount = reader.ReadInt32();
                var modelIds = new int[modelCount];
                for (int i = 0; i < modelCount; i++)
                {
                    modelIds[i] = reader.ReadInt32();
                    ReadDictionary(reader);
                }
                return new SceneShape(nodeId, modelIds, hidden);
            }
        }

        private static (int layerId, bool hidden) ReadLayer(byte[] content)
        {
            using (var stream = new MemoryStream(content, writable: false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int layerId = reader.ReadInt32();
                bool hidden = IsHidden(ReadDictionary(reader));
                return (layerId, hidden);
            }
        }

        private static Dictionary<string, string> ReadDictionary(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException("VOX dictionary has a negative entry count.");
            }

            var result = new Dictionary<string, string>(count);
            for (int i = 0; i < count; i++)
            {
                result[ReadString(reader)] = ReadString(reader);
            }
            return result;
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new InvalidDataException("VOX string has an invalid length.");
            }
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        private static bool IsHidden(IReadOnlyDictionary<string, string> attributes)
        {
            return attributes.TryGetValue("_hidden", out string hidden) && hidden == "1";
        }

        private sealed class SceneTransform
        {
            public int NodeId { get; }
            public int ChildNodeId { get; }
            public int LayerId { get; }
            public bool Hidden { get; }

            public SceneTransform(int nodeId, int childNodeId, int layerId, bool hidden)
            {
                NodeId = nodeId;
                ChildNodeId = childNodeId;
                LayerId = layerId;
                Hidden = hidden;
            }
        }

        private sealed class SceneGroup
        {
            public int NodeId { get; }
            public IReadOnlyList<int> ChildNodeIds { get; }
            public bool Hidden { get; }

            public SceneGroup(int nodeId, IReadOnlyList<int> childNodeIds, bool hidden)
            {
                NodeId = nodeId;
                ChildNodeIds = childNodeIds;
                Hidden = hidden;
            }
        }

        private sealed class SceneShape
        {
            public int NodeId { get; }
            public IReadOnlyList<int> ModelIds { get; }
            public bool Hidden { get; }

            public SceneShape(int nodeId, IReadOnlyList<int> modelIds, bool hidden)
            {
                NodeId = nodeId;
                ModelIds = modelIds;
                Hidden = hidden;
            }
        }
    }
}
