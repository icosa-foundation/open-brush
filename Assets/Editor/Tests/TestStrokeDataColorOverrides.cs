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

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush.Tests
{
    internal class TestStrokeDataColorOverrides
    {
        [Test]
        public void AddModeUsesByteScaledBaseColorAndPreservesAlpha()
        {
            var stroke = new StrokeData
            {
                m_Color = new Color32(64, 128, 192, 77),
                m_ColorOverrideMode = ColorOverrideMode.Add,
                m_OverrideColors = new List<Color32?>
                {
                    new Color32(10, 20, 80, 255)
                }
            };

            Assert.AreEqual(new Color32(74, 148, 255, 77), stroke.GetColor(0));
        }
    }
}
