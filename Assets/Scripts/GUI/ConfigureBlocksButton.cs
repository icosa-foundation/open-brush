// Copyright 2025 The Open Brush Authors
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
namespace TiltBrush
{
    public class ConfigureBlocksButton : MonoBehaviour
    {
        protected void Awake()
        {
            this.GetComponent<OpenBrowserButton>().m_Url = GetBlocksStoreUrl();
        }


        public static string GetBlocksStoreUrl()
        {
#if UNITY_ANDROID
            bool isQuestNative = AndroidUtils.IsPackageInstalled("com.oculus.platformsdkruntime");
#else
            bool isQuestNative = false;
#endif
            if (isQuestNative)
            {
                // Actually running on a Quest. Open the Quest store link.
                return "https://www.meta.com/en-gb/experiences/open-blocks-low-poly-3d-modelling/8043509915705378/";
            }
            else if (!App.Config.IsMobileHardware)
            {
                // All PC users should use Steam for now.
                return "https://store.steampowered.com/app/3077230/Open_Blocks/";
            }
            else
            {
                // At this point it should be an Android device that is not a Quest.
                // Docs url. Good fallback and easy to update with current advice
                return "https://docs.openblocks.app/getting-open-blocks";
            }
        }
    }
}
