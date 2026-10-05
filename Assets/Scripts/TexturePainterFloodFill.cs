// Copyright 2024 The Open Brush Authors
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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public static class TexturePainterFloodFill
    {
        public static void FillFromPoint(Texture2D texture, Color color, Vector2Int point, float threshold = 0f)
        {
            FillFromPoints(texture, color, new Vector2Int[] { point }, threshold);
        }

        public static void FillFromCorners(Texture2D texture, Color color, float threshold = 0f)
        {
            var points = new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(texture.width - 1, 0),
                new Vector2Int(0, texture.height - 1),
                new Vector2Int(texture.width - 1, texture.height - 1)
            };
            FillFromPoints(texture, color, points, threshold);
        }

        public static void FillFromPoints(Texture2D texture, Color color, Vector2Int[] points, float threshold = 0f)
        {
            Color[] pixelsLinear = texture.GetPixels();
            int width = texture.width;
            int height = texture.height;

            foreach (Vector2Int point in points)
            {
                FillPixels(pixelsLinear, point, width, height, color, threshold);
            }

            texture.SetPixels(pixelsLinear);
            texture.Apply();
        }

        static void FillPixels(Color[] pixels, Vector2Int startPoint, int width, int height, Color color, float threshold)
        {
            bool[] pixelsHandled = new bool[width * height];
            Color originColor = pixels[startPoint.y * width + startPoint.x];
            var stack = new Stack<Vector2Int>();
            stack.Push(startPoint);

            while (stack.Count > 0)
            {
                Vector2Int point = stack.Pop();
                int index = point.y * width + point.x;

                if (point.x >= 0 && point.x < width && point.y >= 0 && point.y < height && !pixelsHandled[index])
                {
                    pixelsHandled[index] = true;

                    if (ColorDistance(pixels[index], originColor) <= threshold)
                    {
                        pixels[index] = color;

                        stack.Push(new Vector2Int(point.x - 1, point.y));
                        stack.Push(new Vector2Int(point.x + 1, point.y));
                        stack.Push(new Vector2Int(point.x, point.y - 1));
                        stack.Push(new Vector2Int(point.x, point.y + 1));
                    }
                }
            }
        }

        static float ColorDistance(Color color1, Color color2)
        {
            return Mathf.Sqrt(
                Mathf.Pow(color1.r - color2.r, 2) +
                Mathf.Pow(color1.g - color2.g, 2) +
                Mathf.Pow(color1.b - color2.b, 2)
            );
        }
    }
}