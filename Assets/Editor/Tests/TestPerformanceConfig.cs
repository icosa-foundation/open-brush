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

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TiltBrush
{
    internal class TestPerformanceConfig
    {
        [Test]
        public void TestValidateClearsInvalidOverrides()
        {
            var config = new UserConfig.PerformanceConfig
            {
                HullBrushMaxVertInputs = 0,
                HullBrushMaxKnots = -1,
                ReferenceImagesMaxFileSize = 0,
                ReferenceImagesMaxDimension = -1,
                ReferenceImagesResizeDimension = 0,
                QuickLoadMaxDistancePerFrame = -1,
                MaxSnapshotDimension = 0,
                OverrideQuestFoveationLevel = 4,
                ShadowMode = int.MaxValue,
                ShadowResolution = int.MaxValue,
                ShadowDistance = -1,
                LodBias = 0,
                SkinWeights = int.MaxValue,
            };

            ExpectInvalidValueWarnings(
                nameof(config.HullBrushMaxVertInputs),
                nameof(config.HullBrushMaxKnots),
                nameof(config.ReferenceImagesMaxFileSize),
                nameof(config.ReferenceImagesMaxDimension),
                nameof(config.ReferenceImagesResizeDimension),
                nameof(config.QuickLoadMaxDistancePerFrame),
                nameof(config.MaxSnapshotDimension),
                nameof(config.OverrideQuestFoveationLevel),
                nameof(config.ShadowMode),
                nameof(config.ShadowResolution),
                nameof(config.ShadowDistance),
                nameof(config.LodBias),
                nameof(config.SkinWeights));

            config.Validate();

            Assert.That(config.HullBrushMaxVertInputs, Is.Null);
            Assert.That(config.HullBrushMaxKnots, Is.Null);
            Assert.That(config.ReferenceImagesMaxFileSize, Is.Null);
            Assert.That(config.ReferenceImagesMaxDimension, Is.Null);
            Assert.That(config.ReferenceImagesResizeDimension, Is.Null);
            Assert.That(config.QuickLoadMaxDistancePerFrame, Is.Null);
            Assert.That(config.MaxSnapshotDimension, Is.Null);
            Assert.That(config.OverrideQuestFoveationLevel, Is.Null);
            Assert.That(config.ShadowMode, Is.Null);
            Assert.That(config.ShadowResolution, Is.Null);
            Assert.That(config.ShadowDistance, Is.Null);
            Assert.That(config.LodBias, Is.Null);
            Assert.That(config.SkinWeights, Is.Null);
        }

        [Test]
        public void TestValidatePreservesValidBoundaryValues()
        {
            var config = new UserConfig.PerformanceConfig
            {
                HullBrushMaxVertInputs = 1,
                HullBrushMaxKnots = 1,
                ReferenceImagesMaxFileSize = 1,
                ReferenceImagesMaxDimension = 1,
                ReferenceImagesResizeDimension = 1,
                QuickLoadMaxDistancePerFrame = float.Epsilon,
                MaxSnapshotDimension = 1,
                OverrideQuestFoveationLevel = 0,
                ShadowMode = (int)ShadowQuality.Disable,
                ShadowResolution = (int)UnityEngine.ShadowResolution.Low,
                ShadowDistance = 0,
                LodBias = float.Epsilon,
                SkinWeights = (int)UnityEngine.SkinWeights.OneBone,
            };

            config.Validate();

            Assert.That(config.HullBrushMaxVertInputs, Is.EqualTo(1));
            Assert.That(config.QuickLoadMaxDistancePerFrame, Is.EqualTo(float.Epsilon));
            Assert.That(config.OverrideQuestFoveationLevel, Is.EqualTo(0));
            Assert.That(config.ShadowDistance, Is.EqualTo(0));
            Assert.That(config.LodBias, Is.EqualTo(float.Epsilon));

            config.OverrideQuestFoveationLevel = 3;
            config.Validate();
            Assert.That(config.OverrideQuestFoveationLevel, Is.EqualTo(3));
        }

        private static void ExpectInvalidValueWarnings(params string[] settingNames)
        {
            foreach (string settingName in settingNames)
            {
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex(
                        $"^\\[PerformanceOverrides\\] Ignoring invalid {settingName} value "));
            }
        }
    }
}
