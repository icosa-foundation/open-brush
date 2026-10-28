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

        // Lives on the passthrough environment prefab, so Start and OnDestroy bracket the time
        // the passthrough environment is active.
        void Start()
        {
            if (App.VrSdk.PassthroughMode == PassthroughMode.None)
            {
                return;
            }

            if (App.VrSdk.PassthroughMode == PassthroughMode.FBPassthrough)
            {
                // Quest's FB passthrough path must retain Unity's fallback projection layer.
                // Registering an explicit Default Scene Layer causes the mobile selection
                // render pass to leave the Quest GPU waiting indefinitely on eye-buffer fences.
                m_PassthroughLayer.enabled = true;
                if (METABoundaryVisibility.FeatureEnabled)
                {
                    METABoundaryVisibility.SuppressBoundaryVisibility(false);
                }
            }
            else if (App.VrSdk.PassthroughMode == PassthroughMode.OpenXREnvionmentBlendMode)
            {
                // Environment-blend runtimes need an explicit default layer so its blend type
                // is consistent and can be changed at runtime.
                SetDefaultSceneLayer(true);
                SetBlendModePassthrough(true);
            }
        }

        void OnDestroy()
        {
            // Also runs on quit, after App has gone.
            if (App.Instance == null || App.VrSdk == null)
            {
                return;
            }
            if (App.VrSdk.PassthroughMode == PassthroughMode.OpenXREnvionmentBlendMode)
            {
                SetBlendModePassthrough(false);
            }
            if (App.VrSdk.PassthroughMode == PassthroughMode.OpenXREnvionmentBlendMode)
            {
                SetDefaultSceneLayer(false);
            }
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

        static void SetDefaultSceneLayer(bool active)
        {
            var layer = FindDefaultSceneLayer();
            if (layer == null)
            {
                return;
            }
            var current = CompositionLayerManager.Instance?.DefaultSceneCompositionLayer;
            if (active && current != null && current != layer)
            {
                Debug.LogWarning("[Passthrough] Another default scene layer is already " +
                    $"registered; eye buffer blend type stays {current.LayerData.BlendType}.");
                return;
            }
            layer.enabled = active;
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
