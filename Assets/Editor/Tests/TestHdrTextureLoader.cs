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

using NUnit.Framework;
using System.Text;
using UnityEngine;

namespace TiltBrush
{
    internal class TestHdrTextureLoader
    {
        [TestCase("reference.hdr")]
        [TestCase("reference.exr")]
        [TestCase("REFERENCE.EXR")]
        public void SupportsHdrImageExtensions(string path)
        {
            Assert.IsTrue(HdrTextureLoader.IsSupportedFile(path));
        }

        [TestCase("reference.png")]
        [TestCase("reference.exr.png")]
        [TestCase("")]
        [TestCase(null)]
        public void RejectsOtherImageExtensions(string path)
        {
            Assert.IsFalse(HdrTextureLoader.IsSupportedFile(path));
        }

        [TestCase(".jpg")]
        [TestCase(".jpeg")]
        [TestCase(".png")]
        [TestCase(".svg")]
        [TestCase(".hdr")]
        [TestCase(".EXR")]
        public void CatalogRecognizesSupportedReferenceFormats(string extension)
        {
            Assert.IsTrue(ReferenceImageFormat.IsSupportedExtension(extension));
        }

        [Test]
        public void RecognizesOpenExrMagicBytes()
        {
            Assert.IsTrue(HdrTextureLoader.IsExrData(new byte[] { 0x76, 0x2f, 0x31, 0x01 }));
            Assert.IsFalse(HdrTextureLoader.IsExrData(new byte[] { 0x76, 0x2f, 0x31, 0x00 }));
            Assert.IsFalse(HdrTextureLoader.IsExrData(null));
        }

        [TestCase(TextureFormat.RGB9e5Float, true)]
        [TestCase(TextureFormat.RGBAHalf, true)]
        [TestCase(TextureFormat.RGBAFloat, true)]
        [TestCase(TextureFormat.RGB24, false)]
        [TestCase(TextureFormat.RGBA32, false)]
        public void DetectsHdrTextureFormats(TextureFormat format, bool expected)
        {
            var texture = new Texture2D(1, 1, format, false);
            try
            {
                Assert.AreEqual(expected, HdrTextureLoader.IsHdrTexture(texture));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void LoadsUnityEncodedExrAsPackedHdrTexture()
        {
            Texture2D source = new Texture2D(2, 1, TextureFormat.RGBAFloat, false, true);
            Texture2D decoded = null;
            try
            {
                source.SetPixels(new[]
                {
                    new Color(0.25f, 1.0f, 4.0f, 1.0f),
                    new Color(2.0f, 0.5f, 0.125f, 1.0f)
                });
                source.Apply();
                byte[] bytes = source.EncodeToEXR(
                    Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP);

                decoded = HdrTextureLoader.Load(bytes, "generated.exr");

                Assert.AreEqual(2, decoded.width);
                Assert.AreEqual(1, decoded.height);
                Assert.AreEqual(TextureFormat.RGB9e5Float, decoded.format);
                Assert.IsFalse(decoded.isReadable);
            }
            finally
            {
                Object.DestroyImmediate(source);
                if (decoded != null)
                {
                    Object.DestroyImmediate(decoded);
                }
            }
        }

        [Test]
        public void CanKeepPackedExrTextureReadableForCaching()
        {
            Texture2D source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            Texture2D decoded = null;
            try
            {
                source.SetPixel(0, 0, new Color(0.25f, 1.0f, 4.0f, 1.0f));
                source.Apply();
                byte[] bytes = source.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

                decoded = HdrTextureLoader.Load(
                    bytes, "generated.exr", makeNoLongerReadable: false);

                Assert.IsTrue(decoded.isReadable);
                Assert.AreEqual(sizeof(uint), decoded.GetRawTextureData().Length);
            }
            finally
            {
                Object.DestroyImmediate(source);
                if (decoded != null)
                {
                    Object.DestroyImmediate(decoded);
                }
            }
        }

        [Test]
        public void CanPreserveExrAlpha()
        {
            Texture2D source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            Texture2D decoded = null;
            try
            {
                source.SetPixel(0, 0, new Color(0.25f, 1.0f, 4.0f, 0.375f));
                source.Apply();
                byte[] bytes = source.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

                decoded = HdrTextureLoader.Load(
                    bytes, "generated.exr", makeNoLongerReadable: false,
                    preserveAlpha: true);

                Assert.AreEqual(TextureFormat.RGBAFloat, decoded.format);
                Assert.AreEqual(0.375f, decoded.GetPixel(0, 0).a, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(source);
                if (decoded != null)
                {
                    Object.DestroyImmediate(decoded);
                }
            }
        }

        [Test]
        public void DecodesExrWithoutCreatingTexture()
        {
            Texture2D source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            try
            {
                source.SetPixel(0, 0, new Color(0.25f, 1.0f, 4.0f, 0.375f));
                source.Apply();

                var decoded = HdrTextureLoader.Decode(
                    source.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat), "generated.exr");

                Assert.AreEqual(1, decoded.Width);
                Assert.AreEqual(1, decoded.Height);
                Assert.AreEqual(4.0f, decoded.Pixels[0].b, 0.0001f);
                Assert.AreEqual(0.375f, decoded.Pixels[0].a, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void DecodesRadianceWithoutCreatingTexture()
        {
            byte[] header = Encoding.ASCII.GetBytes(
                "#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 1 +X 1\n");
            var bytes = new byte[header.Length + 4];
            System.Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
            bytes[header.Length + 0] = 64;
            bytes[header.Length + 1] = 128;
            bytes[header.Length + 2] = 255;
            bytes[header.Length + 3] = 128;

            var decoded = HdrTextureLoader.Decode(bytes, "generated.hdr");

            Assert.AreEqual(1, decoded.Width);
            Assert.AreEqual(1, decoded.Height);
            Assert.AreEqual(64.0f / 255.0f, decoded.Pixels[0].r, 0.0001f);
            Assert.AreEqual(128.0f / 255.0f, decoded.Pixels[0].g, 0.0001f);
            Assert.AreEqual(1.0f, decoded.Pixels[0].b, 0.0001f);
        }

        [Test]
        public void ReadsExrDimensionsBeforeDecode()
        {
            Texture2D source = new Texture2D(3, 2, TextureFormat.RGBAFloat, false, true);
            try
            {
                source.Apply();
                byte[] bytes = source.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

                HdrTextureLoader.GetDimensions(bytes, "generated.exr", out int width, out int height);

                Assert.AreEqual(3, width);
                Assert.AreEqual(2, height);
                var error = Assert.Throws<ImageLoadError>(
                    () => HdrTextureLoader.Decode(bytes, "generated.exr", maxDimension: 2));
                Assert.AreEqual(
                    ImageLoadError.ImageLoadErrorCode.ImageTooLargeError,
                    error.imageLoadErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void ResizesDecodedExrBeforeCreatingTexture()
        {
            Texture2D source = new Texture2D(4, 2, TextureFormat.RGBAFloat, false, true);
            Texture2D decodedTexture = null;
            try
            {
                source.SetPixels(new[]
                {
                    Color.red, Color.red, Color.green, Color.green,
                    Color.blue, Color.blue, Color.white, Color.white
                });
                source.Apply();
                byte[] bytes = source.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

                var decoded = HdrTextureLoader.Decode(
                    bytes, "generated.exr", maxDimension: 4, decodeDimension: 2);
                decodedTexture = HdrTextureLoader.CreateTexture(decoded);

                Assert.AreEqual(2, decoded.Width);
                Assert.AreEqual(1, decoded.Height);
                Assert.AreEqual(TextureFormat.RGBAHalf, decodedTexture.format);
            }
            finally
            {
                Object.DestroyImmediate(source);
                if (decodedTexture != null)
                {
                    Object.DestroyImmediate(decodedTexture);
                }
            }
        }

        [Test]
        public void ReadsRadianceDimensionsBeforeDecode()
        {
            byte[] bytes = Encoding.ASCII.GetBytes(
                "#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 7 +X 11\n");

            HdrTextureLoader.GetDimensions(bytes, "generated.hdr", out int width, out int height);

            Assert.AreEqual(11, width);
            Assert.AreEqual(7, height);
        }
    }
}
