using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class ReplaceGvr : Editor
{
    [MenuItem("Custom/ReplaceGvr")]
    public static void Run()
    {
        // Iterate through all scenes
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            EditorSceneManager.OpenScene(scene.path);
            
            foreach (GameObject obj in GameObject.FindObjectsOfType<GameObject>())
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
        FixGvrSource(transform.gameObject);
        
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
        
#if RESONANCE_AUDIO_PRESENT
        if (go.GetComponent<ResonanceAudioSource>() != null) return;
        var resAudioSoundfield = go.AddComponent<ResonanceAudioSource>();

        resAudioSoundfield.bypassRoomEffects = gvr.bypassRoomEffects;
        resAudioSoundfield.gainDb = gvr.gainDb;
        audioSource.playOnAwake = gvr.playOnAwake;
        // See https://resonance-audio.github.io/resonance-audio/develop/unity/getting-started.html#gvraudiosoundfield
        audioSource.clip = gvr.soundfieldClip0102;
        // audioSource.clip = gvr.soundfieldClip0304; // TODO How do we handle this
        audioSource.loop = gvr.soundfieldLoop;
        audioSource.mute = gvr.soundfieldMute;
        audioSource.pitch = gvr.soundfieldPitch;
        audioSource.priority = gvr.soundfieldPriority;
        audioSource.spatialBlend = gvr.soundfieldSpatialBlend;
        audioSource.dopplerLevel = gvr.soundfieldDopplerLevel;
        audioSource.volume = gvr.soundfieldVolume;
        audioSource.rolloffMode = gvr.soundfieldRolloffMode;
        audioSource.maxDistance = gvr.soundfieldMaxDistance;
        audioSource.minDistance = gvr.soundfieldMinDistance;
#endif
    }

    public static void FixGvrRoom(GameObject go)
    {
        var gvr = go.GetComponent<GvrAudioRoom>();
        if (gvr == null) return;
        
        
#if RESONANCE_AUDIO_PRESENT
        if (go.GetComponent<ResonanceAudioRoom>() != null) return;
        var resAudioRoom = go.AddComponent<ResonanceAudioRoom>();

        resAudioRoom.leftWall = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.leftWall;
        resAudioRoom.rightWall = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.rightWall;
        resAudioRoom.floor = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.floor;
        resAudioRoom.ceiling = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.ceiling;
        resAudioRoom.backWall = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.backWall;
        resAudioRoom.frontWall = (ResonanceAudioRoomManager.SurfaceMaterial)gvr.frontWall;
        resAudioRoom.reflectivity = gvr.reflectivity;
        resAudioRoom.reverbGainDb = gvr.reverbGainDb;
        resAudioRoom.reverbBrightness = gvr.reverbBrightness;
        resAudioRoom.reverbTime = gvr.reverbTime;
        resAudioRoom.size = gvr.size;
#endif
    }

    public static void FixGvrListener(GameObject go)
    {
        var gvr = go.GetComponent<GvrAudioListener>();
        if (gvr == null) return;
        
        var audioListener = go.GetComponent<AudioListener>();
        if (audioListener == null)
        {
            go.AddComponent<AudioListener>();
        }
        
#if RESONANCE_AUDIO_PRESENT
        if (go.GetComponent<ResonanceAudioListener>() != null) return;
        var resAudioListener = go.AddComponent<ResonanceAudioListener>();
#endif
    }
    
    public static void FixGvrSource(GameObject go)
    {
        var gvr = go.GetComponent<GvrAudioSource>();
        if (gvr == null) return;
        
        var audioSource = go.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            go.AddComponent<AudioSource>();
        }
    
#if RESONANCE_AUDIO_PRESENT
        if (go.GetComponent<ResonanceAudioSource>() != null) return;
        var resAudioSource = go.AddComponent<ResonanceAudioSource>();
        
        audioSource.bypassEffects = gvr.bypassRoomEffects;
        audioSource.bypassListenerEffects = gvr.bypassRoomEffects;
        resAudioSource.directivityAlpha = gvr.directivityAlpha;
        resAudioSource.directivitySharpness = gvr.directivitySharpness;
        resAudioSource.listenerDirectivityAlpha = gvr.listenerDirectivityAlpha;
        resAudioSource.listenerDirectivitySharpness = gvr.listenerDirectivitySharpness;
        resAudioSource.gainDb = gvr.gainDb;
        resAudioSource.occlusionEnabled = gvr.occlusionEnabled;
        audioSource.playOnAwake = gvr.playOnAwake;
        // resAudioSource.disableOnStop = gvr.disableOnStop;
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
        // resAudioSource.hrtfEnabled = gvr.hrtfEnabled;
#endif
    }
}
