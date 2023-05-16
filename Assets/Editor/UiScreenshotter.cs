// Copyright 2023 The Tilt Brush Authors
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    public class UiScreenshotter : Editor
    {

        [MenuItem("Open Brush/Info/Generate UI Screenshots")]
        static void Generate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("You can only run this whilst in Play Mode");
                return;
            }

            var blackGuid = Guid.Parse("580b4529-ac50-4fe9-b8d2-635765a14893");
            var env = EnvironmentCatalog.m_Instance.GetEnvironment(blackGuid);
            SceneSettings.m_Instance.SetDesiredPreset(env,
                keepSceneTransform: true, forceTransition: false, hasCustomLights: false, skipFade: true);

            foreach (BasePanel.PanelType panelType in (BasePanel.PanelType[])Enum.GetValues(typeof(BasePanel.PanelType)))
            {
                if (!PanelManager.m_Instance.IsPanelOpen(panelType))
                {
                    PanelManager.m_Instance.OpenPanel(panelType, TrTransform.T(new Vector3(0, 50, 2)));
                }
            }
            DelayedTasks();
        }

        async static void DelayedTasks()
        {
            await Task.Delay(3000);

            var cam = Camera.main;
            cam.transform.position = new Vector3(0, 100, 0);
            cam.transform.rotation = Quaternion.identity;

            int count = ((BasePanel.PanelType[])Enum.GetValues(typeof(BasePanel.PanelType))).Length;
            Debug.Log($"Starting {count} panel screenshots");
            for (var i = 0; i < count; i++)
            {
                var panelType = ((BasePanel.PanelType[])Enum.GetValues(typeof(BasePanel.PanelType)))[i];
                Debug.Log($"Screenshot {i}: {panelType}");
                TrTransform panelTr = TrTransform.T(new Vector3(-1.25f, 100, 4));
                if (PanelManager.m_Instance.IsPanelOpen(panelType))
                {
                    BasePanel panel = PanelManager.m_Instance.GetPanelByType(panelType);
                    panel.PanelGazeActive(true);
                    await Task.Delay(500);
                    var originalTransform = TrTransform.FromTransform(panel.transform);
                    panelTr.ToTransform(panel.transform);
                    panel.ResetReticleOffset();
                    SaveCurrentView(cam, $"panel-{panelType}.png", 1600, 1600);

                    // Try to open popups
                    FieldInfo fieldInfo = typeof(BasePanel).GetField("m_PanelPopUpMap", BindingFlags.NonPublic | BindingFlags.Instance);
                    PopupMapKey[] popupMap = (PopupMapKey[])fieldInfo?.GetValue(panel);
                    if (popupMap != null)
                    {
                        foreach (var popup in popupMap)
                        {
                            var btn = panel.GetComponentsInChildren<OptionButton>()
                                .FirstOrDefault(x => x.m_Command == popup.m_Command);
                            if (btn == null)
                            {
                                Debug.LogWarning($"No button found for {popup.m_Command}");
                                continue;
                            }
                            Debug.Log($"Screenshop popup for {popup.m_Command}");
                            GameObject go = Instantiate(popup.m_PopUpPrefab,
                                btn.transform.position + new Vector3(.5f, 0, -0.25f), btn.transform.rotation);
                            go.transform.localScale = Vector3.one * 5;
                            var activePopUp = go.GetComponent<PopUpWindow>();
                            activePopUp.Init(panel.gameObject, "");
                            try
                            {
                                activePopUp.SetPopupCommandParameters(btn.m_CommandParam, btn.m_CommandParam2);
                            }
                            catch (NullReferenceException e) { }
                            SaveCurrentView(cam, $"panel-{panelType}_{btn.m_Command}.png", 1600, 1600);
                            go.transform.position = new Vector3(-100, 0, 0);
                            Destroy(go);
                        }
                    }
                    originalTransform.ToTransform(panel.transform);
                }
            }
        }

        static void SaveCurrentView(Camera cameraToCapture, string fileName, int resWidth, int resHeight)
        {
            RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
            cameraToCapture.targetTexture = rt;
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            cameraToCapture.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            cameraToCapture.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            string filePath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), fileName);
            File.WriteAllBytes(filePath, bytes);
        }
    }
}
