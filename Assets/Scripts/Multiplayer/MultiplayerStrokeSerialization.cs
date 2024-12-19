// Copyright 2023 The Open Brush Authors
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
using System.IO.Compression;
using UnityEngine;
using TiltBrush;
using System.Threading.Tasks;
using System.Linq;

namespace OpenBrush.Multiplayer
{
    public static class MultiplayerStrokeSerialization
    {
        public static async Task<byte[]> SerializeAndCompressMemoryListAsync(List<Stroke> memoryList)
        {
            byte[] serializedData = await SerializeMemoryList(memoryList);
            return await Compress(serializedData);
        }

        public static async Task<List<Stroke>> DecompressAndDeserializeMemoryListAsync(byte[] compressedData)
        {
            byte[] decompressedData = await Decompress(compressedData);
            return await DeserializeMemoryList(decompressedData);
        }

        // Serializes a LinkedList of Strokes into a byte array using SketchWriter.
        // We did not event anything new we are using SketchWriter.WriteMemory from TiltBrush.
        public static async Task<byte[]> SerializeMemoryList(List<Stroke> strokeList)
        {
            try
            {
                var strokeSnapshots = SketchWriter.EnumerateAdjustedSnapshots(strokeList).ToList();
                using (var memoryStream = new MemoryStream())
                {
                    SketchWriter.WriteMemory(memoryStream, strokeSnapshots, new GroupIdMapping());
                    Debug.Log($"Serialization complete. Serialized data size: {memoryStream.Length} bytes.");
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during serialization: {ex.Message}");
                throw;
            }
        }

        // Deserializes a byte array into a List of Strokes using SketchWriter.
        // We did not event anything new we are using SketchWriter.GetStrokes from TiltBrush.
        public static async Task<List<Stroke>> DeserializeMemoryList(byte[] data)
        {
            try
            {
                using (var memoryStream = new MemoryStream(data))
                {
                    var oldGroupToNewGroup = new Dictionary<int, int>();
                    var strokes = SketchWriter.GetStrokes(memoryStream, allowFastPath: true);

                    if (strokes != null)
                    {
                        Debug.Log($"Successfully deserialized {strokes.Count} strokes from network.");
                        return strokes;
                    }
                    else
                    {
                        Debug.LogError("Failed to deserialize strokes.");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during deserialization: {ex.Message}");
                throw;
            }
        }

        public static Guid[] GetBrushGuidsFromManifest()
        {
            // List to store brush GUIDs
            List<Guid> brushGuids = new List<Guid>();

            // Iterate through each unique brush in the manifest
            foreach (BrushDescriptor brush in App.Instance.ManifestFull.UniqueBrushes())
            {
                if (brush != null)
                {
                    // Add the brush GUID to the list
                    brushGuids.Add(brush.m_Guid);
                    Debug.Log($"Brush: {brush.name}, GUID: {brush.m_Guid}");
                }
                else
                {
                    Debug.LogWarning("Encountered a null brush descriptor.");
                }
            }

            return brushGuids.ToArray();
        }

        // Compresses a byte array using Brotli.
        public static async Task<byte[]> Compress(byte[] data)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var outputStream = new MemoryStream();
                    using var brotliStream = new BrotliStream(outputStream, CompressionMode.Compress, leaveOpen: true);

                    brotliStream.Write(data, 0, data.Length);
                    brotliStream.Flush();

                    Debug.Log($"Compression complete. Compressed data size: {outputStream.Length} bytes.");

                    return outputStream.ToArray();
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during compression: {ex.Message}");
                throw;
            }
        }

        // Decompresses a Brotli-compressed byte array.
        public static async Task<byte[]> Decompress(byte[] compressedData)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var input = new MemoryStream(compressedData);
                    using var brotli = new BrotliStream(input, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    brotli.CopyTo(output);
                    Debug.Log($"Decompression complete. Decompressed data size: {output.Length} bytes.");
                    return output.ToArray();
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during decompression: {ex.Message}");
                throw;
            }
        }

    }
}
