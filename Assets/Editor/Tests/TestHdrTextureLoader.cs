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
using UnityEngine;

namespace TiltBrush
{
    internal class TestHdrTextureLoader
    {
        [TestCase("background.hdr")]
        [TestCase("background.exr")]
        [TestCase("BACKGROUND.EXR")]
        public void SupportsHdrImageExtensions(string path)
        {
            Assert.IsTrue(HdrTextureLoader.IsSupportedFile(path));
        }

        [TestCase("background.png")]
        [TestCase("background.exr.png")]
        [TestCase("")]
        [TestCase(null)]
        public void RejectsOtherImageExtensions(string path)
        {
            Assert.IsFalse(HdrTextureLoader.IsSupportedFile(path));
        }

        [Test]
        public void RecognizesOpenExrMagicBytes()
        {
            Assert.IsTrue(HdrTextureLoader.IsExrData(new byte[] { 0x76, 0x2f, 0x31, 0x01 }));
            Assert.IsFalse(HdrTextureLoader.IsExrData(new byte[] { 0x76, 0x2f, 0x31, 0x00 }));
            Assert.IsFalse(HdrTextureLoader.IsExrData(null));
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
    }
}
