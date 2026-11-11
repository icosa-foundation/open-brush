// Copyright 2020 The Tilt Brush Authors
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

using UnityEditor;
using UnityEngine;
using System.Collections;

namespace TiltBrush
{
    public class ResetFirstPlay : MonoBehaviour
    {

        [MenuItem("Open Brush/Reset First Play", priority = 2000)]
        static public void Execute()
        {
            PlayerPrefs.DeleteKey(App.kPlayerPrefHasPlayedBefore);
        }

        [MenuItem("Open Brush/Reset Default Media", priority = 2001)]
        public static void ResetDefaultMedia()
        {
            foreach (string key in new[] {
                App.kPlayerPrefSeededDefaultModels, App.kPlayerPrefSeededDefaultQuillFiles,
                App.kPlayerPrefSeededDefaultBackgroundImages, App.kPlayerPrefSeededDefaultReferenceImages,
                App.kPlayerPrefSeededDefaultVideos, App.kPlayerPrefSeededDefaultSavedStrokes,
                App.kPlayerPrefSeededDefaultSoundClips, "Seeded bundled sketches"
            })
            {
                PlayerPrefs.DeleteKey(key);
                // Keep an empty handled-file record so legacy migration does not mark
                // the examples as already supplied when HasPlayedBefore is still true.
                PlayerPrefs.SetString($"{key}.HandledFilesV1", "");
            }
            PlayerPrefs.Save();
            Debug.Log("[ResetDefaultMedia] Default file installation records reset. Missing examples will be restored on the next Play.");
        }
    }
}
