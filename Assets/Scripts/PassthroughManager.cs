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

namespace TiltBrush
{
    public class PassthroughManager : MonoBehaviour
    {
        [SerializeField] CompositionLayer m_PassthroughLayer;

        void Start()
        {
            if (App.VrSdk.PassthroughMode == PassthroughMode.FBPassthrough)
            {
                m_PassthroughLayer.enabled = true;
                if (METABoundaryVisibility.FeatureEnabled)
                {
                    METABoundaryVisibility.SetBoundaryVisibility(false);
                }
            }
        }
    }
}
