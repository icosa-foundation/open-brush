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
using System.IO;
using Superla.RadianceHDR;
using UnityEngine;

namespace TiltBrush
{
    public static class HdrTextureLoader
    {
        public sealed class DecodedImage
        {
            public int Width;
            public int Height;
            public Color[] Pixels;
        }

        public static bool IsSupportedFile(string path)
        {
            return ReferenceImageFormat.IsHighDynamicRangeFile(path);
        }

        public static bool IsExrData(byte[] bytes)
        {
            return bytes != null && bytes.Length >= 4 &&
                bytes[0] == 0x76 && bytes[1] == 0x2f &&
                bytes[2] == 0x31 && bytes[3] == 0x01;
        }

        public static bool IsHdrTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }

            return texture.format == TextureFormat.RGB9e5Float ||
                texture.format == TextureFormat.RGBAHalf ||
                texture.format == TextureFormat.RGBAFloat;
        }

        public static Texture2D Load(
            byte[] bytes, string path, bool makeNoLongerReadable = true,
            bool preserveAlpha = false)
        {
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".hdr", StringComparison.OrdinalIgnoreCase))
            {
                Texture2D texture = new RadianceHDRTexture(bytes).texture;
                if (texture != null && makeNoLongerReadable)
                {
                    texture.Apply(false, true);
                }
                return texture;
            }
            if (string.Equals(extension, ".exr", StringComparison.OrdinalIgnoreCase))
            {
                if (preserveAlpha)
                {
                    Texture2D texture = TinyExr.LoadTexture2D(
                        bytes, linear: true, mipChain: false);
                    if (makeNoLongerReadable)
                    {
                        texture.Apply(false, true);
                    }
                    return texture;
                }
                return TinyExr.LoadRgb9e5Texture2D(
                    bytes, makeNoLongerReadable: makeNoLongerReadable);
            }
            throw new ArgumentException($"Unsupported HDR image extension: {extension}", nameof(path));
        }

        // Performs CPU-only decoding and is safe to call from an image-loading worker thread.
        public static DecodedImage Decode(byte[] bytes, string path, int maxDimension = -1)
        {
            GetDimensions(bytes, path, out int width, out int height);
            if (maxDimension > 0 &&
                (long)width * height > (long)maxDimension * maxDimension)
            {
                throw new ImageLoadError(
                    $"Image dimensions {width}x{height} are greater than max dimensions of {maxDimension}x{maxDimension}!",
                    ImageLoadError.ImageLoadErrorCode.ImageTooLargeError);
            }

            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".exr", StringComparison.OrdinalIgnoreCase))
            {
                TinyExr.ImageResult result = TinyExr.Load(bytes);
                return new DecodedImage
                {
                    Width = result.width,
                    Height = result.height,
                    Pixels = result.colors
                };
            }
            if (string.Equals(extension, ".hdr", StringComparison.OrdinalIgnoreCase))
            {
                return DecodeRadiance(bytes);
            }
            throw new ArgumentException($"Unsupported HDR image extension: {extension}", nameof(path));
        }

        public static void GetDimensions(byte[] bytes, string path, out int width, out int height)
        {
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".hdr", StringComparison.OrdinalIgnoreCase))
            {
                using (var stream = new MemoryStream(bytes))
                {
                    var header = new RGBEHeader();
                    if (header.ReadHeader(stream) != RGBEReturnCode.RGBE_RETURN_SUCCESS)
                    {
                        throw new InvalidDataException("Invalid Radiance HDR header");
                    }
                    width = header.width;
                    height = header.height;
                    return;
                }
            }
            if (string.Equals(extension, ".exr", StringComparison.OrdinalIgnoreCase))
            {
                GetExrDimensions(bytes, out width, out height);
                return;
            }
            throw new ArgumentException($"Unsupported HDR image extension: {extension}", nameof(path));
        }

        public static Texture2D CreateTexture(DecodedImage image)
        {
            var texture = new Texture2D(
                image.Width, image.Height, TextureFormat.RGBAFloat, false, true);
            texture.SetPixels(image.Pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static DecodedImage DecodeRadiance(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                var header = new RGBEHeader();
                if (header.ReadHeader(stream) != RGBEReturnCode.RGBE_RETURN_SUCCESS)
                {
                    throw new InvalidDataException("Invalid Radiance HDR header");
                }

                int pixelCount = checked(header.width * header.height);
                var pixels = new Color[pixelCount];
                using (var reader = new BinaryReader(stream, System.Text.Encoding.Default, true))
                {
                    if (header.width >= 8 && header.width <= 0x7fff &&
                        IsRleScanline(reader, header.width))
                    {
                        ReadRlePixels(reader, header.width, header.height, pixels);
                    }
                    else
                    {
                        ReadFlatPixels(reader, header.width, header.height, pixels);
                    }
                }
                return new DecodedImage
                {
                    Width = header.width,
                    Height = header.height,
                    Pixels = pixels
                };
            }
        }

        private static void GetExrDimensions(byte[] bytes, out int width, out int height)
        {
            if (!IsExrData(bytes) || bytes.Length < 9)
            {
                throw new InvalidDataException("Invalid OpenEXR header");
            }

            int position = 8;
            while (position < bytes.Length)
            {
                string name = ReadNullTerminatedString(bytes, ref position);
                if (name.Length == 0)
                {
                    break;
                }
                string type = ReadNullTerminatedString(bytes, ref position);
                int size = ReadInt32(bytes, ref position);
                if (size < 0 || position > bytes.Length - size)
                {
                    throw new InvalidDataException("Invalid OpenEXR attribute size");
                }
                if (name == "dataWindow" && type == "box2i" && size >= 16)
                {
                    int valuePosition = position;
                    int minX = ReadInt32(bytes, ref valuePosition);
                    int minY = ReadInt32(bytes, ref valuePosition);
                    int maxX = ReadInt32(bytes, ref valuePosition);
                    int maxY = ReadInt32(bytes, ref valuePosition);
                    width = checked(maxX - minX + 1);
                    height = checked(maxY - minY + 1);
                    if (width <= 0 || height <= 0)
                    {
                        throw new InvalidDataException("Invalid OpenEXR data window");
                    }
                    return;
                }
                position += size;
            }
            throw new InvalidDataException("OpenEXR dataWindow attribute is missing");
        }

        private static string ReadNullTerminatedString(byte[] bytes, ref int position)
        {
            int start = position;
            while (position < bytes.Length && bytes[position] != 0)
            {
                position++;
            }
            if (position >= bytes.Length)
            {
                throw new InvalidDataException("Unterminated OpenEXR header string");
            }
            string value = System.Text.Encoding.ASCII.GetString(bytes, start, position - start);
            position++;
            return value;
        }

        private static int ReadInt32(byte[] bytes, ref int position)
        {
            if (position > bytes.Length - 4)
            {
                throw new EndOfStreamException();
            }
            int value = bytes[position] |
                (bytes[position + 1] << 8) |
                (bytes[position + 2] << 16) |
                (bytes[position + 3] << 24);
            position += 4;
            return value;
        }

        private static bool IsRleScanline(BinaryReader reader, int width)
        {
            long position = reader.BaseStream.Position;
            byte[] marker = reader.ReadBytes(4);
            reader.BaseStream.Position = position;
            return marker.Length == 4 && marker[0] == 2 && marker[1] == 2 &&
                (marker[2] & 0x80) == 0 && ((marker[2] << 8) | marker[3]) == width;
        }

        private static void ReadFlatPixels(
            BinaryReader reader, int width, int height, Color[] pixels)
        {
            for (int sourceY = 0; sourceY < height; sourceY++)
            {
                int destinationRow = (height - 1 - sourceY) * width;
                for (int x = 0; x < width; x++)
                {
                    pixels[destinationRow + x] = ReadRgbeColor(reader);
                }
            }
        }

        private static void ReadRlePixels(
            BinaryReader reader, int width, int height, Color[] pixels)
        {
            var scanline = new byte[checked(width * 4)];
            for (int sourceY = 0; sourceY < height; sourceY++)
            {
                byte[] marker = reader.ReadBytes(4);
                if (marker.Length != 4 || marker[0] != 2 || marker[1] != 2 ||
                    ((marker[2] << 8) | marker[3]) != width)
                {
                    throw new InvalidDataException("Invalid Radiance HDR scanline");
                }

                for (int channel = 0; channel < 4; channel++)
                {
                    int channelOffset = channel * width;
                    int x = 0;
                    while (x < width)
                    {
                        int count = reader.ReadByte();
                        if (count > 128)
                        {
                            count -= 128;
                            int value = reader.ReadByte();
                            if (count == 0 || x + count > width)
                            {
                                throw new InvalidDataException("Invalid Radiance HDR encoded run");
                            }
                            for (int i = 0; i < count; i++)
                            {
                                scanline[channelOffset + x++] = (byte)value;
                            }
                        }
                        else
                        {
                            if (count == 0 || x + count > width)
                            {
                                throw new InvalidDataException("Invalid Radiance HDR literal run");
                            }
                            byte[] values = reader.ReadBytes(count);
                            if (values.Length != count)
                            {
                                throw new EndOfStreamException();
                            }
                            Buffer.BlockCopy(values, 0, scanline, channelOffset + x, count);
                            x += count;
                        }
                    }
                }

                int destinationRow = (height - 1 - sourceY) * width;
                for (int x = 0; x < width; x++)
                {
                    pixels[destinationRow + x] = RgbeToColor(
                        scanline[x], scanline[width + x],
                        scanline[2 * width + x], scanline[3 * width + x]);
                }
            }
        }

        private static Color ReadRgbeColor(BinaryReader reader)
        {
            byte[] rgbe = reader.ReadBytes(4);
            if (rgbe.Length != 4)
            {
                throw new EndOfStreamException();
            }
            return RgbeToColor(rgbe[0], rgbe[1], rgbe[2], rgbe[3]);
        }

        private static Color RgbeToColor(byte r, byte g, byte b, byte exponent)
        {
            if (exponent == 0)
            {
                return new Color(0, 0, 0, 1);
            }
            float scale = (float)(Math.Pow(2.0, exponent - 128.0) / 255.0);
            return new Color(r * scale, g * scale, b * scale, 1);
        }
    }
}
