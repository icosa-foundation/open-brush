// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Reflection;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestOpenBrushExportPlugin
    {
        [Test]
        public void TestStrokeTimestampDataMatchesLegacyLayout()
        {
            var stroke = new Stroke
            {
                m_ControlPoints = new[]
                {
                    new PointerManager.ControlPoint { m_TimestampMs = 1000 },
                    new PointerManager.ControlPoint { m_TimestampMs = 3000 },
                    new PointerManager.ControlPoint { m_TimestampMs = 7000 },
                }
            };

            byte[] data = CreateTimestampData(stroke, 5);
            var timestamps = new float[data.Length / sizeof(float)];
            Buffer.BlockCopy(data, 0, timestamps, 0, data.Length);

            CollectionAssert.AreEqual(new[]
            {
                1f, 7f, 1f,
                1f, 7f, 2f,
                1f, 7f, 3f,
                1f, 7f, 5f,
                1f, 7f, 7f,
            }, timestamps);
        }

        [Test]
        public void TestSingleVertexStrokeUsesFirstControlPointTime()
        {
            var stroke = new Stroke
            {
                m_ControlPoints = new[]
                {
                    new PointerManager.ControlPoint { m_TimestampMs = 1000 },
                    new PointerManager.ControlPoint { m_TimestampMs = 3000 },
                }
            };

            byte[] data = CreateTimestampData(stroke, 1);
            var timestamps = new float[3];
            Buffer.BlockCopy(data, 0, timestamps, 0, data.Length);

            CollectionAssert.AreEqual(new[] { 1f, 3f, 1f }, timestamps);
        }

        private static byte[] CreateTimestampData(Stroke stroke, int vertexCount)
        {
            MethodInfo method = typeof(OpenBrushExportPluginConfig).GetMethod(
                "CreateTimestampData",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(Stroke), typeof(int) },
                null);
            Assert.IsNotNull(method);
            return (byte[])method.Invoke(null, new object[] { stroke, vertexCount });
        }
    }
}
