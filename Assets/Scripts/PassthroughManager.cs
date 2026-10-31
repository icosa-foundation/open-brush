// Copyright 2022-2023 The Open Brush Authors
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

using UnityEngine;
using OpenXR.Extensions;
using Unity.XR.CompositionLayers;
using Unity.XR.CompositionLayers.Layers;
using Unity.XR.CompositionLayers.Services;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;

namespace TiltBrush
{
    public class PassthroughManager : MonoBehaviour
    {
        [SerializeField] CompositionLayer m_PassthroughLayer;

        private bool m_Active;
        private bool m_RequestedActive;
        private PassthroughMode m_ActiveMode;
        private CompositionLayer m_DefaultSceneLayer;
        private bool m_OwnsDefaultSceneLayer;

        // SceneSettings owns this instance independently of the room geometry.
        // In particular, creating an environment prefab must not activate passthrough.
        public bool SetPassthroughActive(bool active)
        {
            if (active && !isActiveAndEnabled) return false;
            if (!ApplyPassthroughActive(active)) return false;
            m_RequestedActive = active;
            return true;
        }

        private bool ApplyPassthroughActive(bool active)
        {
            if (active == m_Active) return true;
            if (active)
            {
                if (App.Instance == null || App.VrSdk == null ||
                    App.VrSdk.PassthroughMode == PassthroughMode.None) return false;
                m_ActiveMode = App.VrSdk.PassthroughMode;
                bool needsLayer = m_ActiveMode == PassthroughMode.FBPassthrough ||
                    m_ActiveMode == PassthroughMode.OpenXREnvionmentBlendMode;
                if (needsLayer)
                {
                    m_DefaultSceneLayer = FindDefaultSceneLayer();
                    var current = CompositionLayerManager.Instance?.DefaultSceneCompositionLayer;
                    if (m_DefaultSceneLayer == null ||
                        (current != null && current != m_DefaultSceneLayer))
                    {
                        Debug.LogWarning("[OB_BACKDROP] Cannot acquire the rig's default scene layer.");
                        return false;
                    }
                    if (m_ActiveMode == PassthroughMode.FBPassthrough && m_PassthroughLayer == null)
                        return false;
                    m_OwnsDefaultSceneLayer = !m_DefaultSceneLayer.enabled;
                    // Register this before enabling FB passthrough, which may create a fallback.
                    m_DefaultSceneLayer.enabled = true;
                }
                m_Active = true;
                if (m_ActiveMode == PassthroughMode.FBPassthrough)
                {
                    m_PassthroughLayer.enabled = true;
                    if (METABoundaryVisibility.FeatureEnabled)
                        METABoundaryVisibility.SuppressBoundaryVisibility(false);
                }
                else if (m_ActiveMode == PassthroughMode.OpenXREnvionmentBlendMode)
                {
                    SetBlendModePassthrough(true);
                }
            }
            else
            {
                // Clear ownership before any disabling callback can re-enter cleanup.
                m_Active = false;
                if (m_ActiveMode == PassthroughMode.FBPassthrough && m_PassthroughLayer != null)
                    m_PassthroughLayer.enabled = false;
                if (m_ActiveMode == PassthroughMode.OpenXREnvionmentBlendMode &&
                    App.Instance != null && App.VrSdk != null)
                    SetBlendModePassthrough(false);
                if (m_OwnsDefaultSceneLayer && m_DefaultSceneLayer != null)
                    m_DefaultSceneLayer.enabled = false;
                m_OwnsDefaultSceneLayer = false;
                m_DefaultSceneLayer = null;
            }
            return true;
        }

        private void OnEnable()
        {
            if (m_RequestedActive) ApplyPassthroughActive(true);
        }

        private void OnDisable() => ApplyPassthroughActive(false);
        private void OnDestroy()
        {
            m_RequestedActive = false;
            ApplyPassthroughActive(false);
        }

        // The rig's Default Scene Layer carries the blend type for the eye buffer.
        static CompositionLayer FindDefaultSceneLayer()
        {
            var origin = App.VrSdk.GetVrCamera().GetComponentInParent<XROrigin>();
            if (origin == null)
            {
                return null;
            }
            foreach (var layer in origin.GetComponentsInChildren<CompositionLayer>(true))
            {
                if (layer.LayerData is DefaultLayerData)
                {
                    return layer;
                }
            }
            return null;
        }

        /// Runtime override of the eye buffer blend type, for comparing how brushes composite
        /// over passthrough without a rebuild. Takes effect immediately if the passthrough
        /// environment is active, otherwise the next time it is. Not saved.
        public static string SetEyeBufferBlendType(BlendType? blendType)
        {
            var layer = FindDefaultSceneLayer();
            if (layer == null)
            {
                return "No Default Scene Layer found on the XR rig";
            }
            if (blendType.HasValue)
            {
                // The setter reports a state change, which resubmits the layer flags.
                layer.LayerData.BlendType = blendType.Value;
                Debug.Log($"[Passthrough] Eye buffer blend type set to {blendType.Value}");
            }
            // BlendType falls back to a supported type if the platform rejects the request.
            return $"requested={layer.LayerData.BlendTypeDirectly} " +
                $"effective={layer.LayerData.BlendType} layerEnabled={layer.enabled}";
        }

        static void SetBlendModePassthrough(bool active)
        {
            // Android XR's camera feature owns the blend mode while its camera subsystem exists:
            // it resets any mode that disagrees with whether the AR Camera Manager is enabled.
            // On other runtimes the manager has no subsystem and enabling it does nothing.
            var cameraManager = App.VrSdk.GetVrCamera().GetComponent<ARCameraManager>();
            if (cameraManager != null)
            {
                cameraManager.enabled = active;
            }
            EnvironmentBlendPassthroughFeature.SetPassthrough(active);
        }
    }
}
