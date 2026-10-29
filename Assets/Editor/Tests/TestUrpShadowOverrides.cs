// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ShadowQuality = UnityEngine.ShadowQuality;
using ShadowResolution = UnityEngine.ShadowResolution;

namespace TiltBrush
{
    internal class TestUrpShadowOverrides
    {
        [Test]
        public void ModeOnlyOptInWorksAndClearingOverridesRestoresAuthoredPolicy()
        {
            var authored = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            JsonUtility.FromJsonOverwrite(
                "{\"m_MainLightShadowsSupported\":false,\"m_AdditionalLightShadowsSupported\":false,\"m_SoftShadowsSupported\":false}",
                authored);
            authored.shadowDistance = 0;
            authored.mainLightShadowmapResolution = 1024;
            authored.additionalLightsShadowmapResolution = 512;
            var owned = Object.Instantiate(authored);
            try
            {
                var policy = new UrpShadowOverrides(authored);
                policy.Apply(owned, (int)ShadowQuality.All, (int)ShadowResolution.VeryHigh, null);
                Assert.IsTrue(owned.supportsMainLightShadows);
                Assert.IsFalse(owned.supportsAdditionalLightShadows);
                Assert.IsTrue(owned.supportsSoftShadows);
                Assert.AreEqual(30, owned.shadowDistance);
                Assert.AreEqual(4096, owned.mainLightShadowmapResolution);

                policy.Apply(owned, (int)ShadowQuality.HardOnly, null, 0);
                Assert.IsFalse(owned.supportsSoftShadows);
                Assert.AreEqual(0, owned.shadowDistance, "Explicit zero must win over mode-only fallback");

                policy.Apply(owned, null, null, null);
                Assert.IsFalse(owned.supportsMainLightShadows);
                Assert.IsFalse(owned.supportsSoftShadows);
                Assert.AreEqual(0, owned.shadowDistance);
                Assert.AreEqual(1024, owned.mainLightShadowmapResolution);
                Assert.AreEqual(512, owned.additionalLightsShadowmapResolution);
                Assert.AreEqual(0, authored.shadowDistance);
                Assert.IsFalse(authored.supportsMainLightShadows);
            }
            finally
            {
                Object.DestroyImmediate(owned);
                Object.DestroyImmediate(authored);
            }
        }

        [Test]
        public void DisabledModeSuppressesBothLightTypesAndNewTierGetsItsOwnBaseline()
        {
            var authored = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            JsonUtility.FromJsonOverwrite(
                "{\"m_MainLightShadowsSupported\":true,\"m_AdditionalLightShadowsSupported\":true,\"m_SoftShadowsSupported\":true}",
                authored);
            authored.shadowDistance = 50;
            var owned = Object.Instantiate(authored);
            try
            {
                var policy = new UrpShadowOverrides(authored);
                policy.Apply(owned, (int)ShadowQuality.Disable, null, null);
                Assert.IsFalse(owned.supportsMainLightShadows);
                Assert.IsFalse(owned.supportsAdditionalLightShadows);
                policy.Apply(owned, null, null, null);
                Assert.IsTrue(owned.supportsMainLightShadows);
                Assert.IsTrue(owned.supportsAdditionalLightShadows);
                Assert.IsTrue(owned.supportsSoftShadows);
                Assert.AreEqual(50, owned.shadowDistance);
            }
            finally
            {
                Object.DestroyImmediate(owned);
                Object.DestroyImmediate(authored);
            }
        }
    }
}
