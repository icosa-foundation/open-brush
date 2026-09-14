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
// Pole filling algorithm based on idea by Leo Sutic:
// https://monochrome.sutic.nu/2012/11/04/filling-in-the-blanks.html

using System;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Fills the nadir and zenith caps of equirectangular images by averaging pixels
    /// from the nearest valid row. Uses a non-linear neighborhood mapping based on latitude.
    /// </summary>
    public static class VrJpegPoleFiller
    {
        /// <summary>
        /// Fills the poles of an equirectangular panorama
        /// </summary>
        /// <param name="pixels">Image pixel data</param>
        /// <param name="width">Image width</param>
        /// <param name="height">Image height</param>
        /// <param name="cropTop">Y coordinate of the top of the valid data</param>
        /// <param name="cropBottom">Y coordinate of the bottom of the valid data</param>
        public static void FillPoles(Color32[] pixels, int width, int height, int cropTop, int cropBottom)
        {
            if (pixels == null || pixels.Length != width * height)
            {
                return;
            }

            // Clamp crop bounds
            cropTop = Mathf.Clamp(cropTop, 0, height - 1);
            cropBottom = Mathf.Clamp(cropBottom, 0, height - 1);

            // Process rows above the crop (north pole)
            for (int y = 0; y < cropTop; y++)
            {
                FillRow(pixels, width, height, y, cropTop, cropBottom, true);
            }

            // Process rows below the crop (south pole)
            for (int y = cropBottom; y < height; y++)
            {
                FillRow(pixels, width, height, y, cropTop, cropBottom, false);
            }
        }

        /// <summary>
        /// Fills a single row by averaging pixels from the reference row
        /// </summary>
        private static void FillRow(Color32[] pixels, int width, int height, int row,
                                   int cropTop, int cropBottom, bool isNorthPole)
        {
            int refRow = isNorthPole ? cropTop : cropBottom - 1;
            int neighborhoodSize = GetNeighborhoodSize(row, cropTop, cropBottom, width, height);

            if (neighborhoodSize <= 0)
            {
                return;
            }

            // Ensure neighborhood is odd
            if (neighborhoodSize % 2 == 0)
            {
                neighborhoodSize--;
            }

            int span = (neighborhoodSize - 1) / 2;

            // Accumulators for averaging
            int r = 0, g = 0, b = 0;
            int oldStart = 0;

            for (int x = 0; x < width; x++)
            {
                int start = x - span;
                int end = x + span;

                if (x == 0)
                {
                    // Initialize accumulators
                    r = g = b = 0;
                    for (int k = start; k <= end; k++)
                    {
                        // Wrap horizontally (equirectangular wraps around)
                        int sampleX = WrapHorizontal(k, width);
                        int refIdx = refRow * width + sampleX;

                        if (refIdx >= 0 && refIdx < pixels.Length)
                        {
                            Color32 pixel = pixels[refIdx];
                            r += pixel.r;
                            g += pixel.g;
                            b += pixel.b;
                        }
                    }
                }
                else
                {
                    // Update accumulators (sliding window)
                    // Remove the old start pixel
                    int oldSampleX = WrapHorizontal(oldStart, width);
                    int oldIdx = refRow * width + oldSampleX;
                    if (oldIdx >= 0 && oldIdx < pixels.Length)
                    {
                        Color32 pixel = pixels[oldIdx];
                        r -= pixel.r;
                        g -= pixel.g;
                        b -= pixel.b;
                    }

                    // Add the new end pixel
                    int newSampleX = WrapHorizontal(end, width);
                    int newIdx = refRow * width + newSampleX;
                    if (newIdx >= 0 && newIdx < pixels.Length)
                    {
                        Color32 pixel = pixels[newIdx];
                        r += pixel.r;
                        g += pixel.g;
                        b += pixel.b;
                    }
                }

                oldStart = start;

                // Write averaged pixel
                int dstIdx = row * width + x;
                if (dstIdx >= 0 && dstIdx < pixels.Length)
                {
                    pixels[dstIdx] = new Color32(
                        (byte)Mathf.Clamp(r / neighborhoodSize, 0, 255),
                        (byte)Mathf.Clamp(g / neighborhoodSize, 0, 255),
                        (byte)Mathf.Clamp(b / neighborhoodSize, 0, 255),
                        255
                    );
                }
            }
        }

        /// <summary>
        /// Wraps horizontal coordinate for equirectangular wrapping
        /// </summary>
        private static int WrapHorizontal(int x, int width)
        {
            while (x < 0) x += width;
            while (x >= width) x -= width;
            return x;
        }

        /// <summary>
        /// Calculates the neighborhood size based on distance from the nearest valid row
        /// Uses a non-linear mapping for better visual results
        /// </summary>
        private static int GetNeighborhoodSize(int row, int cropTop, int cropBottom, int width, int height)
        {
            if (row >= cropTop && row < cropBottom)
            {
                return 0; // Row is within valid data
            }

            double ratio;
            if (row < cropTop)
            {
                // North pole
                if (cropTop == 0)
                {
                    return 0;
                }
                ratio = (double)(cropTop - row) / cropTop;
            }
            else
            {
                // South pole
                int remainingRows = height - cropBottom;
                if (remainingRows == 0)
                {
                    return 0;
                }
                ratio = (double)(row - cropBottom + 1) / remainingRows;
            }

            // Non-linear mapping for better falloff
            double mapped = MapRatio(ratio);
            int neighborhoodSize = (int)Math.Max(1, width * mapped);

            return neighborhoodSize;
        }

        /// <summary>
        /// Remaps the normalized distance for non-linear falloff effect
        /// </summary>
        private static double MapRatio(double v)
        {
            // Use cosine curve for smooth falloff
            return 1.0 - Math.Cos(v);
        }
    }
}
