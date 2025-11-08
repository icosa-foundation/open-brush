// Copyright 2025 The Open Brush Authors
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
//
// Portions adapted from VrJpeg library by Joan Charmant
// Original source: https://github.com/icosa-mirror/vrjpeg

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Metadata for Google Cardboard Camera VR JPEG files
    /// Reference: https://developers.google.com/vr/concepts/cardboard-camera-vr-photo-format
    /// </summary>
    public class VrJpegMetadata
    {
        // GPano metadata
        public int CroppedAreaLeftPixels { get; set; }
        public int CroppedAreaTopPixels { get; set; }
        public int CroppedAreaImageWidthPixels { get; set; }
        public int CroppedAreaImageHeightPixels { get; set; }
        public int FullPanoWidthPixels { get; set; }
        public int FullPanoHeightPixels { get; set; }
        public int InitialViewHeadingDegrees { get; set; }

        // GImage metadata
        public string ImageMime { get; set; }
        public byte[] RightEyeImageData { get; set; }

        // GAudio metadata
        public string AudioMime { get; set; }
        public byte[] AudioData { get; set; }

        private const byte JPEG_MARKER_START = 0xFF;
        private const byte JPEG_MARKER_APP1 = 0xE1;
        private const string XMP_HEADER = "http://ns.adobe.com/xap/1.0/\0";
        private const string XMP_EXTENDED_HEADER = "http://ns.adobe.com/xmp/extension/\0";

        /// <summary>
        /// Reads VR JPEG metadata from a file
        /// </summary>
        public static VrJpegMetadata ReadFromFile(string filename)
        {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return ReadFromStream(stream);
            }
        }

        /// <summary>
        /// Reads VR JPEG metadata from a byte array
        /// </summary>
        public static VrJpegMetadata ReadFromBytes(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return ReadFromStream(stream);
            }
        }

        /// <summary>
        /// Reads VR JPEG metadata from a stream
        /// </summary>
        public static VrJpegMetadata ReadFromStream(Stream stream)
        {
            var metadata = new VrJpegMetadata();
            var xmpSegments = new List<string>();
            var extendedXmpData = new Dictionary<string, List<ExtendedXmpChunk>>();

            // Read JPEG markers
            BinaryReader reader = new BinaryReader(stream);

            // Check JPEG header
            if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
            {
                throw new Exception("Not a valid JPEG file");
            }

            // Read all APP1 segments
            while (stream.Position < stream.Length - 2)
            {
                byte marker = reader.ReadByte();
                if (marker != JPEG_MARKER_START)
                {
                    continue;
                }

                byte markerType = reader.ReadByte();
                if (markerType == 0xD9) // EOI (End of Image)
                {
                    break;
                }

                // Read segment length (big-endian)
                ushort segmentLength = ReadUInt16BigEndian(reader);
                long segmentStart = stream.Position;
                long segmentEnd = segmentStart + segmentLength - 2;

                if (markerType == JPEG_MARKER_APP1 && segmentLength > XMP_HEADER.Length)
                {
                    // Read header to check if it's XMP
                    byte[] headerBytes = reader.ReadBytes(XMP_HEADER.Length);
                    string header = Encoding.UTF8.GetString(headerBytes);

                    if (header == XMP_HEADER)
                    {
                        // Standard XMP segment
                        long xmpDataLength = segmentEnd - stream.Position;
                        byte[] xmpData = reader.ReadBytes((int)xmpDataLength);
                        xmpSegments.Add(Encoding.UTF8.GetString(xmpData));
                    }
                    else if (header == XMP_EXTENDED_HEADER)
                    {
                        // Extended XMP segment
                        stream.Position = segmentStart;
                        ParseExtendedXmpSegment(reader, extendedXmpData, (int)(segmentEnd - segmentStart));
                    }
                }

                // Move to next segment
                stream.Position = segmentEnd;
            }

            // Parse standard XMP
            foreach (var xmp in xmpSegments)
            {
                metadata.ParseXmpString(xmp);
            }

            // Parse extended XMP
            foreach (var kvp in extendedXmpData)
            {
                var chunks = kvp.Value;
                chunks.Sort((a, b) => a.Offset.CompareTo(b.Offset));

                int totalSize = chunks.Count > 0 ? chunks[0].TotalSize : 0;
                byte[] completeData = new byte[totalSize];

                foreach (var chunk in chunks)
                {
                    Array.Copy(chunk.Data, 0, completeData, chunk.Offset, chunk.Data.Length);
                }

                string extendedXmp = Encoding.UTF8.GetString(completeData);
                metadata.ParseExtendedXmpString(extendedXmp);
            }

            return metadata;
        }

        private static void ParseExtendedXmpSegment(BinaryReader reader, Dictionary<string, List<ExtendedXmpChunk>> extendedXmpData, int segmentLength)
        {
            byte[] headerBytes = reader.ReadBytes(XMP_EXTENDED_HEADER.Length);
            byte[] guidBytes = reader.ReadBytes(32);
            string guid = Encoding.ASCII.GetString(guidBytes);

            uint totalSize = ReadUInt32BigEndian(reader);
            uint offset = ReadUInt32BigEndian(reader);

            int dataLength = segmentLength - XMP_EXTENDED_HEADER.Length - 32 - 8;
            byte[] data = reader.ReadBytes(dataLength);

            if (!extendedXmpData.ContainsKey(guid))
            {
                extendedXmpData[guid] = new List<ExtendedXmpChunk>();
            }

            extendedXmpData[guid].Add(new ExtendedXmpChunk
            {
                TotalSize = (int)totalSize,
                Offset = (int)offset,
                Data = data
            });
        }

        private void ParseXmpString(string xmp)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xmp);

                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
                nsmgr.AddNamespace("GPano", "http://ns.google.com/photos/1.0/panorama/");
                nsmgr.AddNamespace("GImage", "http://ns.google.com/photos/1.0/image/");
                nsmgr.AddNamespace("GAudio", "http://ns.google.com/photos/1.0/audio/");

                // Parse GPano properties
                ParseIntProperty(doc, nsmgr, "//GPano:CroppedAreaLeftPixels", ref CroppedAreaLeftPixels);
                ParseIntProperty(doc, nsmgr, "//GPano:CroppedAreaTopPixels", ref CroppedAreaTopPixels);
                ParseIntProperty(doc, nsmgr, "//GPano:CroppedAreaImageWidthPixels", ref CroppedAreaImageWidthPixels);
                ParseIntProperty(doc, nsmgr, "//GPano:CroppedAreaImageHeightPixels", ref CroppedAreaImageHeightPixels);
                ParseIntProperty(doc, nsmgr, "//GPano:FullPanoWidthPixels", ref FullPanoWidthPixels);
                ParseIntProperty(doc, nsmgr, "//GPano:FullPanoHeightPixels", ref FullPanoHeightPixels);
                ParseIntProperty(doc, nsmgr, "//GPano:InitialViewHeadingDegrees", ref InitialViewHeadingDegrees);

                // Parse GImage mime type
                var imageNode = doc.SelectSingleNode("//GImage:Mime", nsmgr);
                if (imageNode != null)
                {
                    ImageMime = imageNode.InnerText;
                }

                // Parse GAudio mime type
                var audioNode = doc.SelectSingleNode("//GAudio:Mime", nsmgr);
                if (audioNode != null)
                {
                    AudioMime = audioNode.InnerText;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error parsing VR JPEG XMP metadata: {e.Message}");
            }
        }

        private void ParseExtendedXmpString(string xmp)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xmp);

                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
                nsmgr.AddNamespace("GImage", "http://ns.google.com/photos/1.0/image/");
                nsmgr.AddNamespace("GAudio", "http://ns.google.com/photos/1.0/audio/");

                // Parse GImage:Data (right eye image)
                var imageDataNode = doc.SelectSingleNode("//GImage:Data", nsmgr);
                if (imageDataNode != null && !string.IsNullOrEmpty(ImageMime))
                {
                    string base64 = imageDataNode.InnerText;
                    RightEyeImageData = DecodeBase64WithoutPadding(base64);
                }

                // Parse GAudio:Data
                var audioDataNode = doc.SelectSingleNode("//GAudio:Data", nsmgr);
                if (audioDataNode != null && !string.IsNullOrEmpty(AudioMime))
                {
                    string base64 = audioDataNode.InnerText;
                    AudioData = DecodeBase64WithoutPadding(base64);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error parsing VR JPEG extended XMP metadata: {e.Message}");
            }
        }

        private void ParseIntProperty(XmlDocument doc, XmlNamespaceManager nsmgr, string xpath, ref int value)
        {
            var node = doc.SelectSingleNode(xpath, nsmgr);
            if (node != null)
            {
                int.TryParse(node.InnerText, out value);
            }
        }

        private byte[] DecodeBase64WithoutPadding(string base64)
        {
            // Google VR JPEGs have base64 data without padding
            base64 = base64.Trim();
            int padding = (4 - base64.Length % 4) % 4;
            base64 = base64.PadRight(base64.Length + padding, '=');
            return Convert.FromBase64String(base64);
        }

        private static ushort ReadUInt16BigEndian(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }

        private static uint ReadUInt32BigEndian(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }

        private class ExtendedXmpChunk
        {
            public int TotalSize { get; set; }
            public int Offset { get; set; }
            public byte[] Data { get; set; }
        }

        /// <summary>
        /// Checks if a file is a VR JPEG by looking for VR metadata
        /// </summary>
        public static bool IsVrJpeg(string filename)
        {
            try
            {
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return IsVrJpeg(stream);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if data is a VR JPEG by looking for VR metadata
        /// </summary>
        public static bool IsVrJpeg(byte[] data)
        {
            try
            {
                using (var stream = new MemoryStream(data))
                {
                    return IsVrJpeg(stream);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsVrJpeg(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);

            // Check JPEG header
            if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
            {
                return false;
            }

            // Look for XMP with GPano namespace
            while (stream.Position < Math.Min(stream.Length - 2, 65536)) // Only check first 64KB
            {
                byte marker = reader.ReadByte();
                if (marker != JPEG_MARKER_START)
                {
                    continue;
                }

                byte markerType = reader.ReadByte();
                if (markerType == 0xD9) // EOI
                {
                    break;
                }

                ushort segmentLength = ReadUInt16BigEndian(reader);
                long segmentStart = stream.Position;

                if (markerType == JPEG_MARKER_APP1 && segmentLength > XMP_HEADER.Length)
                {
                    byte[] headerBytes = reader.ReadBytes(Math.Min(XMP_HEADER.Length + 200, segmentLength - 2));
                    string header = Encoding.UTF8.GetString(headerBytes);

                    if (header.Contains(XMP_HEADER) && header.Contains("GPano:"))
                    {
                        return true;
                    }
                }

                stream.Position = segmentStart + segmentLength - 2;
            }

            return false;
        }
    }
}
