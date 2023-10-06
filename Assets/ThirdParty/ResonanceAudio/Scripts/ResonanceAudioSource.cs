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

/// Resonance Audio source component that enhances AudioSource to provide advanced spatial audio
/// features.
[AddComponentMenu("ResonanceAudio/ResonanceAudioSource")]
[RequireComponent(typeof(AudioSource))]
[ExecuteInEditMode]
public class ResonanceAudioSource : MonoBehaviour {
  /// Audio rendering quality.
  public enum Quality {
    Stereo = 0,  ///< Stereo-only rendering.
    Low = 1,  ///< Low quality binaural rendering (first-order HRTF).
    High = 2  ///< High quality binaural rendering (third-order HRTF).
  }

  /// Denotes whether the room effects should be bypassed.
  [Tooltip("Sets whether the room effects for the source should be bypassed.")]
  public bool bypassRoomEffects = false;

  /// Directivity pattern shaping factor.
  [Range(0.0f, 1.0f)]
  [Tooltip("Controls the balance between a dipole pattern and an omnidirectional pattern for " +
           "source emission. By varying this value, different directivity patterns can be formed.")]
  public float directivityAlpha = 0.0f;

  /// Directivity pattern order.
  [Range(1.0f, 10.0f)]
  [Tooltip("Sets the sharpness of the source directivity pattern. Higher values will result in " +
           "increased directivity.")]
  public float directivitySharpness = 1.0f;

  /// Listener directivity pattern shaping factor.
  [Range(0.0f, 1.0f)]
  [Tooltip("Controls the balance between a dipole pattern and an omnidirectional pattern for " +
           "listener sensitivity. By varying this value, different directivity patterns can be " +
           "formed.")]
  public float listenerDirectivityAlpha = 0.0f;

  /// Listener directivity pattern order.
  [Range(1.0f, 10.0f)]
  [Tooltip("Sets the sharpness of the listener directivity pattern. Higher values will result in " +
           "increased directivity.")]
  public float listenerDirectivitySharpness = 1.0f;

  /// Input gain in decibels.
  [Tooltip("Applies a gain to the source for adjustment of relative loudness.")]
  public float gainDb = 0.0f;

  /// Denotes whether the near field effect should be applied.
  [Tooltip("Sets whether the near field effect should be applied when the distance between the " +
           "source and the listener is less than 1m (in Unity units).")]
  public bool nearFieldEffectEnabled = false;

  /// Near field effect gain.
  [Range(0.0f, 9.0f)]
  [Tooltip("Sets the nearfield effect gain. Note that the near field effect could result in " +
           "up to ~9x gain boost on the source input, therefore, it is advised to set smaller " +
           "gain values for louder sound sources to avoid clipping of the output signal.")]
  public float nearFieldEffectGain = 1.0f;

  /// Occlusion effect toggle.
  [Tooltip("Sets whether the sound of the source should be occluded when there are other objects " +
           "between the source and the listener.")]
  public bool occlusionEnabled = false;

  /// Occlusion effect intensity.
  [Range(0.0f, 10.0f)]
  [Tooltip("Sets the occlusion effect intensity. Higher values will result in a stronger effect " +
           "when the source is occluded.")]
  public float occlusionIntensity = 1.0f;

  /// Rendering quality of the audio source.
  [Tooltip("Sets the quality mode in which the spatial audio will be rendered. Higher quality " +
           "modes allow increased fidelity at the cost of greater CPU usage.")]
  public Quality quality = Quality.High;

  /// Unity audio source attached to the game object.
  public AudioSource audioSource { get; private set; }

  // Native audio spatializer effect data.
  private enum EffectData {
    Id = 0,  // ID.
    DistanceAttenuation = 1,  // Computed distance attenuation.
    RoomEffectsGain = 2,  // Room effects gain.
    Gain = 3,  // Gain.
    DirectivityAlpha = 4,  // Source directivity alpha.
    DirectivitySharpness = 5,  // Source directivity sharpness.
    ListenerDirectivityAlpha = 6,  // Listener directivity alpha.
    ListenerDirectivitySharpness = 7,  // Listener directivity sharpness.
    Occlusion = 8,  // Occlusion intensity.
    Quality = 9,  // Source audio rendering quality.
    NearFieldEffectGain = 10,  // Near field effect gain.
    Volume = 11,  // Volume.
  }

  // Current occlusion value;
  private float currentOcclusion = 0.0f;

  // Next occlusion update time in seconds.
  private float nextOcclusionUpdate = 0.0f;

#if UNITY_EDITOR
  // Directivity gizmo meshes.
  private Mesh directivityGizmoMesh = null;
  private Mesh listenerDirectivityGizmoMesh = null;

  // Directivity gizmo resolution.
  private const int gizmoResolution = 180;
#endif  // UNITY_EDITOR

  void Awake() {
    audioSource = GetComponent<AudioSource>();
  }

#if UNITY_EDITOR
  void OnEnable() {
#if UNITY_2017_2_OR_NEWER
    // Validate the spatializer plugin selection.
    if (AudioSettings.GetSpatializerPluginName() != ResonanceAudio.spatializerPluginName) {
      Debug.LogWarning(ResonanceAudio.spatializerPluginName + " must be selected as the " +
                       "Spatializer Plugin in Edit > Project Settings > Audio.");
    }
#endif  // UNITY_2017_2_OR_NEWER
    // Validate the source output mixer route.
    if (ResonanceAudio.MixerGroup == null ||
        audioSource.outputAudioMixerGroup != ResonanceAudio.MixerGroup) {
      Debug.LogWarning("Make sure AudioSource is routed to a mixer that ResonanceAudioRenderer " +
                       "is attached to.");
    }
  }
#endif  // UNITY_EDITOR

  void Update() {
    if (!occlusionEnabled) {
      currentOcclusion = 0.0f;
    } else if (Time.time >= nextOcclusionUpdate) {
      nextOcclusionUpdate = Time.time + ResonanceAudio.occlusionDetectionInterval;
      currentOcclusion = occlusionIntensity * ResonanceAudio.ComputeOcclusion(transform);
    }
    UpdateSource();
  }

  // Updates the source parameters.
  private void UpdateSource() {
    if (audioSource.clip != null && audioSource.clip.ambisonic) {
      // Use ambisonic decoder.
      audioSource.SetAmbisonicDecoderFloat(
        (int)EffectData.RoomEffectsGain,
        bypassRoomEffects ? 0.0f
                          : ResonanceAudioRoomManager.ComputeRoomEffectsGain(transform.position));
      audioSource.SetAmbisonicDecoderFloat((int) EffectData.Gain,
                                           ResonanceAudio.ConvertAmplitudeFromDb(gainDb));
#if !UNITY_2018_1_OR_NEWER
      audioSource.SetAmbisonicDecoderFloat((int) EffectData.Volume,
                                           audioSource.mute ? 0.0f : audioSource.volume);
#endif  // !UNITY_2018_1_OR_NEWER
    } else if (audioSource.spatialize) {
      // Use spatializer.
      audioSource.SetSpatializerFloat(
        (int)EffectData.RoomEffectsGain,
        bypassRoomEffects ? 0.0f
                          : ResonanceAudioRoomManager.ComputeRoomEffectsGain(transform.position));
      audioSource.SetSpatializerFloat((int) EffectData.Gain,
                                      ResonanceAudio.ConvertAmplitudeFromDb(gainDb));
      audioSource.SetSpatializerFloat((int) EffectData.DirectivityAlpha, directivityAlpha);
      audioSource.SetSpatializerFloat((int) EffectData.DirectivitySharpness, directivitySharpness);
      audioSource.SetSpatializerFloat((int) EffectData.ListenerDirectivityAlpha,
                                      listenerDirectivityAlpha);
      audioSource.SetSpatializerFloat((int) EffectData.ListenerDirectivitySharpness,
                                      listenerDirectivitySharpness);
      audioSource.SetSpatializerFloat((int) EffectData.Occlusion, currentOcclusion);
      audioSource.SetSpatializerFloat((int) EffectData.Quality, (float) quality);
      audioSource.SetSpatializerFloat((int) EffectData.NearFieldEffectGain,
                                      nearFieldEffectEnabled ? nearFieldEffectGain: 0.0f);
    }
  }

#if UNITY_EDITOR
  void OnDrawGizmosSelected() {
    // Draw listener directivity gizmo.
    if (ResonanceAudio.ListenerTransform != null) {
      Gizmos.color = ResonanceAudio.listenerDirectivityColor;
      if (listenerDirectivityGizmoMesh == null) {
        listenerDirectivityGizmoMesh = new Mesh();
        listenerDirectivityGizmoMesh.hideFlags = HideFlags.HideAndDontSave;
      }
      DrawDirectivityGizmo(ResonanceAudio.ListenerTransform, listenerDirectivityGizmoMesh,
                           listenerDirectivityAlpha, listenerDirectivitySharpness);
    }
    // Draw source directivity gizmo.
    Gizmos.color = ResonanceAudio.sourceDirectivityColor;
    if (directivityGizmoMesh == null) {
      directivityGizmoMesh = new Mesh();
      directivityGizmoMesh.hideFlags = HideFlags.HideAndDontSave;
    }
    DrawDirectivityGizmo(transform, directivityGizmoMesh, directivityAlpha, directivitySharpness);
  }

  // Draws a 3D gizmo in the Scene View that shows the selected directivity pattern.
  private void DrawDirectivityGizmo(Transform target, Mesh mesh, float alpha, float sharpness) {
    Vector2[] points = ResonanceAudio.Generate2dPolarPattern(alpha, sharpness, gizmoResolution);
    // Compute |vertices| from the polar pattern |points|.
    int numVertices = gizmoResolution + 1;
    Vector3[] vertices = new Vector3[numVertices];
    vertices[0] = Vector3.zero;
    for (int i = 0; i < points.Length; ++i) {
      vertices[i + 1] = new Vector3(points[i].x, 0.0f, points[i].y);
    }
    // Generate |triangles| from |vertices|. Two triangles per each sweep to avoid backface culling.
    int[] triangles = new int[6 * numVertices];
    for (int i = 0; i < numVertices - 1; ++i) {
      int index = 6 * i;
      if (i < numVertices - 2) {
        triangles[index] = 0;
        triangles[index + 1] = i + 1;
        triangles[index + 2] = i + 2;
      } else {
        // Last vertex is connected back to the first for the last triangle.
        triangles[index] = 0;
        triangles[index + 1] = numVertices - 1;
        triangles[index + 2] = 1;
      }
      // The second triangle facing the opposite direction.
      triangles[index + 3] = triangles[index];
      triangles[index + 4] = triangles[index + 2];
      triangles[index + 5] = triangles[index + 1];
    }
    // Construct a new mesh for the gizmo.
    mesh.vertices = vertices;
    mesh.triangles = triangles;
    mesh.RecalculateNormals();
    // Draw the mesh.
    Vector3 scale = 2.0f * Mathf.Max(target.lossyScale.x, target.lossyScale.z) * Vector3.one;
    Gizmos.DrawMesh(mesh, target.position, target.rotation, scale);
  }
#endif  // UNITY_EDITOR
}
