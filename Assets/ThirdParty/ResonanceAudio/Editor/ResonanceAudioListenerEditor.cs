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
using UnityEditor;
using System.Collections;
using System.IO;

/// A custom editor for properties on the ResonanceAudioListener script. This appears in the
/// Inspector window of a ResonanceAudioListener object.
[CustomEditor(typeof(ResonanceAudioListener))]
public class ResonanceAudioListenerEditor : Editor {
  private SerializedProperty globalGainDb = null;
  private SerializedProperty occlusionMask = null;
  private SerializedProperty stereoSpeakerModeEnabled = null;
  private SerializedProperty recorderFoldout = null;
  private SerializedProperty recorderSeamless = null;
  private SerializedProperty recorderSourceTag = null;

  private GUIContent globalGainDbLabel = new GUIContent("Global Gain (dB)");
  private GUIContent stereoSpeakerModeEnabledLabel = new GUIContent("Enable Stereo Speaker Mode");
  private GUIContent recorderLabel = new GUIContent("Soundfield Recorder",
     "Soundfield recorder allows pre-baking spatial audio sources into first-order ambisonic " +
     "soundfield assets to be played back at run time.");
  private GUIContent recorderSeamlessLabel = new GUIContent("Seamless Loop");
  private GUIContent recorderSourceTagLabel = new GUIContent("Source Tag");

  // Target listener instance.
  private ResonanceAudioListener listener = null;

  void OnEnable() {
    globalGainDb = serializedObject.FindProperty("globalGainDb");
    occlusionMask = serializedObject.FindProperty("occlusionMask");
    stereoSpeakerModeEnabled = serializedObject.FindProperty("stereoSpeakerModeEnabled");
    recorderFoldout = serializedObject.FindProperty("recorderFoldout");
    recorderSeamless = serializedObject.FindProperty("recorderSeamless");
    recorderSourceTag = serializedObject.FindProperty("recorderSourceTag");
    listener = (ResonanceAudioListener) target;
  }

  /// @cond
  public override void OnInspectorGUI() {
    serializedObject.Update();

    // Add clickable script field, as would have been provided by DrawDefaultInspector()
    MonoScript script = MonoScript.FromMonoBehaviour(target as MonoBehaviour);
    EditorGUI.BeginDisabledGroup(true);
    EditorGUILayout.ObjectField ("Script", script, typeof(MonoScript), false);
    EditorGUI.EndDisabledGroup();

    EditorGUILayout.Separator();

    EditorGUILayout.Slider(globalGainDb, ResonanceAudio.minGainDb, ResonanceAudio.maxGainDb,
                           globalGainDbLabel);

    EditorGUILayout.Separator();

    EditorGUILayout.PropertyField(occlusionMask);

    EditorGUILayout.Separator();

    EditorGUILayout.PropertyField(stereoSpeakerModeEnabled, stereoSpeakerModeEnabledLabel);

    EditorGUILayout.Separator();

    // Draw soundfield recorder properties.
    recorderFoldout.boolValue = EditorGUILayout.Foldout(recorderFoldout.boolValue, recorderLabel);
    if (recorderFoldout.boolValue) {
      ++EditorGUI.indentLevel;
      EditorGUI.BeginDisabledGroup(listener.IsRecording || Application.isPlaying);
      recorderSourceTag.stringValue = EditorGUILayout.TagField(recorderSourceTagLabel,
                                                             recorderSourceTag.stringValue);

      EditorGUILayout.Separator();

      EditorGUILayout.PropertyField(recorderSeamless, recorderSeamlessLabel);
      EditorGUI.EndDisabledGroup();

      EditorGUILayout.Separator();

      // Recording is allowed in Edit Mode only.
      EditorGUI.BeginDisabledGroup(Application.isPlaying);
      EditorGUILayout.BeginHorizontal();
      GUILayout.Space(15 * EditorGUI.indentLevel);
      EditorGUILayout.BeginVertical();
      if (listener.IsRecording) {
        if (GUILayout.Button("Stop")) {
          StopRecording();
        }
        --EditorGUI.indentLevel;
        EditorGUILayout.HelpBox("Recording in progress: " +
                                listener.GetCurrentRecordDuration().ToString("F1") + " seconds.",
                                MessageType.Info);
        ++EditorGUI.indentLevel;
        Repaint();
      } else if (GUILayout.Button("Record")) {
        listener.StartSoundfieldRecorder();
      }
      EditorGUILayout.EndVertical();
      EditorGUILayout.EndHorizontal();
      EditorGUI.EndDisabledGroup();
      if (Application.isPlaying) {
        EditorGUILayout.HelpBox("Soundfield recording is only allowed in Edit Mode.",
                                MessageType.Warning);
      }
      --EditorGUI.indentLevel;
    }

    serializedObject.ApplyModifiedProperties();
  }
  /// @endcond

  // Stops soundfield recording.
  private void StopRecording() {
    // Save recorded soundfield clips into a temporary folder.
    string tempFolderPath = FileUtil.GetUniqueTempPathInProject();
    if (!Directory.Exists(tempFolderPath)) {
      Directory.CreateDirectory(tempFolderPath);
    }
    string tempFileName = Path.ChangeExtension(listener.name, "ogg");
    string tempFilePath = Path.Combine(tempFolderPath, tempFileName);
    listener.StopSoundfieldRecorder(tempFilePath);

    // Copy the recorded file as an ambisonic audio clip into project assets.
    string relativeClipPath = EditorUtility.SaveFilePanelInProject("Save Soundfield", listener.name,
                                                                   "ogg", null);
    if (relativeClipPath.Length > 0 && File.Exists(tempFilePath)) {
      string projectFolderPath =
          Application.dataPath.Substring(0, Application.dataPath.IndexOf("Assets"));
      string targetFilePath = Path.Combine(projectFolderPath, relativeClipPath);
      FileUtil.ReplaceFile(tempFilePath, targetFilePath);
      AssetDatabase.Refresh();

      AudioImporter importer = (AudioImporter) AssetImporter.GetAtPath(relativeClipPath);
      importer.ambisonic = true;
      AssetDatabase.Refresh();
    }

    // Cleanup temporary files.
    if (Directory.Exists(tempFolderPath)) {
      Directory.Delete(tempFolderPath, true);
    }
  }
}
