using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildProbe
{
    public static void Build()
    {
        PlayerSettings.companyName = "Icosa";
        PlayerSettings.productName = "OBDS isolated probe";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,
            "foundation.icosa.obdsunityprobe20261009");
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel30;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
        PlayerSettings.Android.forceInternetPermission = false;
        PlayerSettings.Android.forceSDCardPermission = false;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("OBDS_Probe").AddComponent<StorageProbe>();
        EditorSceneManager.SaveScene(scene, "Assets/Probe.unity");
        Directory.CreateDirectory("Build");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Probe.unity" },
            locationPathName = "Build/obds-unity-probe.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"OBDS_BUILD failed: {report.summary.result}");
        Debug.Log($"OBDS_BUILD succeeded bytes={report.summary.totalSize}");
    }
}
