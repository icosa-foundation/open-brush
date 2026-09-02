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
using System.IO;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Utilities for loading and processing Google Cardboard Camera VR JPEG files
    /// </summary>
    public static class VrJpegUtils
    {
        public enum EyeLayout
        {
            LeftRight,  // Side-by-side stereo
            OverUnder   // Top-bottom stereo
        }

        /// <summary>
        /// Result of loading a VR JPEG, including optional audio data
        /// </summary>
        public class VrJpegLoadResult
        {
            public RawImage StereoImage { get; set; }
            public byte[] AudioData { get; set; }
            public string AudioMimeType { get; set; }
            public bool HasAudio => AudioData != null && AudioData.Length > 0;
        }

        /// <summary>
        /// Loads a VR JPEG file and creates a stereo equirectangular texture
        /// </summary>
        /// <param name="filename">Path to the .vr.jpg file</param>
        /// <param name="layout">How to arrange the left and right eye images</param>
        /// <param name="fillPoles">Whether to fill in the poles using interpolation</param>
        /// <param name="maxWidth">Maximum width of the output texture (0 = no limit)</param>
        /// <returns>A RawImage containing the stereo equirectangular panorama</returns>
        public static RawImage LoadVrJpeg(string filename, EyeLayout layout = EyeLayout.OverUnder,
                                         bool fillPoles = true, int maxWidth = 8192)
        {
            byte[] fileData = File.ReadAllBytes(filename);
            return LoadVrJpegFromBytes(fileData, filename, layout, fillPoles, maxWidth);
        }

        /// <summary>
        /// Loads a VR JPEG from byte array and creates a stereo equirectangular texture
        /// </summary>
        public static RawImage LoadVrJpegFromBytes(byte[] data, string filename,
                                                   EyeLayout layout = EyeLayout.OverUnder,
                                                   bool fillPoles = true, int maxWidth = 8192)
        {
            var result = LoadVrJpegWithAudio(data, filename, layout, fillPoles, maxWidth);
            return result.StereoImage;
        }

        /// <summary>
        /// Loads a VR JPEG file with full metadata including audio
        /// </summary>
        /// <param name="filename">Path to the .vr.jpg file</param>
        /// <param name="layout">How to arrange the left and right eye images</param>
        /// <param name="fillPoles">Whether to fill in the poles using interpolation</param>
        /// <param name="maxWidth">Maximum width of the output texture (0 = no limit)</param>
        /// <param name="extractAudio">Whether to extract embedded audio data</param>
        /// <returns>VrJpegLoadResult containing stereo image and optional audio</returns>
        public static VrJpegLoadResult LoadVrJpegWithAudioFromFile(string filename,
                                                                   EyeLayout layout = EyeLayout.OverUnder,
                                                                   bool fillPoles = true,
                                                                   int maxWidth = 8192,
                                                                   bool extractAudio = true)
        {
            byte[] fileData = File.ReadAllBytes(filename);
            return LoadVrJpegWithAudio(fileData, filename, layout, fillPoles, maxWidth, extractAudio);
        }

        /// <summary>
        /// Loads a VR JPEG from byte array with full metadata including audio
        /// </summary>
        public static VrJpegLoadResult LoadVrJpegWithAudio(byte[] data, string filename,
                                                           EyeLayout layout = EyeLayout.OverUnder,
                                                           bool fillPoles = true,
                                                           int maxWidth = 8192,
                                                           bool extractAudio = true)
        {
            // Read metadata
            VrJpegMetadata metadata = VrJpegMetadata.ReadFromBytes(data);

            // Load left eye (the main JPEG image)
            RawImage leftEye = ImageUtils.FromJpeg(data, filename);

            // Load right eye from metadata
            if (metadata.RightEyeImageData == null)
            {
                throw new ImageLoadError("VR JPEG does not contain right eye image data");
            }

            RawImage rightEye = ImageUtils.FromJpeg(metadata.RightEyeImageData, filename + "_right");

            // Convert both eyes to equirectangular
            RawImage leftEquirect = ConvertToEquirectangular(leftEye, metadata, fillPoles, maxWidth);
            RawImage rightEquirect = ConvertToEquirectangular(rightEye, metadata, fillPoles, maxWidth);

            // Combine into stereo image
            RawImage stereo = CombineEyes(leftEquirect, rightEquirect, layout);

            // Create result
            var result = new VrJpegLoadResult
            {
                StereoImage = stereo
            };

            // Extract audio if requested and available
            if (extractAudio && metadata.AudioData != null && metadata.AudioData.Length > 0)
            {
                result.AudioData = metadata.AudioData;
                result.AudioMimeType = metadata.AudioMime;

                Debug.Log($"VR JPEG audio extracted: {metadata.AudioData.Length} bytes, type: {metadata.AudioMime}");
            }

            return result;
        }

        /// <summary>
        /// Extracts and saves audio from a VR JPEG file
        /// </summary>
        /// <param name="vrJpegPath">Path to the VR JPEG file</param>
        /// <param name="outputPath">Path where audio should be saved (if null, uses same directory as image)</param>
        /// <returns>Path to saved audio file, or null if no audio was found</returns>
        public static string ExtractAndSaveAudio(string vrJpegPath, string outputPath = null)
        {
            try
            {
                byte[] data = File.ReadAllBytes(vrJpegPath);
                VrJpegMetadata metadata = VrJpegMetadata.ReadFromBytes(data);

                if (metadata.AudioData == null || metadata.AudioData.Length == 0)
                {
                    Debug.Log($"VR JPEG '{vrJpegPath}' does not contain audio data");
                    return null;
                }

                // Determine output path
                if (string.IsNullOrEmpty(outputPath))
                {
                    string directory = Path.GetDirectoryName(vrJpegPath);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(vrJpegPath);

                    // Remove .vr suffix if present
                    if (fileNameWithoutExt.EndsWith(".vr", StringComparison.OrdinalIgnoreCase))
                    {
                        fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileNameWithoutExt);
                    }

                    // Determine extension from MIME type
                    string extension = GetAudioExtensionFromMime(metadata.AudioMime);
                    outputPath = Path.Combine(directory, fileNameWithoutExt + "_audio" + extension);
                }

                // Save audio file
                File.WriteAllBytes(outputPath, metadata.AudioData);
                Debug.Log($"VR JPEG audio saved to: {outputPath} ({metadata.AudioData.Length} bytes)");

                return outputPath;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error extracting audio from VR JPEG: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets appropriate file extension from MIME type
        /// </summary>
        private static string GetAudioExtensionFromMime(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
            {
                return ".mp4"; // Default for Cardboard Camera
            }

            mimeType = mimeType.ToLower();

            if (mimeType.Contains("mp4") || mimeType.Contains("mpeg4"))
            {
                return ".mp4";
            }
            else if (mimeType.Contains("mpeg"))
            {
                return ".mp3";
            }
            else if (mimeType.Contains("ogg"))
            {
                return ".ogg";
            }
            else if (mimeType.Contains("wav"))
            {
                return ".wav";
            }
            else if (mimeType.Contains("aac"))
            {
                return ".aac";
            }

            return ".mp4"; // Default
        }

        /// <summary>
        /// Converts a single eye image to full equirectangular format
        /// </summary>
        private static RawImage ConvertToEquirectangular(RawImage sourceImage, VrJpegMetadata metadata,
                                                        bool fillPoles, int maxWidth)
        {
            // Calculate output dimensions
            int outputWidth = metadata.FullPanoWidthPixels;
            int outputHeight = metadata.FullPanoHeightPixels;

            // Apply max width constraint
            if (maxWidth > 0 && outputWidth > maxWidth)
            {
                float ratio = (float)maxWidth / outputWidth;
                outputWidth = maxWidth;
                outputHeight = Mathf.RoundToInt(outputHeight * ratio);
            }

            // Ensure aspect ratio is 2:1 for equirectangular
            outputHeight = outputWidth / 2;

            // Create output buffer
            Color32[] outputData = new Color32[outputWidth * outputHeight];

            // Calculate crop rectangle (scaled to output size)
            float scale = (float)outputWidth / metadata.FullPanoWidthPixels;
            int cropLeft = Mathf.FloorToInt(metadata.CroppedAreaLeftPixels * scale);
            int cropTop = Mathf.FloorToInt(metadata.CroppedAreaTopPixels * scale);
            int cropWidth = Mathf.CeilToInt(metadata.CroppedAreaImageWidthPixels * scale);
            int cropHeight = Mathf.CeilToInt(metadata.CroppedAreaImageHeightPixels * scale);

            // Initialize to black
            for (int i = 0; i < outputData.Length; i++)
            {
                outputData[i] = new Color32(0, 0, 0, 255);
            }

            // Copy source image into the crop area
            for (int y = 0; y < cropHeight && y < sourceImage.ColorHeight; y++)
            {
                for (int x = 0; x < cropWidth && x < sourceImage.ColorWidth; x++)
                {
                    int srcX = Mathf.RoundToInt(x / scale);
                    int srcY = Mathf.RoundToInt(y / scale);

                    if (srcX >= sourceImage.ColorWidth) srcX = sourceImage.ColorWidth - 1;
                    if (srcY >= sourceImage.ColorHeight) srcY = sourceImage.ColorHeight - 1;

                    int dstX = cropLeft + x;
                    int dstY = cropTop + y;

                    if (dstX >= 0 && dstX < outputWidth && dstY >= 0 && dstY < outputHeight)
                    {
                        int srcIdx = srcY * sourceImage.ColorWidth + srcX;
                        int dstIdx = dstY * outputWidth + dstX;

                        if (srcIdx >= 0 && srcIdx < sourceImage.ColorData.Length)
                        {
                            outputData[dstIdx] = sourceImage.ColorData[srcIdx];
                        }
                    }
                }
            }

            // Fill poles if requested
            if (fillPoles)
            {
                VrJpegPoleFiller.FillPoles(outputData, outputWidth, outputHeight,
                                          cropTop, cropTop + cropHeight);
            }

            return new RawImage
            {
                ColorData = outputData,
                ColorWidth = outputWidth,
                ColorHeight = outputHeight,
                ColorAspect = outputHeight == 0 ? 2f : ((float)outputWidth / outputHeight)
            };
        }

        /// <summary>
        /// Combines left and right eye images into a single stereo image
        /// </summary>
        private static RawImage CombineEyes(RawImage left, RawImage right, EyeLayout layout)
        {
            if (left.ColorWidth != right.ColorWidth || left.ColorHeight != right.ColorHeight)
            {
                throw new ImageLoadError("Left and right eye images must have the same dimensions");
            }

            int width = left.ColorWidth;
            int height = left.ColorHeight;

            Color32[] stereoData;
            int stereoWidth, stereoHeight;

            if (layout == EyeLayout.LeftRight)
            {
                // Side-by-side layout
                stereoWidth = width * 2;
                stereoHeight = height;
                stereoData = new Color32[stereoWidth * stereoHeight];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIdx = y * width + x;
                        int dstLeftIdx = y * stereoWidth + x;
                        int dstRightIdx = y * stereoWidth + (width + x);

                        stereoData[dstLeftIdx] = left.ColorData[srcIdx];
                        stereoData[dstRightIdx] = right.ColorData[srcIdx];
                    }
                }
            }
            else // OverUnder
            {
                // Top-bottom layout
                stereoWidth = width;
                stereoHeight = height * 2;
                stereoData = new Color32[stereoWidth * stereoHeight];

                // Top half = left eye
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIdx = y * width + x;
                        int dstIdx = y * stereoWidth + x;
                        stereoData[dstIdx] = left.ColorData[srcIdx];
                    }
                }

                // Bottom half = right eye
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIdx = y * width + x;
                        int dstIdx = (height + y) * stereoWidth + x;
                        stereoData[dstIdx] = right.ColorData[srcIdx];
                    }
                }
            }

            return new RawImage
            {
                ColorData = stereoData,
                ColorWidth = stereoWidth,
                ColorHeight = stereoHeight,
                ColorAspect = stereoHeight == 0 ? 1f : ((float)stereoWidth / stereoHeight)
            };
        }
    }
}
