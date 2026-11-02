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

using System.Linq;
using Gsplat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    internal class TestGsplatMigrationConfiguration
    {
        private const string k_SettingsPath =
            "Assets/Settings/Resources/GsplatSettings.asset";
        private const string k_RendererDataPath =
            "Assets/Settings/Open Brush Universal Render Pipeline Asset_Renderer.asset";

        [Test]
        public void SettingsRetainOpenBrushMigrationValues()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GsplatSettings>(k_SettingsPath);

            Assert.IsNotNull(settings);
            Assert.IsNotNull(settings.ComputeShader);
            Assert.IsNotNull(settings.IntersectionShader);
            Assert.IsNotNull(settings.GlobalMaterial);
            Assert.IsNotNull(settings.Materials);
            Assert.AreEqual(2, settings.Materials.Length);
            Assert.IsTrue(settings.Materials.All(material => material != null));
            Assert.IsTrue(settings.EnableGlobalSort);
            Assert.AreEqual(0.01f, settings.DepthPrepassAlphaCutoff);
            Assert.AreEqual(1u, settings.MaxRenderOrder);
            Assert.AreEqual("1.7.0", settings.Version.ToString());
        }

        [Test]
        public void RendererDataHasOneActiveGsplatFeature()
        {
            var rendererData =
                AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(k_RendererDataPath);

            Assert.IsNotNull(rendererData);
            var features = rendererData.rendererFeatures
                .Where(feature => feature != null &&
                                  feature.GetType().FullName == "Gsplat.GsplatURPFeature")
                .ToArray();
            Assert.AreEqual(1, features.Length);
            Assert.IsTrue(features[0].isActive);
        }
    }
}
