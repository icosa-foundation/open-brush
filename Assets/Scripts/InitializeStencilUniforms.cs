// Copyright 2021 The Open Brush Authors
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

using System;
using UnityEngine;

namespace TiltBrush
{
    /*
     * The stencils are textured procedurally and modified via global float parameters.
     * This class exists to make sure that those parameters are non-zero which enables
     * the stencil to be visible in the editor.
     */

    public class InitializeStencilUniforms : MonoBehaviour
    {
        // Update uniforms here as this is the least intrusive place to do so
        private void OnDrawGizmos()
        {
            Shader.SetGlobalFloat(ModifyStencilGridSizeCommand.GlobalGridSizeMultiplierHash, 1f);
            Shader.SetGlobalFloat(ModifyStencilGridLineWidthCommand.GlobalGridLineWidthMultiplierHash, 1f);
            Shader.SetGlobalFloat(ModifyStencilFrameWidthCommand.GlobalFrameWidthMultiplierHash, 1f);
        }
    }
} // namespace TiltBrush
