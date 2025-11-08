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

            return stereo;
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
