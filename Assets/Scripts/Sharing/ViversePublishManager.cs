// Copyright 2020 The Tilt Brush Authors
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{
    public class ViversePublishManager : MonoBehaviour
    {
        private static readonly string[] DefaultSandboxPermissions = new[]
        {
            "allow-forms",
            "allow-modals",
            "allow-popups",
            "allow-top-navigation",
            "allow-pointer-lock",
            "allow-presentation",
            "allow-downloads",
            "allow-orientation-lock",
            "allow-popups-to-escape-sandbox",
            "allow-top-navigation-by-user-activation"
        };

        private static readonly string[] DefaultAllowPermissions = new[]
        {
            "accelerometer",
            "camera",
            "gyroscope",
            "magnetometer",
            "microphone",
            "midi",
            "window-management",
            "xr-spatial-tracking"
        };

        private ViverseAuthManager m_AuthManager;
        private string m_AccessToken;
        private string m_LastSceneSid;
        private WorldContentResponse m_LastResponse;

        public event Action<bool, string> OnPublishComplete;
        public event Action<float> OnUploadProgress;

        void Start()
        {
            m_AuthManager = FindObjectOfType<ViverseAuthManager>();

            if (m_AuthManager != null)
            {
                m_AuthManager.OnAuthComplete += OnAuthSuccess;
            }

            LoadSavedToken();
        }

        private void OnAuthSuccess(string accessToken, string refreshToken, int expiresIn, string accountId, string profileName, string avatarUrl, string avatarId)
        {
            m_AccessToken = accessToken;

            // Save token data to PlayerPrefs
            var tokenData = ViverseTokenData.FromAuthResponse(accessToken, refreshToken, expiresIn);
            string tokenJson = JsonUtility.ToJson(tokenData);
            PlayerPrefs.SetString("viverse_token", tokenJson);
            PlayerPrefs.Save();
        }

        private void LoadSavedToken()
        {
            if (PlayerPrefs.HasKey("viverse_token"))
            {
                try
                {
                    string tokenJson = PlayerPrefs.GetString("viverse_token");
                    var tokenData = JsonUtility.FromJson<ViverseTokenData>(tokenJson);
                    if (tokenData != null && tokenData.IsValid)
                    {
                        m_AccessToken = tokenData.AccessToken;
                    }
                    else
                    {
                        Debug.LogWarning("ViversePublishManager: Saved token is invalid or expired");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"ViversePublishManager: Failed to load token: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Publishes a world to VIVERSE
        /// </summary>
        /// <param name="title">World title (max 30 characters)</param>
        /// <param name="description">World description</param>
        /// <param name="zipFilePath">Path to content.zip file</param>
        public void PublishWorld(string title, string description, string zipFilePath)
        {
            if (string.IsNullOrEmpty(m_AccessToken))
            {
                Debug.LogError("No access token available. Please login first.");
                OnPublishComplete?.Invoke(false, "Not authenticated. Please login first.");
                return;
            }

            if (!File.Exists(zipFilePath))
            {
                Debug.LogError($"File not found: {zipFilePath}");
                OnPublishComplete?.Invoke(false, $"File not found: {zipFilePath}");
                return;
            }

            if (title.Length > 30)
            {
                title = title.Substring(0, 30);
                Debug.LogWarning($"Title truncated to 30 characters: {title}");
            }

            StartCoroutine(PublishWorldCoroutine(title, description, zipFilePath));
        }

        /// <summary>
        /// Publishes with automatic timestamp in title
        /// </summary>
        public void PublishWorldWithTimestamp(string baseTitle, string description, string zipFilePath)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string title = $"{baseTitle}{timestamp}";
            PublishWorld(title, description, zipFilePath);
        }

        private IEnumerator PublishWorldCoroutine(string title, string description, string zipFilePath)
        {
            yield return CreateWorldContent(title, description, (success, sceneSid, error) =>
            {
                if (success)
                {
                    m_LastSceneSid = sceneSid;

                    // CHANGED: Passing m_LastResponse.hub_sid to the upload coroutine
                    string hubSid = m_LastResponse != null ? m_LastResponse.hub_sid : "";
                    StartCoroutine(UploadWorldContent(sceneSid, hubSid, zipFilePath));
                }
                else
                {
                    Debug.LogError($"[ViversePublish] Failed to create content: {error}");
                    OnPublishComplete?.Invoke(false, $"Failed to create content: {error}");
                }
            });
        }

        public void CreateWorldOnly(string title, string description, Action<bool, string, string> callback)
        {
            if (string.IsNullOrEmpty(m_AccessToken))
            {
                callback?.Invoke(false, null, "Not authenticated");
                return;
            }

            if (title.Length > 30)
            {
                title = title.Substring(0, 30);
            }

            StartCoroutine(CreateWorldContent(title, description, callback));
        }

        public IEnumerator CreateWorldContent(string title, string description, Action<bool, string, string> callback)
        {
            string url = ViverseEndpoints.WORLD_CREATE;

            var payload = new WorldContentPayload
            {
                title = title,
                description_plaintext = description,
                preferred_devices = new[] { "hmd", "desktop", "android", "ios" },
                tags = "Open Brush",
            };

            string json = JsonUtility.ToJson(payload);

            UnityWebRequest request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("AccessToken", m_AccessToken);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"{request.error} - {request.downloadHandler.text}";
                callback?.Invoke(false, null, error);
            }
            else
            {
                try
                {
                    string responseText = request.downloadHandler.text;

                    var response = JsonUtility.FromJson<WorldContentResponse>(responseText);

                    if (!string.IsNullOrEmpty(response.scene_sid))
                    {
                        m_LastSceneSid = response.scene_sid;
                        m_LastResponse = response;
                        PlayerPrefs.SetString("viverse_scene_sid", response.scene_sid);
                        PlayerPrefs.Save();
                        callback?.Invoke(true, response.scene_sid, null);
                    }
                    else
                    {
                        callback?.Invoke(false, null, "No scene_sid in response");
                    }
                }
                catch (Exception ex)
                {
                    callback?.Invoke(false, null, $"Failed to parse response: {ex.Message}");
                }
            }

            request.Dispose();
        }

        // CHANGED: Added hubSid parameter
        public IEnumerator UploadWorldContent(string sceneSid, string hubSid, string zipFilePath)
        {
            string url = string.Format(ViverseEndpoints.WORLD_UPLOAD_FORMAT, sceneSid);
            string fileName = Path.GetFileName(zipFilePath);

            byte[] fileData = null;

#if UNITY_ANDROID && !UNITY_EDITOR
            string requestPath = zipFilePath;
            if (!requestPath.Contains("://"))
            {
                requestPath = "file://" + zipFilePath;
            }
            
            UnityWebRequest fileRequest = UnityWebRequest.Get(requestPath);
            yield return fileRequest.SendWebRequest();
            
            if (fileRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ViversePublish] Failed to read file: {fileRequest.error}");
                
                requestPath = Application.streamingAssetsPath + "/content.zip";

                fileRequest.Dispose();
                fileRequest = UnityWebRequest.Get(requestPath);
                yield return fileRequest.SendWebRequest();
                
                if (fileRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[ViversePublish] Alternative path also failed: {fileRequest.error}");
                    OnPublishComplete?.Invoke(false, $"Failed to read file: {fileRequest.error}");
                    fileRequest.Dispose();
                    yield break;
                }
            }
            
            fileData = fileRequest.downloadHandler.data;
            fileRequest.Dispose();
#else
            if (!File.Exists(zipFilePath))
            {
                Debug.LogError($"[ViversePublish] File not found: {zipFilePath}");
                OnPublishComplete?.Invoke(false, $"File not found: {zipFilePath}");
                yield break;
            }
            fileData = File.ReadAllBytes(zipFilePath);
#endif

            if (fileData == null || fileData.Length == 0)
            {
                Debug.LogError("[ViversePublish] File data is null or empty");
                OnPublishComplete?.Invoke(false, "File is empty or could not be read");
                yield break;
            }

            var metaPayload = new MetaDataPayload
            {
                source = "openBrush",
                iframe_settings = new IframeSettings
                {
                    sandbox = DefaultSandboxPermissions,
                    allow = DefaultAllowPermissions
                }
            };

            string metaJson = JsonUtility.ToJson(metaPayload);

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, "application/zip"),
                new MultipartFormDataSection("meta", metaJson)
            };

            UnityWebRequest request = UnityWebRequest.Post(url, formData);
            request.SetRequestHeader("AccessToken", m_AccessToken);
            request.SetRequestHeader("X-Htc-Auth-Client", ViverseEndpoints.CLIENT_ID);


            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                OnUploadProgress?.Invoke(request.uploadProgress);
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"{request.error} - {request.downloadHandler.text}";
                OnPublishComplete?.Invoke(false, $"Upload failed: {error}");
            }
            else
            {
                string responseText = request.downloadHandler.text;
                OnPublishComplete?.Invoke(true, "World published successfully!");
            }

            request.Dispose();
        }

        public bool IsAuthenticated()
        {
            // Get token from OAuth2Identity instead of stored copy
            if (App.ViveIdentity != null)
            {
                return App.ViveIdentity.HasAccessToken;
            }
            return false;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (App.ViveIdentity != null)
            {
                return await App.ViveIdentity.GetAccessToken();
            }
            return null;
        }

        public string GetLastSceneSid()
        {
            return m_LastSceneSid ?? PlayerPrefs.GetString("viverse_scene_sid", "");
        }

        public WorldContentResponse GetLastResponse()
        {
            return m_LastResponse;
        }

        void OnDestroy()
        {
            if (m_AuthManager != null)
            {
                m_AuthManager.OnAuthComplete -= OnAuthSuccess;
            }
        }
    }

    [Serializable]
    public class MetaDataPayload
    {
        public string source;
        public IframeSettings iframe_settings;
    }

    [Serializable]
    public class IframeSettings
    {
        public string[] sandbox;
        public string[] allow;
    }

    [Serializable]
    public class WorldContentPayload
    {
        public string title;
        public string description_plaintext;
        public string[] preferred_devices;
        public string tags;
    }

    [Serializable]
    public class WorldContentResponse
    {
        public string scene_sid;
        public string hub_sid;
        public string preview_url;
        public string publish_url;
    }
} // namespace TiltBrush
