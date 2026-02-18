using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class CopyGvrToAudioSource : Editor
{
    private static string currentSceneOrPrefabName;
    private static int copiedCount;
    private static int addedCount;

    [MenuItem("Open Brush/Copy GvrAudioSource Properties to AudioSource")]
    public static void Run()
    {
        copiedCount = 0;
        addedCount = 0;

        // Save current scene state before iterating
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        // Iterate through all scenes in project
        string[] sceneGUIDs = AssetDatabase.FindAssets("t:Scene");
        foreach (string sceneGuid in sceneGUIDs)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            currentSceneOrPrefabName = scene.name;

            bool sceneModified = false;
            foreach (GameObject obj in scene.GetRootGameObjects())
            {
                sceneModified |= IterateHierarchy(obj.transform);
            }

            if (sceneModified)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[CopyGvr] Saved scene: {scene.name}");
            }
        }

        // Iterate through all prefabs
        string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
        foreach (string prefabGuid in prefabGUIDs)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);

            currentSceneOrPrefabName = prefab.name;
            bool prefabModified = IterateHierarchy(prefab.transform);

            if (prefabModified)
            {
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                Debug.Log($"[CopyGvr] Saved prefab: {prefab.name}");
            }

            PrefabUtility.UnloadPrefabContents(prefab);
        }

        Debug.Log($"[CopyGvr] Done. Copied properties on {copiedCount} objects, added AudioSource on {addedCount} objects.");
    }

    private static bool IterateHierarchy(Transform transform)
    {
        bool modified = CopyGvrSource(transform.gameObject);
        foreach (Transform child in transform)
        {
            modified |= IterateHierarchy(child);
        }
        return modified;
    }

    private static bool CopyGvrSource(GameObject go)
    {
        var gvr = go.GetComponent<GvrAudioSource>();
        if (gvr == null) return false;

        var audioSource = go.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = go.AddComponent<AudioSource>();
            addedCount++;
            Debug.Log($"[CopyGvr] Added AudioSource to {currentSceneOrPrefabName}/{go.name}");
        }

        audioSource.bypassEffects = gvr.bypassRoomEffects;
        audioSource.bypassListenerEffects = gvr.bypassRoomEffects;
        audioSource.clip = gvr.sourceClip;
        audioSource.loop = gvr.sourceLoop;
        audioSource.mute = gvr.sourceMute;
        audioSource.pitch = gvr.sourcePitch;
        audioSource.playOnAwake = gvr.playOnAwake;
        audioSource.priority = gvr.sourcePriority;
        audioSource.spatialBlend = gvr.sourceSpatialBlend;
        audioSource.spatialize = gvr.hrtfEnabled;
        audioSource.dopplerLevel = gvr.sourceDopplerLevel;
        audioSource.spread = gvr.sourceSpread;
        audioSource.volume = gvr.sourceVolume;
        audioSource.rolloffMode = gvr.sourceRolloffMode;
        audioSource.maxDistance = gvr.sourceMaxDistance;
        audioSource.minDistance = gvr.sourceMinDistance;

        // Log properties that would require a SteamAudioSource component to migrate fully
        if (gvr.occlusionEnabled)
            Debug.LogWarning($"[CopyGvr] {currentSceneOrPrefabName}/{go.name}: occlusionEnabled=true — needs SteamAudioSource.occlusion");
        if (gvr.directivityAlpha > 0f)
            Debug.LogWarning($"[CopyGvr] {currentSceneOrPrefabName}/{go.name}: directivityAlpha={gvr.directivityAlpha} — needs SteamAudioSource.dipoleWeight/dipolePower");
        if (gvr.gainDb != 0f)
            Debug.LogWarning($"[CopyGvr] {currentSceneOrPrefabName}/{go.name}: gainDb={gvr.gainDb} — no Steam Audio equivalent, consider baking into volume");

        copiedCount++;
        Debug.Log($"[CopyGvr] Copied GvrAudioSource properties to AudioSource on {currentSceneOrPrefabName}/{go.name}");

        return true;
    }
}
