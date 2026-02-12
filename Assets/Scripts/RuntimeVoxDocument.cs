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
        public sealed class RuntimeModel
        {
            private readonly Dictionary<Vector3Int, byte> m_voxels = new Dictionary<Vector3Int, byte>();

            public string Name { get; }
            public Vector3Int Size { get; }
            public IReadOnlyDictionary<Vector3Int, byte> Voxels => m_voxels;

            public RuntimeModel(string name, Vector3Int size)
            {
                if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(size), "Model size dimensions must be > 0");
                }

                Name = name;
                Size = size;
            }

            public bool AddOrUpdateVoxel(Vector3Int position, byte paletteIndex)
            {
                if (!IsInBounds(position) || paletteIndex == 0)
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

            private bool IsInBounds(Vector3Int position)
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

        public IReadOnlyList<RuntimeModel> Models => m_models;

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
            var model = new RuntimeModel(name, size);
            m_models.Add(model);
            return model;
        }

        public bool ReplacePaletteEntry(int oneBasedIndex, Color32 color)
        {
            int zeroBased = oneBasedIndex - 1;
            if (zeroBased < 0 || zeroBased >= Palette.Length)
            {
                return false;
            }

            Palette[zeroBased] = color;
            return true;
        }

        public static RuntimeVoxDocument FromVoxFile(IVoxFile voxFile)
        {
            if (voxFile == null)
            {
                throw new ArgumentNullException(nameof(voxFile));
            }

            var document = new RuntimeVoxDocument();

            int paletteCount = Math.Min(document.Palette.Length, voxFile.Palette.RawColors.Length);
            for (int i = 0; i < paletteCount; i++)
            {
                VoxReader.Color source = voxFile.Palette.RawColors[i];
                document.Palette[i] = new Color32(source.R, source.G, source.B, source.A);
            }

            foreach (IModel sourceModel in voxFile.Models)
            {
                var modelSize = new Vector3Int(
                    sourceModel.LocalSize.X,
                    sourceModel.LocalSize.Y,
                    sourceModel.LocalSize.Z);

                RuntimeModel runtimeModel = document.CreateModel(sourceModel.Name, modelSize);

                foreach (VoxReader.Voxel voxel in sourceModel.Voxels)
                {
                    var position = new Vector3Int(
                        voxel.LocalPosition.X,
                        voxel.LocalPosition.Y,
                        voxel.LocalPosition.Z);

                    // Voxel color indices from VoxReader are 0-based.
                    byte paletteIndex = (byte)Mathf.Clamp(voxel.ColorIndex + 1, 1, 255);
                    runtimeModel.AddOrUpdateVoxel(position, paletteIndex);
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
            return FromVoxFile(voxFile);
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
    }
}
