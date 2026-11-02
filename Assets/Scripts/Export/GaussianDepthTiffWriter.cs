using System;
using System.IO;
using UnityEngine;

/// <summary>Writes linear Z as uncompressed, single-channel IEEE float32 TIFF.</summary>
internal static class GaussianDepthTiffWriter
{
    public static void Write(string path, Texture2D depthTexture, float nearDepth, float farDepth)
    {
        int channels;
        switch (depthTexture.format)
        {
            case TextureFormat.RFloat: channels = 1; break;
            case TextureFormat.RGBAFloat: channels = 4; break;
            default:
                throw new ArgumentException($"Unsupported training depth format: {depthTexture.format}");
        }

        int width = depthTexture.width;
        int height = depthTexture.height;
        uint byteCount = checked((uint)((long)width * height * sizeof(float)));
        const ushort entryCount = 12;
        const uint dataOffset = 8 + 2 + entryCount * 12 + 4;
        var pixels = depthTexture.GetRawTextureData<float>();
        // Only row-sized scratch buffers: no full-image RGBA conversion or copy.
        var row = new float[width];
        var bytes = new byte[checked(width * sizeof(float))];

        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write((byte)'I');
            writer.Write((byte)'I');
            writer.Write((ushort)42);
            writer.Write((uint)8);
            writer.Write(entryCount);
            WriteEntry(writer, 256, 4, (uint)width); // ImageWidth, LONG
            WriteEntry(writer, 257, 4, (uint)height); // ImageLength
            WriteEntry(writer, 258, 3, 32); // BitsPerSample, SHORT
            WriteEntry(writer, 259, 3, 1); // Compression: none
            WriteEntry(writer, 262, 3, 1); // PhotometricInterpretation: black is zero
            WriteEntry(writer, 273, 4, dataOffset); // StripOffsets
            WriteEntry(writer, 274, 3, 1); // Orientation: top-left
            WriteEntry(writer, 277, 3, 1); // SamplesPerPixel
            WriteEntry(writer, 278, 4, (uint)height); // RowsPerStrip
            WriteEntry(writer, 279, 4, byteCount); // StripByteCounts
            WriteEntry(writer, 284, 3, 1); // PlanarConfiguration: contiguous
            WriteEntry(writer, 339, 3, 3); // SampleFormat: IEEE floating point
            writer.Write((uint)0); // No next IFD

            // ReadPixels/raw texture rows start at the bottom; TIFF and exported PNGs
            // start at the top. Extract red, flip rows and reject background in one pass.
            for (int y = height - 1; y >= 0; --y)
            {
                int offset = y * width * channels;
                for (int x = 0; x < width; ++x)
                {
                    float depth = pixels[offset + x * channels];
                    row[x] = depth > 0f && depth >= nearDepth && depth < farDepth
                        && !float.IsNaN(depth) && !float.IsInfinity(depth) ? depth : 0f;
                }
                Buffer.BlockCopy(row, 0, bytes, 0, bytes.Length);
                if (!BitConverter.IsLittleEndian)
                {
                    for (int i = 0; i < bytes.Length; i += sizeof(float))
                    {
                        Array.Reverse(bytes, i, sizeof(float));
                    }
                }
                writer.Write(bytes);
            }
        }
    }

    private static void WriteEntry(BinaryWriter writer, ushort tag, ushort type, uint value)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write((uint)1); // One value, stored inline (SHORT values are zero-padded).
        writer.Write(value);
    }
}
