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
using System.Collections.Generic;
using System.Linq;

/// Resonance Audio material map scriptable object that holds the mapping from GUIDs to surface
/// materials (which define the acoustic characteristics of surfaces). The GUID identifies either
/// a Unity Material (which gives a mesh its visual appearance) of a game object or the terrain
/// data of a terrain object.
[CreateAssetMenuAttribute(fileName = "New Material Map",
                          menuName = "ResonanceAudio/Material Map", order = 1000)]
public class ResonanceAudioMaterialMap : ScriptableObject {
  // Color coding for surface materials used to visualize the surface material assignment in the
  // scene as well as in the material picking UI.
  // The following colors are generated using http://vrl.cs.brown.edu/color, while setting a
  // starting point of "rgb(128,128,128)", and maximizing the "Perceptual Distance" and
  // "Pair Preference" parameters. This color mapping is shared by the shader and the preview
  // thumbnails of the surface materials (see InitializeColorArrayInShader() and
  // InitializeSurfaceMaterialPreviews()).
  public static readonly Color[] surfaceMaterialColors = new Color[] {
    new Color(0.500000f, 0.500000f, 0.500000f),
    new Color(0.545098f, 0.909804f, 0.678431f),
    new Color(0.184314f, 0.258824f, 0.521569f),
    new Color(0.552941f, 0.737255f, 0.976471f),
    new Color(0.035294f, 0.376471f, 0.074510f),
    new Color(0.952941f, 0.415686f, 0.835294f),
    new Color(0.105882f, 0.894118f, 0.427451f),
    new Color(0.541176f, 0.015686f, 0.345098f),
    new Color(0.631373f, 0.847059f, 0.196078f),
    new Color(0.513725f, 0.003922f, 0.741176f),
    new Color(0.949020f, 0.690196f, 0.964706f),
    new Color(0.082353f, 0.305882f, 0.337255f),
    new Color(0.152941f, 0.792157f, 0.901961f),
    new Color(0.921569f, 0.070588f, 0.254902f),
    new Color(0.274510f, 0.635294f, 0.423529f),
    new Color(0.556863f, 0.215686f, 0.066667f),
    new Color(0.960784f, 0.803922f, 0.686275f),
    new Color(0.305882f, 0.282353f, 0.035294f),
    new Color(0.917647f, 0.839216f, 0.141176f),
    new Color(0.521569f, 0.458824f, 0.858824f),
    new Color(0.937255f, 0.592157f, 0.176471f),
    new Color(0.980392f, 0.105882f, 0.988235f),
    new Color(0.725490f, 0.423529f, 0.552941f)
  };

  // Mapping from GUIDs to surface materials.
  [SerializeField]
  private ResonanceAudioRoomManager.SurfaceMaterialDictionary surfaceMaterialFromGuid = null;

  // Default surface material.
  private const ResonanceAudioRoomManager.SurfaceMaterial defaultSurfaceMaterial =
      ResonanceAudioRoomManager.SurfaceMaterial.Transparent;

  // Returns the list of GUIDs.
  public List<string> GuidList() {
    return surfaceMaterialFromGuid.Keys.ToList();
  }

  // Returns the surface material mapped from |guid|.
  public ResonanceAudioRoomManager.SurfaceMaterial GetMaterialFromGuid(string guid) {
    return surfaceMaterialFromGuid[guid];
  }

  // If |guid| is not mapped to a surface material yet, map it to the default surface material.
  public void AddDefaultMaterialIfGuidUnmapped(string guid) {
    if (!surfaceMaterialFromGuid.ContainsKey(guid)) {
      surfaceMaterialFromGuid.Add(guid, defaultSurfaceMaterial);
    }
  }
}
