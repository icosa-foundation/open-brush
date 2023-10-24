using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class ReplaceGvr : Editor
{

    // Only used for console logging
    private static string currentSceneOrPrefabName;

    [MenuItem("Open Brush/Replace GoogleVR Audio")]
    public static void Run()
    {
        // Iterate through all scenes
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            EditorSceneManager.OpenScene(scene.path);

            currentSceneOrPrefabName = scene.name;
            foreach (GameObject obj in FindObjectsOfType<GameObject>(includeInactive: true))
            {
                FixAllGvr(obj);
            }
        }

        // Iterate through all prefabs
        string[] prefabGUIDs = AssetDatabase.FindAssets("t:Prefab");
        foreach (string prefabGuid in prefabGUIDs)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            IteratePrefab(prefab.transform);
        }
    }

    private static void IteratePrefab(Transform transform)
    {
        currentSceneOrPrefabName = transform.name;
        FixAllGvr(transform.gameObject);

        foreach (Transform child in transform)
        {
            IteratePrefab(child);
        }
    }

    public static void FixAllGvr(GameObject go)
    {
        FixGvrSource(go);
        FixGvrSoundfield(go);
        FixGvrRoom(go);
        FixGvrListener(go);
    }

    public static void FixGvrSoundfield(GameObject go)
    {
        var gvr = go.GetComponent<GvrAudioSoundfield>();
        if (gvr == null) return;

        // Disable Resonance for now
        // if (go.GetComponent<ResonanceAudioSource>() == null)
        // {
        //     go.AddComponent<ResonanceAudioSource>();
        //     Debug.Log($"Added ResonanceAudioSource to {currentSceneOrPrefabName}.{go.name}");
        // }
    }

    public static void FixGvrRoom(GameObject go)
    {
        var gvr = go.GetComponent<GvrAudioRoom>();
        if (gvr == null) return;

        // Disable Resonance for now
        // ResonanceAudioRoom resAudioRoom = null;
        // if (go.GetComponent<ResonanceAudioRoom>() == null)
        // {
        //     resAudioRoom = go.AddComponent<ResonanceAudioRoom>();
        //     Debug.Log($"Added ResonanceAudioRoom to {currentSceneOrPrefabName}.{go.name}");
        // }

        // resAudioRoom.leftWall = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.leftWall;
        // resAudioRoom.rightWall = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.rightWall;
        // resAudioRoom.floor = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.floor;
        // resAudioRoom.ceiling = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.ceiling;
        // resAudioRoom.backWall = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.backWall;
        // resAudioRoom.frontWall = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.frontWall;
        // resAudioRoom.reflectivity = gvr.reflectivity;
        // resAudioRoom.reverbGainDb = gvr.reverbGainDb;
        // resAudioRoom.reverbBrightness = gvr.reverbBrightness;
        // resAudioRoom.reverbTime = gvr.reverbTime;
        // resAudioRoom.size = gvr.size;
    }

    public static void FixGvrListener(GameObject go)
    {
        var gvr = go.GetComponent<GvrAudioListener>();
        if (gvr == null) return;

        // Disable Resonance for now
        // ResonanceAudioListener resAudioListener = null;
        // if (go.GetComponent<ResonanceAudioListener>() == null)
        // {
        //     resAudioListener = go.AddComponent<ResonanceAudioListener>();
        //     Debug.Log($"Added ResonanceAudioListener to {currentSceneOrPrefabName}.{go.name}");
        // }

        // resAudioListener.occlusionMask = gvr.occlusionMask;
        // resAudioListener.globalGainDb = gvr.globalGainDb;
        // // resAudioListener.??? = gvr.quality;
    }

    public static void FixGvrSource(GameObject go)
    {
        var gvr = go.GetComponent<GvrAudioSource>();
        if (gvr == null) return;

        var audioSource = go.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = go.AddComponent<AudioSource>();
            Debug.Log($"Added AudioSource to {currentSceneOrPrefabName}.{go.name}");
        }

        audioSource.bypassEffects = gvr.bypassRoomEffects;
        audioSource.bypassListenerEffects = gvr.bypassRoomEffects;
        audioSource.clip = gvr.sourceClip;
        audioSource.loop = gvr.sourceLoop;
        audioSource.mute = gvr.sourceMute;
        audioSource.pitch = gvr.sourcePitch;
        audioSource.priority = gvr.sourcePriority;
        audioSource.spatialBlend = gvr.sourceSpatialBlend;
        audioSource.dopplerLevel = gvr.sourceDopplerLevel;
        audioSource.spread = gvr.sourceSpread;
        audioSource.volume = gvr.sourceVolume;
        audioSource.rolloffMode = gvr.sourceRolloffMode;
        audioSource.maxDistance = gvr.sourceMaxDistance;
        audioSource.minDistance = gvr.sourceMinDistance;

        // Disable Resonance for now
        // ResonanceAudioSource resAudioSource = null;
        // if (go.GetComponent<ResonanceAudioSource>() == null)
        // {
        //     resAudioSource = go.AddComponent<ResonanceAudioSource>();
        //     Debug.Log($"Added ResonanceAudioSource to {currentSceneOrPrefabName}.{go.name}");
        // }

        // resAudioSource.directivityAlpha = gvr.directivityAlpha;
        // resAudioSource.directivitySharpness = gvr.directivitySharpness;
        // resAudioSource.listenerDirectivityAlpha = gvr.listenerDirectivityAlpha;
        // resAudioSource.listenerDirectivitySharpness = gvr.listenerDirectivitySharpness;
        // resAudioSource.gainDb = gvr.gainDb;
        // resAudioSource.occlusionEnabled = gvr.occlusionEnabled;
        // audioSource.playOnAwake = gvr.playOnAwake;
        // // resAudioSource.disableOnStop = gvr.disableOnStop;
        // // resAudioSource.hrtfEnabled = gvr.hrtfEnabled;
    }
}
