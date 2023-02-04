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
using UnityEngine;

namespace TiltBrush
{
    public class CHRFont : ScriptableObject
    {
        private Dictionary<int, List<List<Vector2>>> _outlines;
        private Dictionary<int, float> _widths;
        public float Height;
        public string DataRaw;

        public Dictionary<int, List<List<Vector2>>> Outlines
        {
            get { return _outlines; }
        }

        public Dictionary<int, float> Widths
        {
            get { return _widths; }
        }

        private void OnEnable()
        {
            var fontDataLines = DataRaw.Split('\n');
            _outlines = new Dictionary<int, List<List<Vector2>>>();
            _widths = new Dictionary<int, float>();

            foreach (string line in fontDataLines)
            {
                if (line.Trim().Length < 1) continue;
                var item = line.Split(new[] { ';' }, 2);
                var lineHeader = item[0];
                var lineHeaderData = lineHeader.Split(' ');
                var asciiCode = int.Parse(lineHeaderData[0].Split('_')[1], System.Globalization.NumberStyles.HexNumber);
                var width = float.Parse(lineHeaderData[1]) / 20f;
                var lineData = item[1];
                if (lineData.Trim().Length < 1) continue;
                var strokesStrings = lineData.Trim().Split(';');
                var strokes = new List<List<Vector2>>();
                foreach (var stroke in strokesStrings)
                {
                    var pointsString = stroke.Trim().Split(' ');
                    if (pointsString.Length < 1) continue;
                    var points = new List<Vector2>();

                    foreach (var point in pointsString)
                    {
                        var coordsString = point.Trim().Split(',');
                        if (coordsString.Length < 1) continue;
                        float xCoord = float.Parse(coordsString[0]);
                        float yCoord = float.Parse(coordsString[1]);
                        var coord = new Vector2(xCoord / 20f, yCoord / 20f);
                        Height = Mathf.Max(Height, coord.y);
                        points.Add(coord);
                    }

                    strokes.Add(points);
                }

                _outlines[asciiCode] = strokes;
                _widths[asciiCode] = width;
            }
        }
    }
}








