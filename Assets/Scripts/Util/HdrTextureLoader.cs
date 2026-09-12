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

        private sealed class RadianceHeader
        {
            public char ScanlineAxis;
            public char ScanlineSign;
            public int ScanlineCount;
            public char PixelAxis;
            public char PixelSign;
            public int PixelCount;
            public int Width;
            public int Height;
            public int DataOffset;
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
        public static DecodedImage Decode(
            byte[] bytes, string path, int maxDimension = -1, int decodeDimension = -1)
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
            DecodedImage image;
            if (string.Equals(extension, ".exr", StringComparison.OrdinalIgnoreCase))
            {
                TinyExr.ImageResult result = TinyExr.Load(bytes);
                image = new DecodedImage
                {
                    Width = result.width,
                    Height = result.height,
                    Pixels = result.colors
                };
            }
            else if (string.Equals(extension, ".hdr", StringComparison.OrdinalIgnoreCase))
            {
                image = DecodeRadiance(bytes);
            }
            else
            {
                throw new ArgumentException(
                    $"Unsupported HDR image extension: {extension}", nameof(path));
            }
            return ResizeDecodedImage(image, decodeDimension);
        }

        public static void GetDimensions(byte[] bytes, string path, out int width, out int height)
        {
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".hdr", StringComparison.OrdinalIgnoreCase))
            {
                RadianceHeader header = ParseRadianceHeader(bytes);
                width = header.Width;
                height = header.Height;
                return;
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
                image.Width, image.Height, TextureFormat.RGBAHalf, false, true);
            texture.SetPixels(image.Pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static DecodedImage ResizeDecodedImage(DecodedImage source, int maxDimension)
        {
            if (maxDimension <= 0 ||
                (source.Width <= maxDimension && source.Height <= maxDimension))
            {
                return source;
            }

            double scale = Math.Min(
                1.0, maxDimension / (double)Math.Max(source.Width, source.Height));
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            var pixels = new Color[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                double sourceY = ((y + 0.5) * source.Height / height) - 0.5;
                int y0 = Math.Max(0, (int)Math.Floor(sourceY));
                int y1 = Math.Min(source.Height - 1, y0 + 1);
                float yFraction = (float)Math.Max(0, sourceY - y0);
                for (int x = 0; x < width; x++)
                {
                    double sourceX = ((x + 0.5) * source.Width / width) - 0.5;
                    int x0 = Math.Max(0, (int)Math.Floor(sourceX));
                    int x1 = Math.Min(source.Width - 1, x0 + 1);
                    float xFraction = (float)Math.Max(0, sourceX - x0);
                    Color top = Color.Lerp(
                        source.Pixels[y0 * source.Width + x0],
                        source.Pixels[y0 * source.Width + x1], xFraction);
                    Color bottom = Color.Lerp(
                        source.Pixels[y1 * source.Width + x0],
                        source.Pixels[y1 * source.Width + x1], xFraction);
                    pixels[y * width + x] = Color.Lerp(top, bottom, yFraction);
                }
            }
            return new DecodedImage { Width = width, Height = height, Pixels = pixels };
        }

        private static DecodedImage DecodeRadiance(byte[] bytes)
        {
            RadianceHeader header = ParseRadianceHeader(bytes);
            using (var stream = new MemoryStream(bytes))
            {
                stream.Position = header.DataOffset;
                int pixelCount = checked(header.Width * header.Height);
                var pixels = new Color[pixelCount];
                using (var reader = new BinaryReader(stream, System.Text.Encoding.Default, true))
                {
                    if (header.PixelCount >= 8 && header.PixelCount <= 0x7fff &&
                        IsRleScanline(reader, header.PixelCount))
                    {
                        ReadRlePixels(reader, header, pixels);
                    }
                    else
                    {
                        ReadFlatPixels(reader, header, pixels);
                    }
                }
                return new DecodedImage
                {
                    Width = header.Width,
                    Height = header.Height,
                    Pixels = pixels
                };
            }
        }

        private static RadianceHeader ParseRadianceHeader(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidDataException("Radiance HDR data is empty");
            }

            int position = 0;
            string firstLine = ReadAsciiLine(bytes, ref position);
            if (!firstLine.StartsWith("#?", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Invalid Radiance HDR signature");
            }

            bool hasFormat = false;
            while (position < bytes.Length)
            {
                string line = ReadAsciiLine(bytes, ref position).Trim();
                if (line.StartsWith("FORMAT=", StringComparison.Ordinal))
                {
                    hasFormat = true;
                    continue;
                }

                string[] tokens = line.Split(
                    new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 4 || !TryParseRadianceAxis(tokens[0], out char firstSign,
                        out char firstAxis) ||
                    !int.TryParse(tokens[1], out int firstCount) ||
                    !TryParseRadianceAxis(tokens[2], out char secondSign,
                        out char secondAxis) ||
                    !int.TryParse(tokens[3], out int secondCount))
                {
                    continue;
                }
                if (!hasFormat || firstAxis == secondAxis || firstCount <= 0 || secondCount <= 0)
                {
                    throw new InvalidDataException("Invalid Radiance HDR resolution");
                }

                return new RadianceHeader
                {
                    ScanlineAxis = firstAxis,
                    ScanlineSign = firstSign,
                    ScanlineCount = firstCount,
                    PixelAxis = secondAxis,
                    PixelSign = secondSign,
                    PixelCount = secondCount,
                    Width = firstAxis == 'X' ? firstCount : secondCount,
                    Height = firstAxis == 'Y' ? firstCount : secondCount,
                    DataOffset = position
                };
            }
            throw new InvalidDataException("Radiance HDR resolution is missing");
        }

        private static string ReadAsciiLine(byte[] bytes, ref int position)
        {
            int start = position;
            while (position < bytes.Length && bytes[position] != '\n')
            {
                position++;
            }
            int end = position;
            if (position < bytes.Length)
            {
                position++;
            }
            if (end > start && bytes[end - 1] == '\r')
            {
                end--;
            }
            return System.Text.Encoding.ASCII.GetString(bytes, start, end - start);
        }

        private static bool TryParseRadianceAxis(string token, out char sign, out char axis)
        {
            sign = '\0';
            axis = '\0';
            if (token.Length != 2 || (token[0] != '+' && token[0] != '-'))
            {
                return false;
            }
            char parsedAxis = char.ToUpperInvariant(token[1]);
            if (parsedAxis != 'X' && parsedAxis != 'Y')
            {
                return false;
            }
            sign = token[0];
            axis = parsedAxis;
            return true;
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
            BinaryReader reader, RadianceHeader header, Color[] pixels)
        {
            for (int scanline = 0; scanline < header.ScanlineCount; scanline++)
            {
                for (int pixel = 0; pixel < header.PixelCount; pixel++)
                {
                    StoreRadiancePixel(
                        header, scanline, pixel, ReadRgbeColor(reader), pixels);
                }
            }
        }

        private static void ReadRlePixels(
            BinaryReader reader, RadianceHeader header, Color[] pixels)
        {
            var scanlineBytes = new byte[checked(header.PixelCount * 4)];
            for (int scanline = 0; scanline < header.ScanlineCount; scanline++)
            {
                byte[] marker = reader.ReadBytes(4);
                if (marker.Length != 4 || marker[0] != 2 || marker[1] != 2 ||
                    ((marker[2] << 8) | marker[3]) != header.PixelCount)
                {
                    throw new InvalidDataException("Invalid Radiance HDR scanline");
                }

                for (int channel = 0; channel < 4; channel++)
                {
                    int channelOffset = channel * header.PixelCount;
                    int x = 0;
                    while (x < header.PixelCount)
                    {
                        int count = reader.ReadByte();
                        if (count > 128)
                        {
                            count -= 128;
                            int value = reader.ReadByte();
                            if (count == 0 || x + count > header.PixelCount)
                            {
                                throw new InvalidDataException("Invalid Radiance HDR encoded run");
                            }
                            for (int i = 0; i < count; i++)
                            {
                                scanlineBytes[channelOffset + x++] = (byte)value;
                            }
                        }
                        else
                        {
                            if (count == 0 || x + count > header.PixelCount)
                            {
                                throw new InvalidDataException("Invalid Radiance HDR literal run");
                            }
                            byte[] values = reader.ReadBytes(count);
                            if (values.Length != count)
                            {
                                throw new EndOfStreamException();
                            }
                            Buffer.BlockCopy(
                                values, 0, scanlineBytes, channelOffset + x, count);
                            x += count;
                        }
                    }
                }

                for (int pixel = 0; pixel < header.PixelCount; pixel++)
                {
                    StoreRadiancePixel(
                        header, scanline, pixel,
                        RgbeToColor(
                            scanlineBytes[pixel],
                            scanlineBytes[header.PixelCount + pixel],
                            scanlineBytes[2 * header.PixelCount + pixel],
                            scanlineBytes[3 * header.PixelCount + pixel]),
                        pixels);
                }
            }
        }

        private static void StoreRadiancePixel(
            RadianceHeader header, int scanline, int pixel, Color color, Color[] pixels)
        {
            int firstCoordinate = RadianceCoordinate(
                header.ScanlineSign, scanline, header.ScanlineCount);
            int secondCoordinate = RadianceCoordinate(
                header.PixelSign, pixel, header.PixelCount);
            int x = header.ScanlineAxis == 'X' ? firstCoordinate : secondCoordinate;
            int y = header.ScanlineAxis == 'Y' ? firstCoordinate : secondCoordinate;
            pixels[y * header.Width + x] = color;
        }

        private static int RadianceCoordinate(char sign, int index, int count)
        {
            return sign == '+' ? index : count - 1 - index;
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
