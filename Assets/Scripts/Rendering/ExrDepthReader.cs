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
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TiltBrush
{
    // Reads the one EXR variant Open Brush's own depth pipeline writes and this
    // branch's post-production media pipeline bakes real-footage depth to (see
    // POSTPROD_PREVIEW_PLAN.md stage 4): a single-part scanline EXR, one channel
    // named "Y", pixel type FLOAT, compression NONE/ZIPS/ZIP. This is deliberately
    // NOT a general OpenEXR reader - tiled, multi-part, deep, or differently-shaped
    // files throw rather than being silently misread.
    //
    // The ZIP/ZIPS decompression algorithm (zlib inflate, then an un-predict pass,
    // then an un-interleave pass - in that order) was verified empirically against
    // Python's reference `OpenEXR` bindings before being ported here, since getting
    // the byte-level transform order wrong would silently corrupt every value rather
    // than fail loudly.
    public static class ExrDepthReader
    {
        private const uint MagicNumber = 0x01312f76;

        // Version-field flag bits (ImfVersion.h).
        private const uint TiledFlag = 1u << 9;
        private const uint DeepFlag = 1u << 11;
        private const uint MultiPartFlag = 1u << 12;

        private enum CompressionType : byte
        {
            None = 0,
            Rle = 1,
            Zips = 2,
            Zip = 3,
            // Piz, Pxr24, B44, B44A, Dwaa, Dwab (4-9): not supported here.
        }

        // Reads the sole "Y" channel of a single-part scanline EXR into a row-major
        // float array (row 0 = the top of the image, i.e. dataWindow.yMin).
        public static float[] ReadSingleChannelFloat(string path, out int width, out int height)
        {
            byte[] data = File.ReadAllBytes(path);
            return Decode(data, path, n => new float[n], bottomRowFirst: false, reuseScratch: false,
                out width, out height);
        }

        // Same decode for the fast plate cache, which decodes ~30 frames a second: the
        // pixel array comes from the caller (rentPixels(length), e.g. a pool), rows are
        // written bottom row first (Unity's texture order, so no flipped copy is needed),
        // and the file and per-block buffers are reused per thread. Allocating all of these
        // afresh cost ~16MB per frame and caused 100-170ms garbage-collection hitches
        // during playback (measured 2026-10-23).
        public static float[] ReadSingleChannelFloatPooled(string path, Func<int, float[]> rentPixels,
            out int width, out int height)
        {
            byte[] data = ReadFileReusingBuffer(path);
            return Decode(data, path, rentPixels, bottomRowFirst: true, reuseScratch: true,
                out width, out height);
        }

        [ThreadStatic] private static byte[] t_FileBuffer;
        [ThreadStatic] private static byte[] t_RawBuffer;
        [ThreadStatic] private static byte[] t_ReorderBuffer;

        private static byte[] ReadFileReusingBuffer(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1,
                FileOptions.SequentialScan);
            int length = (int)stream.Length;
            if (t_FileBuffer == null || t_FileBuffer.Length < length) { t_FileBuffer = new byte[length]; }
            int total = 0;
            while (total < length)
            {
                int read = stream.Read(t_FileBuffer, total, length - total);
                if (read <= 0) { throw new EndOfStreamException($"EXR shorter than expected: {path}"); }
                total += read;
            }
            return t_FileBuffer;
        }

        private static byte[] Scratch(ref byte[] buffer, int size)
        {
            if (buffer == null || buffer.Length < size) { buffer = new byte[size]; }
            return buffer;
        }

        // data may be longer than the file (a reused buffer); everything is read by offset.
        private static float[] Decode(byte[] data, string path, Func<int, float[]> allocatePixels,
            bool bottomRowFirst, bool reuseScratch, out int width, out int height)
        {
            int pos = 0;

            uint magic = ReadUInt32(data, ref pos);
            if (magic != MagicNumber)
            {
                throw new InvalidDataException($"Not an EXR file (bad magic number): {path}");
            }

            uint version = ReadUInt32(data, ref pos);
            if ((version & TiledFlag) != 0)
            {
                throw new NotSupportedException($"Tiled EXR not supported: {path}");
            }
            if ((version & DeepFlag) != 0)
            {
                throw new NotSupportedException($"Deep-data EXR not supported: {path}");
            }
            if ((version & MultiPartFlag) != 0)
            {
                throw new NotSupportedException($"Multi-part EXR not supported: {path}");
            }

            byte[] dataWindowBytes = null;
            byte compression = byte.MaxValue;
            bool foundYChannel = false;

            while (true)
            {
                string attrName = ReadCString(data, ref pos);
                if (attrName.Length == 0)
                {
                    break; // end of header
                }
                string attrType = ReadCString(data, ref pos);
                int size = (int)ReadUInt32(data, ref pos);
                int valueStart = pos;
                pos += size;

                switch (attrName)
                {
                    case "dataWindow":
                        dataWindowBytes = new byte[size];
                        Array.Copy(data, valueStart, dataWindowBytes, 0, size);
                        break;
                    case "compression":
                        compression = data[valueStart];
                        break;
                    case "channels":
                        foundYChannel = ValidateChannelListIsSingleFloatY(data, valueStart, valueStart + size);
                        break;
                }
            }

            if (dataWindowBytes == null)
            {
                throw new InvalidDataException($"EXR missing dataWindow attribute: {path}");
            }
            if (!foundYChannel)
            {
                throw new NotSupportedException(
                    $"EXR does not have exactly one float channel named \"Y\": {path}");
            }

            int xMin = ReadInt32(dataWindowBytes, 0);
            int yMin = ReadInt32(dataWindowBytes, 4);
            int xMax = ReadInt32(dataWindowBytes, 8);
            int yMax = ReadInt32(dataWindowBytes, 12);
            width = xMax - xMin + 1;
            height = yMax - yMin + 1;

            int linesPerBlock = compression switch
            {
                (byte)CompressionType.None => 1,
                (byte)CompressionType.Rle => 1,
                (byte)CompressionType.Zips => 1,
                (byte)CompressionType.Zip => 16,
                _ => throw new NotSupportedException(
                    $"Unsupported EXR compression type {compression} (only NONE/RLE/ZIPS/ZIP): {path}"),
            };

            int numChunks = (height + linesPerBlock - 1) / linesPerBlock;
            // Chunk (scanline-block) offset table - one int64 per chunk. We read chunks
            // sequentially below instead of following these offsets, so just skip past it.
            pos += numChunks * 8;

            float[] pixels = allocatePixels(width * height);

            for (int c = 0; c < numChunks; c++)
            {
                int chunkY = ReadInt32(data, ref pos);
                int chunkSize = ReadInt32(data, ref pos);
                int chunkStart = pos;
                pos += chunkSize;

                int row = chunkY - yMin;
                int linesThisChunk = Math.Min(linesPerBlock, height - row);
                int rawSize = linesThisChunk * width * 4;

                byte[] raw = reuseScratch ? Scratch(ref t_RawBuffer, rawSize) : new byte[rawSize];
                if (compression == (byte)CompressionType.None || chunkSize == rawSize)
                {
                    // Either genuinely uncompressed, or the EXR-spec fallback where the
                    // compressor stored the scanlines raw because compressing didn't help.
                    Array.Copy(data, chunkStart, raw, 0, rawSize);
                }
                else
                {
                    InflateZlib(data, chunkStart, chunkSize, raw, rawSize);
                    UnPredict(raw, rawSize);
                    byte[] reordered = reuseScratch ? Scratch(ref t_ReorderBuffer, rawSize) : new byte[rawSize];
                    UnReorder(raw, reordered, rawSize);
                    raw = reordered;
                }

                for (int line = 0; line < linesThisChunk; line++)
                {
                    int destRow = bottomRowFirst ? height - 1 - (row + line) : row + line;
                    for (int x = 0; x < width; x++)
                    {
                        // BinaryPrimitives has no ReadSingleLittleEndian in this Unity's BCL
                        // version - read the raw bits as int32 and reinterpret instead.
                        int bits = BinaryPrimitives.ReadInt32LittleEndian(
                            new ReadOnlySpan<byte>(raw, (line * width + x) * 4, 4));
                        pixels[destRow * width + x] = BitConverter.Int32BitsToSingle(bits);
                    }
                }
            }

            return pixels;
        }

        // Parses an EXR "chlist" attribute value and returns true if it describes
        // exactly one channel, named "Y", of pixel type FLOAT (2).
        private static bool ValidateChannelListIsSingleFloatY(byte[] data, int start, int end)
        {
            int pos = start;
            int channelCount = 0;
            bool isSingleFloatY = false;

            while (pos < end)
            {
                string name = ReadCString(data, ref pos);
                if (name.Length == 0)
                {
                    break; // end of channel list
                }
                int pixelType = ReadInt32(data, ref pos);
                pos += 1; // pLinear
                pos += 3; // reserved
                pos += 4; // xSampling
                pos += 4; // ySampling

                channelCount++;
                if (name == "Y" && pixelType == 2 /* FLOAT */)
                {
                    isSingleFloatY = true;
                }
            }

            return channelCount == 1 && isSingleFloatY;
        }

        // Reverses the EXR ZIP/ZIPS "predictor" pass in place: each byte (after the
        // first) was stored as a running difference from the previous byte, biased by
        // 128 and wrapped mod 256 on write. This undoes that with a cumulative sum.
        // Only the first n bytes are used (buffers may be reused and longer).
        private static void UnPredict(byte[] buf, int n)
        {
            for (int i = 1; i < n; i++)
            {
                buf[i] = (byte)(buf[i] + buf[i - 1] - 128);
            }
        }

        // Reverses the EXR ZIP/ZIPS "reorder" pass: on write, the byte stream was split
        // into two halves and interleaved (even output positions from the first half,
        // odd output positions from the second). This de-interleaves the first n bytes of
        // buf back into outBuf.
        private static void UnReorder(byte[] buf, byte[] outBuf, int n)
        {
            int half = (n + 1) / 2;
            for (int i = 0; i < half; i++)
            {
                outBuf[i * 2] = buf[i];
            }
            for (int i = 0; i < n - half; i++)
            {
                outBuf[i * 2 + 1] = buf[half + i];
            }
        }

        // EXR's ZIP/ZIPS compression is standard zlib (not raw deflate) - a 2-byte
        // zlib header followed by a deflate stream, no preset dictionary (OpenEXR's
        // compressor uses zlib's compress2() with default settings). .NET's
        // DeflateStream only understands the raw deflate portion, so the 2-byte
        // header is skipped manually; the trailing 4-byte Adler32 checksum is simply
        // never read, since DeflateStream stops exactly at the end of the deflate data.
        private static void InflateZlib(byte[] data, int offset, int compressedLength, byte[] output, int expectedSize)
        {
            using var input = new MemoryStream(data, offset + 2, compressedLength - 2);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            int totalRead = 0;
            while (totalRead < expectedSize)
            {
                int read = deflate.Read(output, totalRead, expectedSize - totalRead);
                if (read <= 0)
                {
                    throw new InvalidDataException(
                        $"EXR scanline block decompressed to {totalRead} bytes, expected {expectedSize}");
                }
                totalRead += read;
            }
        }

        private static string ReadCString(byte[] data, ref int pos)
        {
            int start = pos;
            while (data[pos] != 0)
            {
                pos++;
            }
            string s = Encoding.ASCII.GetString(data, start, pos - start);
            pos++; // skip the null terminator
            return s;
        }

        private static uint ReadUInt32(byte[] data, ref int pos)
        {
            uint v = BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(data, pos, 4));
            pos += 4;
            return v;
        }

        private static int ReadInt32(byte[] data, ref int pos)
        {
            int v = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(data, pos, 4));
            pos += 4;
            return v;
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(data, offset, 4));
        }
    }
}
