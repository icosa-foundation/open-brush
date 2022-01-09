﻿// Copyright 2021 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections;
using System.Collections.Generic;
using System.Resources;
using TiltBrush;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;

[ExecuteInEditMode]
public class ToggleMonoscopic : MonoBehaviour
{
    private static Scene MainScene => EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

    [MenuItem("Tilt/Enable Monoscopic Mode in Editor")]
    public static void EnableMono()
    {
        SetConfigSDKMode(SdkMode.Monoscopic);
        SteamVrSettings.autoEnableVR = false;
        EditorUtility.SetDirty(SteamVrSettings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tilt/Disable Monoscopic Mode in Editor")]
    public static void DisableMono()
    {
        // TODO Update this when we switch to OpenXR branch
        SetConfigSDKMode(SdkMode.SteamVR);
        SteamVrSettings.autoEnableVR = true;
        EditorUtility.SetDirty(SteamVrSettings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static SteamVR_Settings SteamVrSettings
    {
        get
        {
            string[] svr = AssetDatabase.FindAssets("t:SteamVR_Settings");
            if (svr.Length == 1)
            {
                string path = AssetDatabase.GUIDToAssetPath(svr[0]);
                return (SteamVR_Settings)AssetDatabase.LoadAssetAtPath(path, typeof(SteamVR_Settings));
            }
            else
            {
                if (svr.Length == 0)
                {
                    Debug.LogWarning("No SteamVR_Settings found");
                }
                else
                {
                    Debug.LogWarning("Multiple SteamVR_Settings found");
                }
                return null;
            }
        }
    }

    private static void SetConfigSDKMode(SdkMode mode)
    {
        var roots = MainScene.GetRootGameObjects();
        Config config = null;
        foreach (var root in roots)
        {
            if (root.GetComponent<App>() != null)
            {
                config = root.GetComponentInChildren<Config>();
                Undo.RecordObject(config, "Change Monoscopic SDK");
                config.m_SdkMode = mode;
                EditorUtility.SetDirty(config);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                // TODO this failed to mark the scene as saved for some reason.
                // Better to let the user do it manually.
                // EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                break;
            }
        }
    }

}
