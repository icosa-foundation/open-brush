// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Only invoke custom build processor when building for Android or iOS.
#if UNITY_ANDROID || UNITY_IOS
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
#if UNITY_2018_1_OR_NEWER
using UnityEditor.Build.Reporting;
#endif  // UNITY_2018_1_OR_NEWER

// Notify developer if conflicting Google VR audio libraries are present.
class ResonanceAudioBuildProcessor : IPreprocessBuild {
  private const string StripAssetPathPrefix = "Assets/";

  // Finds the following assets:
  // …/Android/libs/armeabi-v7a/libaudioplugingvrunity.so
  // …/Android/libs/x86/libaudioplugingvrunity.so
  // …/iOS/libaudioplugingvrunity.a
  // …/x86_64/libaudioplugingvrunity.so
  private const string gvrAudioLibraryAssetName = "libaudioplugingvrunity";

  // Finds the following assets:
  // …/iOS/GvrAudioAppController.h
  // …/iOS/GvrAudioAppController.mm
  private const string gvrAudioAppControllerAssetName = "GvrAudioAppController";

  // Finds the following assets:
  // …/Resources/GvrAudioMixer.mixer
  private const string gvrAudioMixerAssetName = "GvrAudioMixer";

  // Search entire project since asset locations varied across releases.
  private const string[] gvrAudioLibrarySearchInFolders = null;

  public int callbackOrder {
    get { return 0; }
  }

#if UNITY_2018_1_OR_NEWER
  public void OnPreprocessBuild(BuildReport report) {
    OnPreprocessBuild(report.summary.platform, report.summary.outputPath);
  }
#endif  // UNITY_2018_1_OR_NEWER

  public void OnPreprocessBuild(BuildTarget target, string path) {
    // Find Google VR audio asset to delete.
    List<string> assetPaths = new List<string>();
    assetPaths.AddRange(FindAssetPaths(gvrAudioLibraryAssetName));
    assetPaths.AddRange(FindAssetPaths(gvrAudioAppControllerAssetName));
    assetPaths.AddRange(FindAssetPaths(gvrAudioMixerAssetName));

    if (assetPaths.Count == 0) {
      // No incompatible Google VR audio assets found.
      return;
    }

    // Ask permission to delete incompatible assets.
    string message = string.Format("Found {0} incompatible Google VR audio assets:\n\n" +
      "{1}\n" +
      "For more information, including instructions for removing" +
      " additional unused Google VR audio prefabs and scripts, see:\n\n" +
      "{2}",
      assetPaths.Count, AsBulletedList(assetPaths),
      "https://developers.google.com/resonance-audio/migrate");
    string okButtonText = String.Format("DELETE {0} ASSETS", assetPaths.Count);
    if (EditorUtility.DisplayDialog("Resonance Audio upgrade", message, okButtonText, "Cancel")) {
      // Delete incompatible Google VR audio assets.
      foreach (string assetPath in assetPaths) {
        if (!AssetDatabase.DeleteAsset(assetPath)) {
          Debug.LogErrorFormat("Error deleting Google VR audio asset: {0}", assetPath);
        }
      }
    } else {
      // Generate one error for each incompatible asset. Unity build process will summarize.
      foreach (string assetPath in assetPaths) {
        Debug.LogErrorFormat("Found incompatible Google VR audio assets: {0}", assetPath);
      }
    }
  }

  private string AsBulletedList(List<string> paths) {
    StringBuilder builder = new StringBuilder();
    for (int i = 0; i < paths.Count; i++) {
      string path = paths[i];
      if (path.StartsWith(StripAssetPathPrefix)) {
        path = path.Substring(StripAssetPathPrefix.Length);
      }
      builder.Append(i + 1).Append(". ").Append(path).Append('\n');
    }
    return builder.ToString();
  }

  private List<string> FindAssetPaths(string filter) {
    List<string> paths = new List<string>();
    foreach (string guid in AssetDatabase.FindAssets(filter, gvrAudioLibrarySearchInFolders)) {
      paths.Add(AssetDatabase.GUIDToAssetPath(guid));
    }
    return paths;
  }
}
#endif  // UNITY_ANDROID || UNITY_IOS
