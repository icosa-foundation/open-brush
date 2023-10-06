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
using System.Collections.Generic;

/// Resonance Audio listener component that enhances AudioListener to provide advanced spatial audio
/// features.
///
/// There should be only one instance of this which is attached to the AudioListener's game object.
[AddComponentMenu("ResonanceAudio/ResonanceAudioListener")]
[RequireComponent(typeof(AudioListener))]
[ExecuteInEditMode]
public class ResonanceAudioListener : MonoBehaviour {
  /// Global gain in decibels to be applied to the processed output.
  [Tooltip("Sets the global gain for all spatialized audio sources. Can be used to adjust the " +
           "overall output volume.")]
  public float globalGainDb = 0.0f;

  /// Global layer mask to be used in occlusion detection.
  [Tooltip("Sets the global layer mask for occlusion detection.")]
  public LayerMask occlusionMask = -1;

  /// Stereo speaker mode toggle.
  [Tooltip("Disables HRTF-based rendering and force stereo-panning only rendering for all " +
           "spatialized audio sources. This mode is recommended only when the audio output is " +
           "routed to a stereo loudspeaker configuration.")]
  public bool stereoSpeakerModeEnabled = false;

  /// Denotes whether the soundfield should be recorded in a seamless loop.
  [Tooltip("Sets whether the recorded soundfield clip should be saved as a seamless loop.")]
  public bool recorderSeamless = false;

  /// Target tag for spatial audio sources to be recorded into soundfield.
  [Tooltip("Specify by tag which spatialized audio sources will be recorded. Choose " +
           "\"Untagged\" to include all enabled spatialized audio sources in the scene.")]
  public string recorderSourceTag = "Untagged";

  /// Is currently recording soundfield?
  public bool IsRecording { get; private set; }

#pragma warning disable 0414  // private variable assigned but is never used.
  // Denotes whether the soundfield recorder foldout should be expanded.
  [SerializeField]
  private bool recorderFoldout = false;
#pragma warning restore 0414

  // List of target spatial audio sources to be recorded.
  private List<AudioSource> recorderTaggedSources = null;

  // Record start time in seconds.
  private double recorderStartTime = 0.0;

  void OnEnable() {
    if (Application.isEditor && !Application.isPlaying) {
      IsRecording = false;
      recorderStartTime = 0.0;
      recorderTaggedSources = new List<AudioSource>();
    }
  }

  void OnDisable() {
    if (Application.isEditor && IsRecording) {
      // Force stop soundfield recorder.
      StopSoundfieldRecorder(null);
      Debug.LogWarning("Soundfield recording is stopped.");
    }
  }

  void Update() {
    if (Application.isEditor && !Application.isPlaying && !IsRecording) {
      // Update soundfield recorder properties.
      UpdateTaggedSources();
    } else {
      // Update global properties.
      ResonanceAudio.UpdateAudioListener(this);
    }
  }

  /// Returns the current record duration in seconds.
  public double GetCurrentRecordDuration() {
    if (IsRecording) {
      double currentTime = AudioSettings.dspTime;
      return currentTime - recorderStartTime;
    }
    return 0.0;
  }

  /// Starts soundfield recording.
  public void StartSoundfieldRecorder() {
    if (!(Application.isEditor && !Application.isPlaying)) {
      Debug.LogError("Soundfield recording is only supported in Unity Editor \"Edit Mode\".");
      return;
    }

    if (IsRecording) {
      Debug.LogWarning("Soundfield recording is already in progress.");
      return;
    }

    recorderStartTime = AudioSettings.dspTime;
    for (int i = 0; i < recorderTaggedSources.Count; ++i) {
      if (recorderTaggedSources[i].playOnAwake) {
        recorderTaggedSources[i].PlayScheduled(recorderStartTime);
      }
    }
    IsRecording = ResonanceAudio.StartRecording();
    if (!IsRecording) {
      Debug.LogError("Failed to start soundfield recording.");
      IsRecording = false;
      for (int i = 0; i < recorderTaggedSources.Count; ++i) {
        recorderTaggedSources[i].Stop();
      }
    }
  }

  /// Stops soundfield recording and saves the recorded data into target file path.
  public void StopSoundfieldRecorder(string filePath) {
    if (!(Application.isEditor && !Application.isPlaying)) {
      Debug.LogError("Soundfield recording is only supported in Unity Editor \"Edit Mode\".");
      return;
    }

    if (!IsRecording) {
      Debug.LogWarning("No recorded soundfield was found.");
      return;
    }

    IsRecording = false;
    recorderStartTime = 0.0;
    if (!ResonanceAudio.StopRecordingAndSaveToFile(filePath, recorderSeamless)) {
      Debug.LogError("Failed to save soundfield recording into file.");
    }
    for (int i = 0; i < recorderTaggedSources.Count; ++i) {
      recorderTaggedSources[i].Stop();
    }
  }

  // Updates the list of the target spatial audio sources to be recorded.
  private void UpdateTaggedSources() {
    recorderTaggedSources.Clear();
    var sources = GameObject.FindObjectsOfType<AudioSource>();
    for (int i = 0; i < sources.Length; ++i) {
      // Untagged is treated as *all* spatial audio sources in the scene.
      if ((recorderSourceTag == "Untagged" || sources[i].tag == recorderSourceTag) &&
          sources[i].enabled && sources[i].spatialize) {
        recorderTaggedSources.Add(sources[i]);
      }
    }
  }
}
