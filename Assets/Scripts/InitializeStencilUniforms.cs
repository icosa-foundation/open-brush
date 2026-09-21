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

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrush
{
    // Keep the MonoBehaviour type for existing prefab references. Initialization is global
    // and does not depend on a guide or settings panel being instantiated.
    public class InitializeStencilUniforms : MonoBehaviour
    {
        // Runs for each player/Play-mode session, including with domain reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeDefaults()
        {
            Shader.SetGlobalFloat(ModifyStencilGridSizeCommand.GlobalGridSizeMultiplierHash, 1f);
            Shader.SetGlobalFloat(ModifyStencilGridLineWidthCommand.GlobalGridLineWidthMultiplierHash, 1f);
            Shader.SetGlobalFloat(ModifyStencilFrameWidthCommand.GlobalFrameWidthMultiplierHash, 1f);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitializeEditor()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // A script reload during play must not reset the user's current settings.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                InitializeDefaults();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                InitializeDefaults();
            }
        }
#endif
    }
} // namespace TiltBrush
