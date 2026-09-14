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
using UnityEngine;
using VoxReader.Interfaces;

namespace TiltBrush
{
    public sealed class RuntimeVoxDocument
    {
        // XYZI stores each voxel coordinate in one byte, so valid coordinates are
        // 0..255 and each model dimension can contain at most 256 cells.
        public const int MaxModelDimension = byte.MaxValue + 1;

        public sealed class RuntimeModel
        {
            private readonly Dictionary<Vector3Int, byte> m_voxels;

            public string Name { get; }
            public Vector3Int Size { get; }
            public Vector3 TransformOffset { get; set; }
            public Vector3 LocalTransformOffset { get; }
            public Matrix4x4 GlobalRotation { get; }
            public Matrix4x4 LocalRotation { get; }
            public int SourceModelId { get; }
            public bool IsCopy { get; }
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
                Matrix4x4? localRotation = null)
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

            foreach (IModel sourceModel in voxFile.Models)
            {
                var modelSize = new Vector3Int(
                    sourceModel.LocalSize.X,
                    sourceModel.LocalSize.Y,
                    sourceModel.LocalSize.Z);

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
                    ToUnityMatrix(sourceModel.GlobalRotation),
                    ToUnityMatrix(sourceModel.LocalRotation));
                document.m_models.Add(runtimeModel);
                runtimeModel.TransformOffset = new Vector3(
                    sourceModel.GlobalPosition.X,
                    sourceModel.GlobalPosition.Y,
                    sourceModel.GlobalPosition.Z);

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

        public static RuntimeVoxDocument FromBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            IVoxFile voxFile = VoxReader.VoxReader.Read(bytes);
            RuntimeVoxDocument document = FromVoxFile(voxFile);
            document.m_sourceVoxBytes = (byte[])bytes.Clone();
            return document;
        }

        public static RuntimeVoxDocument FromBytes(ReadOnlyMemory<byte> bytes)
        {
            return FromBytes(bytes.ToArray());
        }

        public static RuntimeVoxDocument FromStream(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return FromBytes(memoryStream.ToArray());
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

        private static Matrix4x4 ToUnityMatrix(VoxReader.Matrix3 source)
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
    }
}
