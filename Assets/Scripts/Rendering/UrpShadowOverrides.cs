// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ShadowQuality = UnityEngine.ShadowQuality;
using ShadowResolution = UnityEngine.ShadowResolution;

namespace TiltBrush
{
    /// <summary>Applies nullable overrides to an owned pipeline, starting from its authored policy.</summary>
    internal sealed class UrpShadowOverrides
    {
        // URP exposes these serialized flags with internal setters. Keep the version-specific
        // names here; JsonUtility can patch serialized ScriptableObject fields in players.
        [Serializable]
        private struct ShadowSupport
        {
            public bool m_MainLightShadowsSupported;
            public bool m_AdditionalLightShadowsSupported;
            public bool m_SoftShadowsSupported;
#if UNITY_EDITOR
            public bool m_AnyShadowsSupported;
#endif
        }

        private readonly ShadowSupport m_Support;
        private readonly float m_Distance;
        private readonly int m_MainResolution;
        private readonly int m_AdditionalResolution;

        internal UrpShadowOverrides(UniversalRenderPipelineAsset authored)
        {
            m_Support = new ShadowSupport
            {
                m_MainLightShadowsSupported = authored.supportsMainLightShadows,
                m_AdditionalLightShadowsSupported = authored.supportsAdditionalLightShadows,
                m_SoftShadowsSupported = authored.supportsSoftShadows
            };
            m_Distance = authored.shadowDistance;
            m_MainResolution = authored.mainLightShadowmapResolution;
            m_AdditionalResolution = authored.additionalLightsShadowmapResolution;
        }

        internal void Apply(UniversalRenderPipelineAsset owned, int? mode, int? resolution,
                            float? distance)
        {
            var support = m_Support;
            float effectiveDistance = m_Distance;
            if (mode.HasValue && Enum.IsDefined(typeof(ShadowQuality), mode.Value))
            {
                var quality = (ShadowQuality)mode.Value;
                support.m_MainLightShadowsSupported = quality != ShadowQuality.Disable;
                support.m_AdditionalLightShadowsSupported =
                    quality != ShadowQuality.Disable && m_Support.m_AdditionalLightShadowsSupported;
                support.m_SoftShadowsSupported = quality == ShadowQuality.All;
                // The no-shadow tier has zero distance; a mode-only opt-in must still work.
                if (quality != ShadowQuality.Disable && effectiveDistance == 0)
                    effectiveDistance = 30;
            }
#if UNITY_EDITOR
            support.m_AnyShadowsSupported =
                support.m_MainLightShadowsSupported || support.m_AdditionalLightShadowsSupported;
#endif
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(support), owned);
            owned.shadowDistance = distance ?? effectiveDistance;
            owned.mainLightShadowmapResolution = m_MainResolution;
            owned.additionalLightsShadowmapResolution = m_AdditionalResolution;
            if (resolution.HasValue && Enum.IsDefined(typeof(ShadowResolution), resolution.Value))
            {
                // Unity's enum is an ordinal; URP expects a pixel dimension.
                int pixels = 512 << resolution.Value;
                owned.mainLightShadowmapResolution = pixels;
                owned.additionalLightsShadowmapResolution = pixels;
            }
        }
    }
}
