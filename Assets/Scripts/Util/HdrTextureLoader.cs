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
        public static bool IsSupportedFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".hdr", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".exr", StringComparison.OrdinalIgnoreCase);
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
    }
}
