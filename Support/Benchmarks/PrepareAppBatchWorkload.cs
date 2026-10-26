using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class AppWorkloadSetup
{
    public static string Refresh()
    {
        Debug.Log("[BatchWorkloadBenchmark] Refresh temporary measurement scopes.");
        AssetDatabase.Refresh(); return Application.consoleLogPath;
    }
    public static string Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Editor busy");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene; refusing to replace it");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var config = UnityEngine.Object.FindAnyObjectByType<TiltBrush.Config>();
        if (config == null) throw new Exception("Main Config missing");
        System.IO.File.WriteAllText("Logs/workload-original-config.json", EditorJsonUtility.ToJson(config));
        config.m_SdkMode = TiltBrush.SdkMode.Monoscopic;
        config.m_FakeCommandLineArgsInEditor = "--DisableXrMode --EnableMonoscopicMode";
        SessionState.SetBool("BatchWorkloadRunning", false);
        SessionState.SetBool("BatchWorkloadCancel", false);
        EditorApplication.isPlaying = true;
        return "Starting owned benchmark Play session with batching left at scene default.";
    }
    public static string RestoreScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Editor busy");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene; refusing to replace it");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        return "Restored Main scene from disk without saving temporary benchmark settings";
    }
    public static string Stop() { EditorApplication.isPlaying = false; return "Leaving owned benchmark Play session"; }
    public static string Status()
    {
        return $"play={EditorApplication.isPlaying}; app={TiltBrush.App.CurrentState}; batch={(TiltBrush.App.Config == null ? false : TiltBrush.App.Config.m_UseBatchedBrushes)}; memory={(TiltBrush.SketchMemoryScript.m_Instance == null ? -1 : TiltBrush.SketchMemoryScript.m_Instance.GetMemoryList.Count)}";
    }
}
