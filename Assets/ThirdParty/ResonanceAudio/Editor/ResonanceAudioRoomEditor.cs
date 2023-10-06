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

/// A custom editor for properties on the ResonanceAudioRoom script. This appears in the Inspector
/// window of a ResonanceAudioRoom object.
[CustomEditor(typeof(ResonanceAudioRoom))]
[CanEditMultipleObjects]
public class ResonanceAudioRoomEditor : Editor {
  private SerializedProperty leftWall = null;
  private SerializedProperty rightWall = null;
  private SerializedProperty floor = null;
  private SerializedProperty ceiling = null;
  private SerializedProperty backWall = null;
  private SerializedProperty frontWall = null;
  private SerializedProperty reflectivity = null;
  private SerializedProperty reverbGainDb = null;
  private SerializedProperty reverbBrightness = null;
  private SerializedProperty reverbTime = null;
  private SerializedProperty size = null;

  private GUIContent surfaceMaterialsLabel = new GUIContent("Surface Materials",
      "Room surface materials to calculate the acoustic properties of the room.");
  private GUIContent reverbPropertiesLabel = new GUIContent("Reverb Properties",
      "Parameters to adjust the reverb properties of the room.");
  private GUIContent reverbGainDbLabel = new GUIContent("Gain (dB)");
  private GUIContent reverbBrightnessLabel = new GUIContent("Brightness");
  private GUIContent reverbTimeLabel = new GUIContent("Time");

  void OnEnable() {
    leftWall = serializedObject.FindProperty("leftWall");
    rightWall = serializedObject.FindProperty("rightWall");
    floor = serializedObject.FindProperty("floor");
    ceiling = serializedObject.FindProperty("ceiling");
    backWall = serializedObject.FindProperty("backWall");
    frontWall = serializedObject.FindProperty("frontWall");
    reflectivity = serializedObject.FindProperty("reflectivity");
    reverbGainDb = serializedObject.FindProperty("reverbGainDb");
    reverbBrightness = serializedObject.FindProperty("reverbBrightness");
    reverbTime = serializedObject.FindProperty("reverbTime");
    size = serializedObject.FindProperty("size");
  }

  /// @cond
  public override void OnInspectorGUI() {
    serializedObject.Update();

    // Add clickable script field, as would have been provided by DrawDefaultInspector()
    MonoScript script = MonoScript.FromMonoBehaviour(target as MonoBehaviour);
    EditorGUI.BeginDisabledGroup(true);
    EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
    EditorGUI.EndDisabledGroup();

    EditorGUILayout.LabelField(surfaceMaterialsLabel);
    ++EditorGUI.indentLevel;
    EditorGUILayout.PropertyField(leftWall);
    EditorGUILayout.PropertyField(rightWall);
    EditorGUILayout.PropertyField(floor);
    EditorGUILayout.PropertyField(ceiling);
    EditorGUILayout.PropertyField(backWall);
    EditorGUILayout.PropertyField(frontWall);
    --EditorGUI.indentLevel;

    EditorGUILayout.Separator();

    EditorGUILayout.Slider(reflectivity, 0.0f, ResonanceAudio.maxReflectivity);

    EditorGUILayout.Separator();

    EditorGUILayout.LabelField(reverbPropertiesLabel);
    ++EditorGUI.indentLevel;
    EditorGUILayout.Slider(reverbGainDb, ResonanceAudio.minGainDb, ResonanceAudio.maxGainDb,
                           reverbGainDbLabel);
    EditorGUILayout.Slider(reverbBrightness, ResonanceAudio.minReverbBrightness,
                           ResonanceAudio.maxReverbBrightness, reverbBrightnessLabel);
    EditorGUILayout.Slider(reverbTime, 0.0f, ResonanceAudio.maxReverbTime, reverbTimeLabel);
    --EditorGUI.indentLevel;

    EditorGUILayout.Separator();

    EditorGUILayout.PropertyField(size);

    serializedObject.ApplyModifiedProperties();
  }
  /// @endcond
}
