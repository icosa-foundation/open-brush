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
using System.Collections;

/// Resonance Audio room component that simulates environmental effects of a room with respect to
/// the properties of the attached game object.
[AddComponentMenu("ResonanceAudio/ResonanceAudioRoom")]
public class ResonanceAudioRoom : MonoBehaviour {
  /// Room surface material in negative x direction.
  [Tooltip("Left wall surface material used to calculate the acoustic properties of the room.")]
  public ResonanceAudioRoomManager.SurfaceMaterial leftWall =
      ResonanceAudioRoomManager.SurfaceMaterial.ConcreteBlockCoarse;

  /// Room surface material in positive x direction.
  [Tooltip("Right wall surface material used to calculate the acoustic properties of the room.")]
  public ResonanceAudioRoomManager.SurfaceMaterial rightWall =
      ResonanceAudioRoomManager.SurfaceMaterial.ConcreteBlockCoarse;

  /// Room surface material in negative y direction.
  [Tooltip("Floor surface material used to calculate the acoustic properties of the room.")]
  public ResonanceAudioRoomManager.SurfaceMaterial floor =
      ResonanceAudioRoomManager.SurfaceMaterial.ParquetOnConcrete;

  /// Room surface material in positive y direction.
  [Tooltip("Ceiling surface material used to calculate the acoustic properties of the room.")]
  public ResonanceAudioRoomManager.SurfaceMaterial ceiling =
      ResonanceAudioRoomManager.SurfaceMaterial.PlasterRough;

  /// Room surface material in negative z direction.
  [Tooltip("Back wall surface material used to calculate the acoustic properties of the room.")]
  public ResonanceAudioRoomManager.SurfaceMaterial backWall =
      ResonanceAudioRoomManager.SurfaceMaterial.ConcreteBlockCoarse;

  /// Room surface material in positive z direction.
  [Tooltip("Front wall surface material used to calculate the acoustic properties of the room.")]
  public ResonanceAudioRoomManager.SurfaceMaterial frontWall =
      ResonanceAudioRoomManager.SurfaceMaterial.ConcreteBlockCoarse;

  /// Reflectivity scalar for each surface of the room.
  [Tooltip("Adjusts what proportion of the direct sound is reflected back by each surface, after " +
           "an appropriate delay. Reverberation is unaffected by this setting.")]
  public float reflectivity = 1.0f;

  /// Reverb gain modifier in decibels.
  [Tooltip("Adjusts the reverb gain in the room.")]
  public float reverbGainDb = 0.0f;

  /// Reverb brightness modifier.
  [Tooltip("Adjusts the balance between high and low frequencies in the reverb. Increasing this " +
           "value will increase high frequencies in the reverb, while decreasing the low " +
           "frequencies respectively.")]
  public float reverbBrightness = 0.0f;

  /// Reverb time modifier.
  [Tooltip("Adjusts the overall duration of the reverb by a positive scaling factor.")]
  public float reverbTime = 1.0f;

  /// Size of the room (normalized with respect to scale of the game object).
  [Tooltip("Sets the room dimensions in meters relative to the scale of the game object.")]
  public Vector3 size = Vector3.one;

  void OnEnable() {
    ResonanceAudioRoomManager.UpdateRoom(this);
  }

  void OnDisable() {
    ResonanceAudioRoomManager.RemoveRoom(this);
  }

  void Update() {
    ResonanceAudioRoomManager.UpdateRoom(this);
  }

  void OnDrawGizmosSelected() {
    // Draw shoebox model wireframe of the room.
    Gizmos.color = Color.yellow;
    Gizmos.matrix = transform.localToWorldMatrix;
    Gizmos.DrawWireCube(Vector3.zero, size);
  }
}
