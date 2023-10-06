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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

/// An editor window that provides UI for reverb baking related tasks:
/// 1. Select reverb probes and bake reverb to them.
/// 2. Modify the material mappings.
public class ResonanceAudioReverbBakingWindow : EditorWindow {
  private SerializedProperty reverbLayerMask = null;
  private SerializedProperty includeNonStaticGameObjects = null;
  private SerializedProperty materialMap = null;

  private GUIContent materialMapLabel = new GUIContent("Material Map",
      "ResonanceAudioMaterialMap asset to use.");
  private GUIContent reverbLayerMaskLabel = new GUIContent("Reverb Mask",
      "Which layers of game objects are included in reverb computation.");
  private GUIContent nonStaticGameObjectLabel = new GUIContent("Include Non-Static Game Objects",
      "Should non-static game objects be included in reverb computation?");
  private GUIContent visualizeModeLabel = new GUIContent("Visualize Mode",
      "Toggle to visualize the material mapping in the Scene View.");
  private GUIContent selectReverbProbesLabel = new GUIContent("Select Reverb Probes",
      "Reverb probe selections for baking.");
  private GUIContent selectAllProbesLabel = new GUIContent("Select All",
      "Selects all reverb probes.");
  private GUIContent clearAllProbesLabel = new GUIContent("Clear",
      "Clears reverb probe selections.");
  private GUIContent bakeLabel = new GUIContent("Bake", "Bake reverb to selected reverb probes.");

  // Whether to visualize the material mapping.
  private bool isInVisualizeMode = false;

  // The material mapper instance.
  private ResonanceAudioMaterialMapper materialMapper = null;

  // The material mapper updater instance.
  private ResonanceAudioMaterialMapperUpdater materialMapperUpdater = null;

  // The serialized object of the material mapper.
  private SerializedObject serializedMaterialMapper = null;

  // Whether the scene view needs to be redrawn. True when some things are changed (e.g. material
  // mappings changed or objects moved).
  private bool redraw = false;

  // The set of scene views whose shaders have been updated. This is used to make sure that each
  // scene view is at least updated once after OnEnable() (during OnEnable() the scene views might
  // not be available yet).
  private HashSet <int> updatedSceneViews = null;

  // Shader to visualize surface materials.
  private Shader surfaceMaterialShader = null;

  // This is to accomodate the long |nonStaticGameObjectLabel|.
  private const float propertyLabelWidth = 184.0f;

  // The scroll position of the reverb probe selection UI.
  private Vector2 probeSelectionScrollPosition = Vector2.zero;

  // The foldout of the reverb probe selection.
  private bool showReverbProbeSelection = true;

  // The path to the material mapper asset.
  private const string materialMapperAssetPath =
      "Assets/ResonanceAudio/Resources/ResonanceAudioMaterialMapper.asset";

  [MenuItem("ResonanceAudio/Reverb Baking")]
  private static void Initialize() {
    ResonanceAudioReverbBakingWindow window =
        EditorWindow.GetWindow<ResonanceAudioReverbBakingWindow>();
    window.Show();
  }

  void OnEnable() {
    updatedSceneViews = new HashSet<int>();

    InitializeColorArrayInShader();
    InitializeSurfaceMaterialShader();

    LoadOrCreateMaterialMapper();
    LoadOrCreateMaterialMapperUpdater();

    isInVisualizeMode = false;

    EditorSceneManager.sceneOpened += (Scene scene, OpenSceneMode mode) => OnSceneOrModeSwitch();
    EditorSceneManager.sceneClosed += (Scene scene) => OnSceneOrModeSwitch();
#if UNITY_2017_2_OR_NEWER
    EditorApplication.playModeStateChanged += (PlayModeStateChange state) => OnSceneOrModeSwitch();
#else
    EditorApplication.playmodeStateChanged = OnSceneOrModeSwitch;
#endif  // UNITY_2017_2_OR_NEWER
    SceneView.onSceneGUIDelegate += OnSceneGUI;
  }

  void OnDisable() {
    EditorSceneManager.sceneOpened -= (Scene scene, OpenSceneMode mode) => OnSceneOrModeSwitch();
    EditorSceneManager.sceneClosed -= (Scene scene) => OnSceneOrModeSwitch();
#if UNITY_2017_2_OR_NEWER
    EditorApplication.playModeStateChanged -= (PlayModeStateChange state) => OnSceneOrModeSwitch();
#else
    EditorApplication.playmodeStateChanged = null;
#endif  // UNITY_2017_2_OR_NEWER
    SceneView.onSceneGUIDelegate -= OnSceneGUI;

    // Destroy the material mapper updater if not null.
    if (!EditorApplication.isPlaying && materialMapperUpdater != null) {
      DestroyImmediate(materialMapperUpdater.gameObject);
    }

    if (isInVisualizeMode) {
      isInVisualizeMode = false;
      RefreshMaterialMapper();
      UpdateShader();
    }
  }

  /// @cond
  void OnGUI() {
    serializedMaterialMapper.Update();

    EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
    var savedWidth = EditorGUIUtility.labelWidth;
    EditorGUIUtility.labelWidth = propertyLabelWidth;
    DrawMaterialMapSelection();

    EditorGUILayout.Separator();

    EditorGUI.BeginDisabledGroup(materialMap.objectReferenceValue == null);
    DrawObjectFiltering();

    EditorGUILayout.Separator();

    DrawVisualizeModeCheckbox();
    EditorGUIUtility.labelWidth = savedWidth;

    EditorGUILayout.Separator();

    showReverbProbeSelection = EditorGUILayout.Foldout(showReverbProbeSelection,
                                                       selectReverbProbesLabel);
    if (showReverbProbeSelection) {
      ++EditorGUI.indentLevel;
      DrawProbeSelection();
      --EditorGUI.indentLevel;
    }

    EditorGUILayout.Separator();

    DrawBakeButton();
    EditorGUI.EndDisabledGroup();  // Disabled if no material map is selected.
    EditorGUI.EndDisabledGroup();  // Disabled if in Play mode.

    serializedMaterialMapper.ApplyModifiedProperties();
  }
  /// @endcond

  // Loads the material mapper asset; creates one if not found.
  private void LoadOrCreateMaterialMapper() {
    materialMapper = AssetDatabase.LoadAssetAtPath<ResonanceAudioMaterialMapper>(
        materialMapperAssetPath);
    if (materialMapper == null) {
      materialMapper = ScriptableObject.CreateInstance<ResonanceAudioMaterialMapper>();
      AssetDatabase.CreateAsset(materialMapper, materialMapperAssetPath);
      AssetDatabase.SaveAssets();
    }

    serializedMaterialMapper = new UnityEditor.SerializedObject(materialMapper);
    reverbLayerMask = serializedMaterialMapper.FindProperty("reverbLayerMask");
    includeNonStaticGameObjects =
        serializedMaterialMapper.FindProperty("includeNonStaticGameObjects");
    materialMap = serializedMaterialMapper.FindProperty("materialMap");

    materialMapper.Initialize();
    RefreshMaterialMapper();
    UpdateShader();
  }

  // Loads the unique material mapper updater; creates one if not found.
  private void LoadOrCreateMaterialMapperUpdater() {
    if (EditorApplication.isPlayingOrWillChangePlaymode) {
      return;
    }

    var scene = EditorSceneManager.GetActiveScene();
    GameObject[] rootGameObjects = scene.GetRootGameObjects();
    for (int i = 0; i < rootGameObjects.Length; ++i) {
      var foundUpdater =
          rootGameObjects[i].GetComponentInChildren<ResonanceAudioMaterialMapperUpdater>();
      if (foundUpdater != null) {
        ResetMaterialMapperUpdater(foundUpdater);
        return;
      }
    }

    // Create an empty GameObject at the root, which is hidden and not saved, to hold a
    // ResonanceAudioMaterialMapperUpdater.
    GameObject updaterObject = new GameObject("Holder of mapper updater ID = ");
    updaterObject.hideFlags = HideFlags.HideAndDontSave;
    var newUpdater = updaterObject.AddComponent<ResonanceAudioMaterialMapperUpdater>();
    updaterObject.name += newUpdater.GetInstanceID();
    ResetMaterialMapperUpdater(newUpdater);
  }

  // Resets the |materialMapperUpdater| to |newUpdater| and destroy the old one if necessary.
  private void ResetMaterialMapperUpdater(ResonanceAudioMaterialMapperUpdater newUpdater) {
    if (newUpdater != materialMapperUpdater) {
      if (materialMapperUpdater != null) {
        DestroyImmediate(materialMapperUpdater.gameObject);
      }

      materialMapperUpdater = newUpdater;
    }
    materialMapperUpdater.RefreshMaterialMapper = RefreshMaterialMapperOnlyInVisualizeMode;
  }

  // Initializes the surface material colors in a global vector array for shaders.
  private void InitializeColorArrayInShader() {
    var numSurfaceMaterials =
        Enum.GetValues(typeof(ResonanceAudioRoomManager.SurfaceMaterial)).Length;
    Vector4[] vectorArray = new Vector4[numSurfaceMaterials];
    for (int surfaceMaterialIndex = 0; surfaceMaterialIndex < numSurfaceMaterials;
         ++surfaceMaterialIndex) {
      var color = ResonanceAudioMaterialMap.surfaceMaterialColors[surfaceMaterialIndex];
      vectorArray[surfaceMaterialIndex] = new Vector4(color.r, color.g, color.b, 0.5f);
    }

    Shader.SetGlobalVectorArray("_SurfaceMaterialColors", vectorArray);
  }

  // Initializes the surface material shader which visualizes surface materials as colors.
  private void InitializeSurfaceMaterialShader() {
    surfaceMaterialShader = Shader.Find("ResonanceAudio/SurfaceMaterial");
    if (surfaceMaterialShader == null) {
      Debug.LogError("Surface material shader not found");
      return;
    }
  }

  // Refreshes the material mapper's data to reflect external changes (e.g. scene modified,
  // material mapping changed).
  private void RefreshMaterialMapper() {
    if (EditorApplication.isPlaying) {
      return;
    }

    if (materialMap.objectReferenceValue == null) {
      return;
    }

    MeshRenderer[] meshRenderers = null;
    List<string>[] guidsForMeshRenderers = null;
    GatherMeshRenderersAndGuids(ref meshRenderers, ref guidsForMeshRenderers);

    Terrain[] activeTerrains = null;
    string[] guidsForTerrains = null;
    GatherTerrainsAndGuids(ref activeTerrains, ref guidsForTerrains);
    materialMapper.ApplyMaterialMapping(meshRenderers, guidsForMeshRenderers, activeTerrains,
                                        guidsForTerrains, surfaceMaterialShader);
    redraw = true;
  }

  // Refreshes the material mapper's data only in visualize mode.
  private void RefreshMaterialMapperOnlyInVisualizeMode() {
    if (isInVisualizeMode) {
      RefreshMaterialMapper();
    }
  }

  // Gathers the mesh renderes of game objects, and the GUIDs of the Unity Materials of
  // each sub-mesh.
  private void GatherMeshRenderersAndGuids(ref MeshRenderer[] meshRenderers,
                                           ref List<string>[] guidsForMeshRenderers) {
    List<MeshRenderer> meshRenderersList = new List<MeshRenderer>();
    List<List<string>> guidsForMeshRenderersList = new List<List<string>>();

    // Gather mesh renderers from all scenes.
    for (int sceneIndex = 0; sceneIndex < EditorSceneManager.sceneCount; ++sceneIndex) {
      Scene scene = EditorSceneManager.GetSceneAt(sceneIndex);
      if (!scene.isLoaded) {
        continue;
      }

      // Get the root game objects in this loaded scene.
      GameObject[] rootGameObjects = scene.GetRootGameObjects();
      for (int rootGameObjectIndex = 0; rootGameObjectIndex < rootGameObjects.Length;
           ++rootGameObjectIndex) {
        var rootGameObject = rootGameObjects[rootGameObjectIndex];

        var meshRenderersInChildren = rootGameObject.GetComponentsInChildren<MeshRenderer>();
        for (int meshRenderIndex = 0; meshRenderIndex < meshRenderersInChildren.Length;
             ++meshRenderIndex) {
          var meshRenderer = meshRenderersInChildren[meshRenderIndex];
          meshRenderersList.Add(meshRenderer);

          // Each Unity Material of a mesh renderer correspondes to a sub-mesh.
          var unityMaterials = meshRenderer.sharedMaterials;
          var guidsForMeshRenderer = new List<string>();
          for (int subMeshIndex = 0; subMeshIndex < unityMaterials.Length; ++subMeshIndex) {
            // Find the GUID that identifies this Unity Material.
            var unityMaterial = unityMaterials[subMeshIndex];
            string assetPath = AssetDatabase.GetAssetPath(unityMaterial);
            guidsForMeshRenderer.Add(AssetDatabase.AssetPathToGUID(assetPath));
          }
          guidsForMeshRenderersList.Add(guidsForMeshRenderer);
        }
      }
    }

    meshRenderers = meshRenderersList.ToArray();
    guidsForMeshRenderers = guidsForMeshRenderersList.ToArray();
  }

  // Gathers the terrains and the GUIDs of the terrain data.
  private void GatherTerrainsAndGuids(ref Terrain[] activeTerrains, ref string[] guidsForTerrains) {
    List<string> guidsForTerrainsList = new List<string>();

    // Gather from |activeTerrains|, the terrains in all loaded scenes.
    activeTerrains = Terrain.activeTerrains;
    foreach (var terrain in activeTerrains) {
      // Finds the GUID that identifies this terrain data.
      string assetPath = AssetDatabase.GetAssetPath(terrain.terrainData);
      guidsForTerrainsList.Add(AssetDatabase.AssetPathToGUID(assetPath));
    }
    guidsForTerrains = guidsForTerrainsList.ToArray();
  }

  // Attempts to update the scene views' shader, using the surface material shader stored in
  // |materialMapper| if |isInVisualizeMode| is true, and using the default shader otherwise.
  // Defers the updating to OnSceneGUI() if the scene views are not ready yet.
  private void UpdateShader() {
    var sceneViews = SceneView.sceneViews;

    // Defer the updating if the scene views are not ready.
    if (sceneViews.Count == 0) {
      updatedSceneViews.Clear();
      return;
    }

    // Update all ready scene views.
    for (int i = 0; i < sceneViews.Count; ++i) {
      UpdateShaderForSceneView((SceneView) sceneViews[i]);
    }
  }

  // Updates the shader of a specific scene view.
  private void UpdateShaderForSceneView(SceneView sceneView) {
    if (isInVisualizeMode) {
      sceneView.SetSceneViewShaderReplace(surfaceMaterialShader, "RenderType");
    } else {
      sceneView.SetSceneViewShaderReplace(null, null);
    }
    sceneView.Repaint();
    updatedSceneViews.Add(sceneView.GetInstanceID());
  }

  // The UI for selecting a ResonanceAudioMaterialMap asset to use.
  private void DrawMaterialMapSelection() {
    EditorGUILayout.PropertyField(materialMap, materialMapLabel);
  }

  // Draws the objects filtering GUI. Users can decide which layers to include, and whether to
  // include non-static objects.
  private void DrawObjectFiltering() {
    EditorGUILayout.PropertyField(reverbLayerMask, reverbLayerMaskLabel);

    EditorGUILayout.Separator();

    EditorGUILayout.PropertyField(includeNonStaticGameObjects, nonStaticGameObjectLabel);
  }

  // Draws the "Visualize Mode" checkbox.
  private void DrawVisualizeModeCheckbox() {
    if (isInVisualizeMode != EditorGUILayout.Toggle(visualizeModeLabel, isInVisualizeMode)) {
      isInVisualizeMode = !isInVisualizeMode;
      RefreshMaterialMapper();
      UpdateShader();
    }
  }

  // The UI for selecting a subset of reverb probes to bake reverb to.
  private void DrawProbeSelection() {
    ResonanceAudioReverbProbe[] allReverbProbes =
        UnityEngine.Object.FindObjectsOfType<ResonanceAudioReverbProbe>();

    // Clean up the deleted reverb probes.
    var selectedReverbProbes = ResonanceAudioReverbComputer.selectedReverbProbes;
    selectedReverbProbes.RemoveAll(reverbProbe => reverbProbe == null);

    probeSelectionScrollPosition = EditorGUILayout.BeginScrollView(probeSelectionScrollPosition,
                                                                   GUILayout.ExpandHeight(false));
    for (int i = 0; i < allReverbProbes.Length; ++i) {
      var reverbProbe = allReverbProbes[i];
      bool currentlySelected = selectedReverbProbes.Contains(reverbProbe);
      if (EditorGUILayout.ToggleLeft(reverbProbe.name, currentlySelected)) {
        if (!currentlySelected) {
          // Reverb probe selected.
          selectedReverbProbes.Add(reverbProbe);
        }
      } else {
        if (currentlySelected) {
          // Reverb probe de-selected.
          selectedReverbProbes.Remove(reverbProbe);
        }
      }
    }
    EditorGUILayout.EndScrollView();

    if (allReverbProbes.Length > 0) {
      EditorGUILayout.Separator();

      EditorGUILayout.BeginHorizontal();
      GUILayout.Space(15 * EditorGUI.indentLevel);
      if (GUILayout.Button(selectAllProbesLabel)) {
        for (int i = 0; i < allReverbProbes.Length; ++i) {
          if (!selectedReverbProbes.Contains(allReverbProbes[i])) {
            selectedReverbProbes.Add(allReverbProbes[i]);
          }
        }
      }
      if (GUILayout.Button(clearAllProbesLabel)) {
        selectedReverbProbes.Clear();
      }
      EditorGUILayout.EndHorizontal();
    } else {
      EditorGUILayout.HelpBox("No ResonanceAudioReverbProbe exists in the scene.",
                              MessageType.Warning);
    }
  }

  // The UI to compute reverb and bake the results to the selected probes.
  private void DrawBakeButton() {
    // Only enable the "Bake" button when at least one reverb probe is selected and the scene
    // is loaded.
    var scene = EditorSceneManager.GetActiveScene();
    EditorGUI.BeginDisabledGroup(ResonanceAudioReverbComputer.selectedReverbProbes.Count == 0 ||
                                 !scene.isLoaded);
    EditorGUILayout.BeginHorizontal();
    GUILayout.Space(15 * EditorGUI.indentLevel);
    if (GUILayout.Button(bakeLabel)) {
      // We allow only one material mapper in the scene. Find the unique one and ask for acoustic
      // meshes that should be included in the reverb computation.
      if (materialMapper != null) {
        // Compute the reverb for the selected reverb probes using the included acoustic meshes.
        RefreshMaterialMapper();
        ResonanceAudioReverbComputer.ComputeReverb(materialMapper.GetIncludedAcousticMeshes());
      }
    }
    EditorGUILayout.EndHorizontal();
    EditorGUI.EndDisabledGroup();
  }

  private void OnSceneGUI(SceneView sceneView) {
    // Deferred update of the scene view if it is not updated yet.
    if (!updatedSceneViews.Contains(sceneView.GetInstanceID())) {
      UpdateShaderForSceneView(sceneView);
    }

    if (isInVisualizeMode && redraw) {
      materialMapper.RenderAcousticMeshes();
      redraw = false;
    }
  }

  private void OnSceneOrModeSwitch() {
    LoadOrCreateMaterialMapperUpdater();

    // Force repaint this window to reflect the scene changes, which may have a different set of
    // Unity Materials and Terrain data.
    Repaint();
  }
}
