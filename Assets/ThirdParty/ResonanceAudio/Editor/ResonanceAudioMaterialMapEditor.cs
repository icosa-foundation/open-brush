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

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// A custom editor for properties on the ResonanceAudioMaterialMap script. This appears in the
/// Inspector window of a ResonanceAudioMaterialMap object.
[CustomEditor(typeof(ResonanceAudioMaterialMap))]
public class ResonanceAudioMaterialMapEditor : Editor {
  private SerializedProperty materialMappingGuids = null;
  private SerializedProperty materialMappingSurfaceMaterials = null;

  private GUIContent clearAllMappingLabel = new GUIContent("Reset All",
      "Resets the material mapping selections to default.");

  // The thumbnail previews of the surface materials in the inspector window, shown as solid color
  // patches.
  private Texture2D[] surfaceMaterialPreviews = null;

  // Stored asset previews and names corresponding to GUIDS. Generated once when the Editor is
  // enabled.
  private Dictionary<string, Texture2D> assetPreviewFromGuid = null;
  private Dictionary<string, string> guidNameFromGuid = null;

  // Since asset previews for Unity Materials are loaded asynchronously, we keep a dictionary of
  // those whose previews are not ready yet, and show a default icon for them.
  private Dictionary<string, Material> materialWaitingForPreviewFromGuid = null;
  private Texture2D cachedMaterialIcon = null;

  private const int guidLabelWidth = 150;
  private const int materialRowMargin = 5;
  private const int materialPreviewSize = 50;
  private GUILayoutOption previewHeight = null;
  private GUILayoutOption previewWidth = null;
  private GUIStyle materialRowStyle = null;

  // The scroll position of the material mapping UI.
  private Vector2 scrollPosition = Vector2.zero;

  void OnEnable() {
    InitializeProperties();
    InitializeGuiParameters();
    InitializeSurfaceMaterialPreviews();
    materialWaitingForPreviewFromGuid = new Dictionary<string, Material>();
    assetPreviewFromGuid = new Dictionary<string, Texture2D>();
    guidNameFromGuid = new Dictionary<string, string>();
  }

  public override void OnInspectorGUI() {
    serializedObject.Update();

    DrawMaterialMappingGUI();

    serializedObject.ApplyModifiedProperties();
  }

  // Initializes the material mapper and loads properties to be displayed in this window.
  private void InitializeProperties() {
    var surfaceMaterialFromGuid = serializedObject.FindProperty("surfaceMaterialFromGuid");
    materialMappingGuids = surfaceMaterialFromGuid.FindPropertyRelative("guids");
    materialMappingSurfaceMaterials =
        surfaceMaterialFromGuid.FindPropertyRelative("surfaceMaterials");
  }

  // Initializes various GUI parameters.
  private void InitializeGuiParameters() {
    previewHeight = GUILayout.Height((float) materialPreviewSize);
    previewWidth = GUILayout.Width((float) materialPreviewSize);
    materialRowStyle = new GUIStyle();
    materialRowStyle.margin = new RectOffset(materialRowMargin, materialRowMargin,
                                             materialRowMargin, materialRowMargin);
  }

  // Initializes the thumbnail previews used in the material picking UI. Each surface material
  // is shown as a square filled with solid color.
  private void InitializeSurfaceMaterialPreviews() {
    int numSurfaceMaterials = ResonanceAudioMaterialMap.surfaceMaterialColors.Length;
    surfaceMaterialPreviews = new Texture2D[numSurfaceMaterials];
    for (int surfaceMaterialIndex = 0; surfaceMaterialIndex < numSurfaceMaterials;
         ++surfaceMaterialIndex) {
      var color = ResonanceAudioMaterialMap.surfaceMaterialColors[surfaceMaterialIndex];
      Texture2D surfaceMaterialPreview = new Texture2D(materialPreviewSize, materialPreviewSize);
      var pixelArraySize = surfaceMaterialPreview.GetPixels().Length;
      Color[] pixelArray = new Color[pixelArraySize];
      for (int pixelArrayIndex = 0; pixelArrayIndex < pixelArraySize; ++pixelArrayIndex) {
        pixelArray[pixelArrayIndex] = color;
      }
      surfaceMaterialPreview.SetPixels(pixelArray);
      surfaceMaterialPreview.Apply();
      surfaceMaterialPreviews[surfaceMaterialIndex] = surfaceMaterialPreview;
    }
  }

  // Draws the material mapping GUI. The GUI is organized as rows, each row having a GUID (a Unity
  // Material or a TerrainData) on the left, and the mapped surface materials on the right.
  private void DrawMaterialMappingGUI() {
    // Show the material mapping as rows.
    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(false));

    for (int i = 0; i < materialMappingGuids.arraySize; ++i) {
      string guid = materialMappingGuids.GetArrayElementAtIndex(i).stringValue;
      Texture2D assetPreview = null;
      string guidName = null;

      // Use the stored asset preview and GUID name or try to load them.
      if((!assetPreviewFromGuid.TryGetValue(guid, out assetPreview) ||
          !guidNameFromGuid.TryGetValue(guid, out guidName)) &&
         !LoadAssetPreviewsAndGuidName(guid, out assetPreview, out guidName)) {
        // Skip this row if the asset preview and GUID name are not stored and cannot be loaded.
        continue;
      }

      // If the asset preview for the material has not been loaded, attempt to load it again.
      Material materialWaitingForPreview = null;
      if (materialWaitingForPreviewFromGuid.TryGetValue(guid, out materialWaitingForPreview)) {
        assetPreview = GetClonedPreviewOrDefaultIconForMaterial(guid, materialWaitingForPreview);
        assetPreviewFromGuid[guid] = assetPreview;
      }

      EditorGUILayout.BeginHorizontal(materialRowStyle);
      GUILayout.Space(15 * EditorGUI.indentLevel);
      DrawGuidColumn(assetPreview, guidName);
      DrawSurfaceMaterialColumn(materialMappingSurfaceMaterials.GetArrayElementAtIndex(i));
      EditorGUILayout.EndHorizontal();
    }
    EditorGUILayout.EndScrollView();

    EditorGUILayout.Separator();

    DrawClearAllButton();
  }

  // Draws the GUID column: the thumbnail preview first, followed by the name.
  private void DrawGuidColumn(Texture2D assetPreview, string guidName) {
    // Draw the preview.
    GUILayout.Box(assetPreview, GUIStyle.none, previewHeight, previewWidth);

    // Display the name.
    if (guidName != null) {
      EditorGUILayout.LabelField(guidName, GUILayout.Width(guidLabelWidth));
    }
  }

  // Draws the surface material column: the thumbnail preview first, followed by a drop-down menu
  // to let users choose the mapped material.
  private void DrawSurfaceMaterialColumn(SerializedProperty surfaceMaterialProperty) {
    // Draw the preview.
    var preview = surfaceMaterialPreviews[surfaceMaterialProperty.enumValueIndex];
    GUILayout.Box(preview, GUIStyle.none, previewHeight, previewWidth);

    // Draw the drop-down menu.
    EditorGUILayout.PropertyField(surfaceMaterialProperty, GUIContent.none);
  }

  // Draws the "Clear All" button and clears the material mapping (by clearing the underlying
  // serialized lists).
  private void DrawClearAllButton() {
    EditorGUILayout.BeginHorizontal();
    GUILayout.Space(15 * EditorGUI.indentLevel);
    if (GUILayout.Button(clearAllMappingLabel)) {
      materialMappingGuids.ClearArray();
      materialMappingSurfaceMaterials.ClearArray();
    }
    EditorGUILayout.EndHorizontal();
  }

  // Initializes the asset preview and name for a GUID to be shown in the UI, depending on
  // whether the GUID identifies a Unity Material or a TerrainData.
  private bool LoadAssetPreviewsAndGuidName(string guid, out Texture2D assetPreview,
                                            out string guidName) {
    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
    Material unityMaterial = (Material) AssetDatabase.LoadAssetAtPath(assetPath, typeof(Material));
    if (unityMaterial != null) {
      assetPreview = GetClonedPreviewOrDefaultIconForMaterial(guid, unityMaterial);
      guidName = unityMaterial.name;
      assetPreviewFromGuid.Add(guid, assetPreview);
      guidNameFromGuid.Add(guid, guidName);
      return true;
    }

    TerrainData terrainData = (TerrainData) AssetDatabase.LoadAssetAtPath(assetPath,
                                                                          typeof(TerrainData));
    if (terrainData != null) {
      assetPreview = AssetPreview.GetMiniThumbnail(terrainData);
      guidName = terrainData.name;
      assetPreviewFromGuid.Add(guid, assetPreview);
      guidNameFromGuid.Add(guid, guidName);
      return true;
    }

    // Neither a Unity Material nor a TerrainData can be loaded from the GUID (perhaps the asset
    // is removed from the project).
    assetPreview = null;
    guidName = null;
    return false;
  }

  // Clones the asset preview for a Unity Material and returns it. If the asset preview is not
  // loaded yet, records the Unity Material and returs a default icon.
  private Texture2D GetClonedPreviewOrDefaultIconForMaterial(string guid, Material unityMaterial) {
    Texture2D assetPreview = null;
    var assetPreviewReference = AssetPreview.GetAssetPreview(unityMaterial);
    if (assetPreviewReference != null) {
      assetPreview = Instantiate<Texture2D>(assetPreviewReference);
      materialWaitingForPreviewFromGuid.Remove(guid);
    } else {
      if (cachedMaterialIcon == null) {
        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
        cachedMaterialIcon = (Texture2D) AssetDatabase.GetCachedIcon(assetPath);
      }
      assetPreview = cachedMaterialIcon;
      materialWaitingForPreviewFromGuid[guid] = unityMaterial;
    }
    return assetPreview;
  }
}
