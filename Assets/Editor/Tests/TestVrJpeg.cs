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
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestVrJpeg
    {
        private const string kXmpHeader = "http://ns.adobe.com/xap/1.0/\0";
        private const string kExtendedXmpHeader = "http://ns.adobe.com/xmp/extension/\0";
        private const string kExtendedXmpGuid = "0123456789ABCDEF0123456789ABCDEF";

        [Test]
        public void DecodesVrJpegAtRequestedSize()
        {
            Texture2D source = CreateSourceTexture();
            try
            {
                byte[] jpeg = source.EncodeToJPG();
                byte[] vrJpeg = CreateVrJpeg(jpeg, jpeg);

                RawImage decoded = ImageUtils.FromImageData(
                    vrJpeg, "generated.vr.jpg", abortDimension: 16,
                    decodeDimension: 4);

                Assert.IsTrue(VrJpegMetadata.IsVrJpeg(vrJpeg));
                Assert.AreEqual(4, decoded.ColorWidth);
                Assert.AreEqual(4, decoded.ColorHeight);
                Assert.AreEqual(16, decoded.ColorData.Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void TreatsMonoscopicGpanoAsOrdinaryJpeg()
        {
            Texture2D source = CreateSourceTexture();
            try
            {
                byte[] jpeg = source.EncodeToJPG();
                byte[] gpanoJpeg = CreateVrJpeg(jpeg, null);

                RawImage decoded = ImageUtils.FromImageData(gpanoJpeg, "generated.jpg");

                Assert.IsFalse(VrJpegMetadata.IsVrJpeg(gpanoJpeg));
                Assert.AreEqual(4, decoded.ColorWidth);
                Assert.AreEqual(2, decoded.ColorHeight);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static Texture2D CreateSourceTexture()
        {
            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            var pixels = new Color32[8];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32((byte)(i * 20), 64, 128, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static byte[] CreateVrJpeg(byte[] leftEyeJpeg, byte[] rightEyeJpeg)
        {
            string standardXmp = @"
                <x:xmpmeta xmlns:x=""adobe:ns:meta/""><rdf:RDF
                xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""><rdf:Description
                xmlns:GPano=""http://ns.google.com/photos/1.0/panorama/""
                xmlns:GImage=""http://ns.google.com/photos/1.0/image/""
                GPano:CroppedAreaLeftPixels=""2"" GPano:CroppedAreaTopPixels=""1""
                GPano:CroppedAreaImageWidthPixels=""4""
                GPano:CroppedAreaImageHeightPixels=""2""
                GPano:FullPanoWidthPixels=""8"" GPano:FullPanoHeightPixels=""4""
                GImage:Mime=""image/jpeg""/></rdf:RDF></x:xmpmeta>";
            byte[] standardSegment = CreateApp1Segment(
                Encoding.UTF8.GetBytes($"{kXmpHeader}{standardXmp}"));
            byte[] extendedSegment = null;
            if (rightEyeJpeg != null)
            {
                string extendedXmp = $@"
                    <x:xmpmeta xmlns:x=""adobe:ns:meta/""><rdf:RDF
                    xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""><rdf:Description
                    xmlns:GImage=""http://ns.google.com/photos/1.0/image/""><GImage:Data>
                    {Convert.ToBase64String(rightEyeJpeg)}
                    </GImage:Data></rdf:Description></rdf:RDF></x:xmpmeta>";
                byte[] extendedXml = Encoding.UTF8.GetBytes(extendedXmp);
                using (var extendedPayload = new MemoryStream())
                {
                    WriteBytes(extendedPayload, Encoding.ASCII.GetBytes(kExtendedXmpHeader));
                    WriteBytes(extendedPayload, Encoding.ASCII.GetBytes(kExtendedXmpGuid));
                    WriteBigEndian(extendedPayload, (uint)extendedXml.Length);
                    WriteBigEndian(extendedPayload, 0);
                    WriteBytes(extendedPayload, extendedXml);
                    extendedSegment = CreateApp1Segment(extendedPayload.ToArray());
                }
            }

            using (var output = new MemoryStream())
            {
                output.WriteByte(0xff);
                output.WriteByte(0xd8);
                WriteBytes(output, standardSegment);
                if (extendedSegment != null)
                {
                    WriteBytes(output, extendedSegment);
                }
                output.Write(leftEyeJpeg, 2, leftEyeJpeg.Length - 2);
                return output.ToArray();
            }
        }

        private static byte[] CreateApp1Segment(byte[] payload)
        {
            int segmentLength = checked(payload.Length + 2);
            if (segmentLength > ushort.MaxValue)
            {
                throw new InvalidDataException("Test XMP segment is too large");
            }
            using (var segment = new MemoryStream())
            {
                segment.WriteByte(0xff);
                segment.WriteByte(0xe1);
                segment.WriteByte((byte)(segmentLength >> 8));
                segment.WriteByte((byte)segmentLength);
                WriteBytes(segment, payload);
                return segment.ToArray();
            }
        }

        private static void WriteBigEndian(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static void WriteBytes(Stream stream, byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
