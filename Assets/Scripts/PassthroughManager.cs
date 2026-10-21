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
            if (App.VrSdk.PassthroughMode == PassthroughMode.FBPassthrough)
            {
                m_PassthroughLayer.enabled = true;
                if (METABoundaryVisibility.FeatureEnabled)
                {
                    METABoundaryVisibility.SuppressBoundaryVisibility(false);
                }
            }
            else if (App.VrSdk.PassthroughMode == PassthroughMode.OpenXREnvionmentBlendMode)
            {
                SetBlendModePassthrough(true);
            }
        }

        void OnDestroy()
        {
            // Also runs on quit, after App has gone.
            if (App.Instance != null && App.VrSdk != null &&
                App.VrSdk.PassthroughMode == PassthroughMode.OpenXREnvionmentBlendMode)
            {
                SetBlendModePassthrough(false);
            }
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
