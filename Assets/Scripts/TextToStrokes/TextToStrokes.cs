// Copyright 2021 The Open Brush Authors
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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{

    public class TextToStrokes
    {
        private CHRFont _font;

        public TextToStrokes(CHRFont font)
        {
            _font = font;
        }

        public List<List<TrTransform>> Build(string text)
        {
            var allPaths = new List<List<TrTransform>>();
            Vector2 offset = Vector2.zero;

            foreach (var character in text)
            {
                if (character == '\n')
                {
                    offset.y -= (_font.Height * 1.1f);
                    offset.x = 0;
                    continue;
                }

                var letterShape2d = new List<List<Vector2>>();
                if (_font.Outlines.TryGetValue(character, out var outline))
                {
                    letterShape2d = outline;
                }

                // Offset letter outline by the current total offset and convert to 3d TrTransform
                List<List<TrTransform>> pathList = letterShape2d.Select(
                    path => path.Select(
                        point => TrTransform.T(new Vector3(point.x + offset.x, point.y + offset.y, 0))
                    ).ToList()
                ).ToList();

                allPaths.AddRange(pathList);
                if (_font.Outlines.ContainsKey(character))
                {
                    offset.x += _font.Widths[character];
                }
                else
                {
                    // This is mainly to handle missing space characters.
                    // Is this a sane general default?
                    offset.x += 0.75f;
                }
            }
            return allPaths;
        }
    }
}
