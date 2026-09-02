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
        private const uint k_ContributorEnvelopeMagic = 0x3143504d;
        private const int k_ContributorEnvelopeVersion = 1;
        private const uint k_StrokeClockTrailerMagic = 0x314b4c43;

        public static async Task<byte[]> SerializeAndCompressMemoryListAsync(List<Stroke> memoryList)
        {
            byte[] serializedData = await SerializeMemoryList(memoryList);
            return await Compress(serializedData);
        }

        public static async Task<byte[]> SerializeAndCompressContributorMemoryListAsync(
            List<Stroke> memoryList)
        {
            List<Stroke> serializableStrokes = memoryList
                .Where(stroke => stroke.IsGeometryEnabled)
                .ToList();
            byte[] strokeData = await SerializeMemoryList(serializableStrokes);
            byte[] envelope;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(k_ContributorEnvelopeMagic);
                writer.Write(k_ContributorEnvelopeVersion);
                writer.Write(serializableStrokes.Count);
                foreach (var stroke in serializableStrokes)
                {
                    writer.Write(stroke.m_MultiplayerContributorId.ToByteArray());
                    writer.Write(stroke.m_MultiplayerContributorNickname ?? string.Empty);
                }
                writer.Write(strokeData.Length);
                writer.Write(strokeData);
                // Keep the version-1 envelope layout readable by existing clients. They stop
                // after strokeData; compatible clients can consume this optional trailer.
                writer.Write(k_StrokeClockTrailerMagic);
                writer.Write(serializableStrokes.Count);
                foreach (var stroke in serializableStrokes)
                {
                    bool hasTimeSession = SketchMemoryScript.m_Instance.TryGetStrokeTimeSession(
                        stroke, out StrokeTimeSessionMetadata timeSession);
                    writer.Write(hasTimeSession);
                    if (hasTimeSession)
                    {
                        writer.Write(timeSession.StartUtcMs);
                        writer.Write(timeSession.StartSketchTimeMs);
                        writer.Write(timeSession.EndSketchTimeMs);
                    }
                }
                writer.Flush();
                envelope = stream.ToArray();
            }
            return await Compress(envelope);
        }

        public static async Task<List<Stroke>> DecompressAndDeserializeMemoryListAsync(byte[] compressedData)
        {
            byte[] decompressedData = await Decompress(compressedData);
            return await DeserializeMemoryList(decompressedData);
        }

        public static async Task<List<Stroke>>
            DecompressAndDeserializeContributorMemoryListAsync(byte[] compressedData)
        {
            byte[] data = await Decompress(compressedData);
            if (!TryReadContributorEnvelope(
                data, out var contributorIds, out var contributorNicknames,
                out var strokeTimeSessions, out var strokeData))
            {
                return await DeserializeMemoryList(data);
            }

            List<Stroke> strokes = await DeserializeMemoryList(
                strokeData, squashLayers: false);
            if (strokes == null || strokes.Count != contributorIds.Count)
            {
                throw new InvalidDataException(
                    "Multiplayer contributor metadata does not match the stroke count.");
            }

            for (int i = 0; i < strokes.Count; ++i)
            {
                strokes[i].m_MultiplayerContributorId = contributorIds[i];
                strokes[i].m_MultiplayerContributorNickname = contributorNicknames[i];
            }

            SketchMemoryScript.m_Instance.RestoreStrokeTimeSessions(
                strokeTimeSessions
                    .Where(session => session != null)
                    .GroupBy(session => new
                    {
                        session.StartUtcMs,
                        session.StartSketchTimeMs,
                        session.EndSketchTimeMs,
                    })
                    .Select(group => group.First()));
            return strokes;
        }

        private static bool TryReadContributorEnvelope(
            byte[] data, out List<Guid> contributorIds,
            out List<string> contributorNicknames,
            out List<StrokeTimeSessionMetadata> strokeTimeSessions,
            out byte[] strokeData)
        {
            contributorIds = null;
            contributorNicknames = null;
            strokeTimeSessions = null;
            strokeData = null;
            if (data == null || data.Length < sizeof(uint) + sizeof(int))
            {
                return false;
            }

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt32() != k_ContributorEnvelopeMagic)
            {
                return false;
            }
            if (reader.ReadInt32() != k_ContributorEnvelopeVersion)
            {
                throw new InvalidDataException("Unsupported multiplayer contributor envelope.");
            }

            int count = reader.ReadInt32();
            if (count < 0 || count > 10000)
            {
                throw new InvalidDataException("Invalid multiplayer contributor count.");
            }

            contributorIds = new List<Guid>(count);
            contributorNicknames = new List<string>(count);
            for (int i = 0; i < count; ++i)
            {
                byte[] guidBytes = reader.ReadBytes(16);
                if (guidBytes.Length != 16)
                {
                    throw new EndOfStreamException();
                }
                contributorIds.Add(new Guid(guidBytes));
                contributorNicknames.Add(reader.ReadString());
            }

            int strokeDataLength = reader.ReadInt32();
            if (strokeDataLength < 0 || strokeDataLength > stream.Length - stream.Position)
            {
                throw new InvalidDataException("Invalid multiplayer stroke payload length.");
            }
            strokeData = reader.ReadBytes(strokeDataLength);

            strokeTimeSessions = Enumerable.Repeat<StrokeTimeSessionMetadata>(
                null, count).ToList();
            if (stream.Length - stream.Position < sizeof(uint) + sizeof(int))
            {
                return true;
            }

            if (reader.ReadUInt32() != k_StrokeClockTrailerMagic)
            {
                return true;
            }

            int clockCount = reader.ReadInt32();
            if (clockCount != count)
            {
                throw new InvalidDataException(
                    "Multiplayer stroke clock metadata does not match the stroke count.");
            }
            for (int i = 0; i < clockCount; ++i)
            {
                if (!reader.ReadBoolean())
                {
                    continue;
                }
                strokeTimeSessions[i] = new StrokeTimeSessionMetadata
                {
                    StartUtcMs = reader.ReadInt64(),
                    StartSketchTimeMs = reader.ReadUInt32(),
                    EndSketchTimeMs = reader.ReadUInt32(),
                };
            }
            return true;
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
        public static async Task<List<Stroke>> DeserializeMemoryList(
            byte[] data, bool squashLayers = false)
        {
            try
            {
                using (var memoryStream = new MemoryStream(data))
                {
                    var oldGroupToNewGroup = new Dictionary<int, int>();
                    var strokes = SketchWriter.GetStrokes(
                        memoryStream, allowFastPath: true, squashLayers: squashLayers);

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
