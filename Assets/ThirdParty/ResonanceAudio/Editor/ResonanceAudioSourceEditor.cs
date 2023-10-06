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

/// A custom editor for properties on the ResonanceAudioSource script. This appears in the Inspector
/// window of a ResonanceAudioSource object.
[CustomEditor(typeof(ResonanceAudioSource))]
[CanEditMultipleObjects]
public class ResonanceAudioSourceEditor : Editor {
  private SerializedProperty bypassRoomEffects = null;
  private SerializedProperty directivityAlpha = null;
  private SerializedProperty directivitySharpness = null;
  private SerializedProperty listenerDirectivityAlpha = null;
  private SerializedProperty listenerDirectivitySharpness = null;
  private Texture2D directivityTexture = null;
  private SerializedProperty gainDb = null;
  private SerializedProperty nearFieldEffectEnabled = null;
  private SerializedProperty nearFieldEffectGain = null;
  private SerializedProperty occlusionEnabled = null;
  private SerializedProperty occlusionIntensity = null;
  private SerializedProperty quality = null;

  private GUIContent directivityAlphaLabel = new GUIContent("Alpha");
  private GUIContent directivitySharpnessLabel = new GUIContent("Sharpness");
  private GUIContent listenerDirectivityLabel = new GUIContent("Listener Directivity",
      "Controls the pattern of sound sensitivity of the listener for the source. This can " +
      "change the perceived loudness of the source depending on which way the listener is facing " +
      "relative to the source. Patterns are aligned to the 'forward' direction of the listener.");
  private GUIContent sourceDirectivityLabel = new GUIContent("Source Directivity",
      "Controls the pattern of sound emission of the source. This can change the perceived " +
      "loudness of the source depending on which way it is facing relative to the listener. " +
      "Patterns are aligned to the 'forward' direction of the parent object.");
  private GUIContent gainDbLabel = new GUIContent("Gain (dB)");
  private GUIContent nearFieldEffectGainLabel = new GUIContent("Gain");
  private GUIContent nearFieldEffectEnabledLabel = new GUIContent("Enable Near-Field Effect");
  private GUIContent occlusionEnabledLabel = new GUIContent("Enable Occlusion");
  private GUIContent occlusionIntensityLabel = new GUIContent("Intensity");

  // Target source instance.
  private ResonanceAudioSource source = null;

  void OnEnable() {
    bypassRoomEffects = serializedObject.FindProperty("bypassRoomEffects");
    directivityAlpha = serializedObject.FindProperty("directivityAlpha");
    directivitySharpness = serializedObject.FindProperty("directivitySharpness");
    listenerDirectivityAlpha = serializedObject.FindProperty("listenerDirectivityAlpha");
    listenerDirectivitySharpness = serializedObject.FindProperty("listenerDirectivitySharpness");
    directivityTexture = Texture2D.blackTexture;
    gainDb = serializedObject.FindProperty("gainDb");
    nearFieldEffectEnabled = serializedObject.FindProperty("nearFieldEffectEnabled");
    nearFieldEffectGain = serializedObject.FindProperty("nearFieldEffectGain");
    occlusionEnabled = serializedObject.FindProperty("occlusionEnabled");
    occlusionIntensity = serializedObject.FindProperty("occlusionIntensity");
    quality = serializedObject.FindProperty("quality");
    source = (ResonanceAudioSource) target;
  }

  /// @cond
  public override void OnInspectorGUI() {
    serializedObject.Update();

    // Add clickable script field, as would have been provided by DrawDefaultInspector()
    MonoScript script = MonoScript.FromMonoBehaviour(target as MonoBehaviour);
    EditorGUI.BeginDisabledGroup(true);
    EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
    EditorGUI.EndDisabledGroup();

    EditorGUILayout.PropertyField(bypassRoomEffects);

    EditorGUILayout.Separator();

    EditorGUILayout.Slider(gainDb, ResonanceAudio.minGainDb, ResonanceAudio.maxGainDb, gainDbLabel);

    EditorGUILayout.Separator();

    // Spatializer only properties, does not apply to ambisonic decoder.
    EditorGUI.BeginDisabledGroup(source.audioSource != null && source.audioSource.clip != null &&
                                 source.audioSource.clip.ambisonic);
    // Draw the listener directivity properties.
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.BeginVertical();
    GUILayout.Label(listenerDirectivityLabel);
    ++EditorGUI.indentLevel;
    EditorGUILayout.PropertyField(listenerDirectivityAlpha, directivityAlphaLabel);
    EditorGUILayout.PropertyField(listenerDirectivitySharpness, directivitySharpnessLabel);
    --EditorGUI.indentLevel;
    EditorGUILayout.EndVertical();
    DrawDirectivityPattern(listenerDirectivityAlpha.floatValue,
                           listenerDirectivitySharpness.floatValue,
                           ResonanceAudio.listenerDirectivityColor,
                           (int) (3.0f * EditorGUIUtility.singleLineHeight));
    EditorGUILayout.EndHorizontal();
    // Draw the source directivity properties.
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.BeginVertical();
    GUILayout.Label(sourceDirectivityLabel);
    ++EditorGUI.indentLevel;
    EditorGUILayout.PropertyField(directivityAlpha, directivityAlphaLabel);
    EditorGUILayout.PropertyField(directivitySharpness, directivitySharpnessLabel);
    --EditorGUI.indentLevel;
    EditorGUILayout.EndVertical();
    DrawDirectivityPattern(directivityAlpha.floatValue, directivitySharpness.floatValue,
                           ResonanceAudio.sourceDirectivityColor,
                           (int) (3.0f * EditorGUIUtility.singleLineHeight));
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.PropertyField(occlusionEnabled, occlusionEnabledLabel);
    EditorGUI.BeginDisabledGroup(!occlusionEnabled.boolValue);
    ++EditorGUI.indentLevel;
    EditorGUILayout.PropertyField(occlusionIntensity, occlusionIntensityLabel);
    --EditorGUI.indentLevel;
    EditorGUI.EndDisabledGroup();

    EditorGUILayout.Separator();

    EditorGUILayout.PropertyField(nearFieldEffectEnabled, nearFieldEffectEnabledLabel);
    EditorGUI.BeginDisabledGroup(!nearFieldEffectEnabled.boolValue);
    ++EditorGUI.indentLevel;
    EditorGUILayout.PropertyField(nearFieldEffectGain, nearFieldEffectGainLabel);
    --EditorGUI.indentLevel;
    EditorGUI.EndDisabledGroup();

    EditorGUILayout.Separator();

    EditorGUILayout.PropertyField(quality);
    EditorGUI.EndDisabledGroup();

    serializedObject.ApplyModifiedProperties();
  }
  /// @endcond

  private void DrawDirectivityPattern(float alpha, float sharpness, Color color, int size) {
    directivityTexture.Resize(size, size);
    // Draw the axes.
    Color axisColor = color.a * Color.black;
    for (int i = 0; i < size; ++i) {
      directivityTexture.SetPixel(i, size / 2, axisColor);
      directivityTexture.SetPixel(size / 2, i, axisColor);
    }
    // Draw the 2D polar directivity pattern.
    float offset = 0.5f * size;
    float cardioidSize = 0.45f * size;
    Vector2[] vertices = ResonanceAudio.Generate2dPolarPattern(alpha, sharpness, 180);
    for (int i = 0; i < vertices.Length; ++i) {
      directivityTexture.SetPixel((int) (offset + cardioidSize * vertices[i].x),
                                  (int) (offset + cardioidSize * vertices[i].y), color);
    }
    directivityTexture.Apply();
    // Show the texture.
    GUILayout.Box(directivityTexture);
  }
}
