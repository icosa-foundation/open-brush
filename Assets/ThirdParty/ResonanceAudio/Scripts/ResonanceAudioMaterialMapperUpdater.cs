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

using UnityEngine;

/// Resonance Audio material mapper updater. This class is used to receive Update events during Edit
/// Mode (which are triggered whenever the scene is modified and the material mappings are changed
/// etc.), then calls a delegate to refresh the material mapper.
[AddComponentMenu("")]
[ExecuteInEditMode]
public class ResonanceAudioMaterialMapperUpdater : MonoBehaviour {
  /// The delegate to call to refresh the material mapper.
  public delegate void RefreshMaterialMapperDelegate();
  public RefreshMaterialMapperDelegate RefreshMaterialMapper = null;

  void Update() {
    if (Application.isEditor && !Application.isPlaying && RefreshMaterialMapper != null) {
      RefreshMaterialMapper();
    }
  }
}
