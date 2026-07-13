// Copyright 2026 The Open Brush Authors
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

using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class InstallTiltFileThumbnailsButton : BaseButton
    {
        private const string kInstallerFilename = "TiltThumbs-Installer.exe";
        private const string kButtonLabel = "Install .tilt thumbnails";
        private const string kInstallerMissingMessage = "Thumbnail installer not found.";
        private const string kOpenFolderMessage =
            "Remove headset and run TiltThumbs-Installer.exe from the folder that opened.";

        protected override void Awake()
        {
            base.Awake();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            SetTextLabel();
#else
            gameObject.SetActive(false);
#endif
        }

        private void SetTextLabel()
        {
            TextMeshPro label = GetComponentInChildren<TextMeshPro>();
            if (label != null)
            {
                label.text = kButtonLabel;
            }
        }

        protected override void OnButtonPressed()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string installerPath = GetInstallerPath();
            if (!File.Exists(installerPath))
            {
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush,
                    kInstallerMissingMessage,
                    fPopScalar: 0.5f);
                UnityEngine.Debug.LogWarning($"Tilt thumbnail installer was not found at {installerPath}.");
                return;
            }

            string installerFolder = Path.GetDirectoryName(installerPath);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerFolder,
                    UseShellExecute = true
                });

                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush,
                    kOpenFolderMessage,
                    fPopScalar: 0.5f);
            }
            catch (System.Exception ex)
            {
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush,
                    "Could not open thumbnail installer folder.",
                    fPopScalar: 0.5f);
                UnityEngine.Debug.LogWarning($"Could not open thumbnail installer folder: {ex.Message}");
            }
#endif
        }

        private static string GetInstallerPath()
        {
#if UNITY_EDITOR_WIN
            return Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Support", "windows", "tools", kInstallerFilename));
#else
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", kInstallerFilename));
#endif
        }
    }
} // namespace TiltBrush
