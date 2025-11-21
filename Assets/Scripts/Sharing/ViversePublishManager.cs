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
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{
    public class ViversePublishManager : MonoBehaviour
    {
        private const string WORLD_API_BASE = "https://world-api.viverse.com/api/hubs-cms/v1/standalone";
        
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

        private void OnAuthSuccess(string accessToken, string refreshToken, int expiresIn)
        {
            m_AccessToken = accessToken;
            PlayerPrefs.SetString("viverse_access_token", accessToken);
            PlayerPrefs.SetString("viverse_refresh_token", refreshToken);
            PlayerPrefs.Save();
        }

        private void LoadSavedToken()
        {
            if (PlayerPrefs.HasKey("viverse_access_token"))
            {
                m_AccessToken = PlayerPrefs.GetString("viverse_access_token");
            }
            else if (PlayerPrefs.HasKey("access_token"))
            {
                m_AccessToken = PlayerPrefs.GetString("access_token");
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
            Debug.Log($"[ViversePublish] Starting publish: {title}");
            
            yield return CreateWorldContent(title, description, (success, sceneSid, error) =>
            {
                if (success)
                {
                    m_LastSceneSid = sceneSid;
                    Debug.Log($"[ViversePublish] Content created with scene_sid: {sceneSid}");
                    StartCoroutine(UploadWorldContent(sceneSid, zipFilePath));
                }
                else
                {
                    Debug.LogError($"[ViversePublish] Failed to create content: {error}");
                    OnPublishComplete?.Invoke(false, $"Failed to create content: {error}");
                }
            });
        }

        public IEnumerator CreateWorldContent(string title, string description, Action<bool, string, string> callback)
        {
            string url = $"{WORLD_API_BASE}/contents";
            
            var payload = new WorldContentPayload
            {
                title = title,
                description_plaintext = description
            };
            
            string json = JsonUtility.ToJson(payload);
            Debug.Log($"[ViversePublish] Creating content: {json}");

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
                    Debug.Log($"[ViversePublish] Create response: {responseText}");
                    
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

        public IEnumerator UploadWorldContent(string sceneSid, string zipFilePath)
        {
            string url = $"{WORLD_API_BASE}/contents/{sceneSid}/upload";
            string fileName = Path.GetFileName(zipFilePath);
            
            Debug.Log($"[ViversePublish] Uploading to: {url}");
            Debug.Log($"[ViversePublish] File: {zipFilePath}");
            Debug.Log($"[ViversePublish] Platform: {Application.platform}");

            byte[] fileData = null;

#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[ViversePublish] Using UnityWebRequest for Android");
            
            string requestPath = zipFilePath;
            if (!requestPath.Contains("://"))
            {
                requestPath = "file://" + zipFilePath;
            }
            
            Debug.Log($"[ViversePublish] Request path: {requestPath}");
            
            UnityWebRequest fileRequest = UnityWebRequest.Get(requestPath);
            yield return fileRequest.SendWebRequest();
            
            Debug.Log($"[ViversePublish] Request result: {fileRequest.result}");
            Debug.Log($"[ViversePublish] Request error: {fileRequest.error}");
            
            if (fileRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ViversePublish] Failed to read file: {fileRequest.error}");
                
                Debug.Log("[ViversePublish] Trying alternative path...");
                requestPath = Application.streamingAssetsPath + "/content.zip";
                Debug.Log($"[ViversePublish] Alternative path: {requestPath}");
                
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
            Debug.Log("[ViversePublish] Using File.ReadAllBytes");
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

            Debug.Log($"[ViversePublish] File size: {fileData.Length} bytes ({fileData.Length / 1024.0f / 1024.0f:F2} MB)");

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, fileName, "application/zip"),
                new MultipartFormDataSection("meta", "{\"source\": \"studio\"}")
            };

            UnityWebRequest request = UnityWebRequest.Post(url, formData);
            request.SetRequestHeader("AccessToken", m_AccessToken);

            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                OnUploadProgress?.Invoke(request.uploadProgress);
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"{request.error} - {request.downloadHandler.text}";
                Debug.LogError($"[ViversePublish] Upload failed: {error}");
                OnPublishComplete?.Invoke(false, $"Upload failed: {error}");
            }
            else
            {
                Debug.Log("[ViversePublish] Upload successful!");
                string responseText = request.downloadHandler.text;
                Debug.Log($"[ViversePublish] Upload response: {responseText}");
                
                OnPublishComplete?.Invoke(true, "World published successfully!");
            }

            request.Dispose();
        }

        public bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(m_AccessToken);
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
    public class WorldContentPayload
    {
        public string title;
        public string description_plaintext;
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