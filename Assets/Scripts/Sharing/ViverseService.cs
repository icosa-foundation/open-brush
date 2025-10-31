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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{

    class ViverseService
    {
        public const string kWorldApiHost = "https://world-api.viverse.com";
        public const string kAccountHost = "https://account.htcvive.com";
        
        // API endpoints
        private const string kCreateContentEndpoint = "/api/hubs-cms/v1/standalone/contents";
        private const string kUploadContentEndpoint = "/api/hubs-cms/v1/standalone/contents/{0}/upload";
        
        private readonly OAuth2Identity m_identity;
        
        /// <summary>
        /// Options for creating VIVERSE World content
        /// </summary>
        [Serializable, UsedImplicitly]
        public class Options
        {
            public string title;
            public string description_plaintext;
            public string source = "open-brush";
            
            // Additional optional fields
            public string category;
            public string[] tags;
        }
        
        /// <summary>
        /// Response from creating content
        /// </summary>
        [Serializable, UsedImplicitly]
        public class CreateContentResponse
        {
            public string scene_sid;
            public string hub_sid;
            public string preview_url;
            public string publish_url;
        }
        
        /// <summary>
        /// User profile information
        /// </summary>
        [Serializable, UsedImplicitly]
        public class UserProfile
        {
            public string user_id;
            public string username;
            public string email;
            public string avatar_url;
        }
        
        public ViverseService(OAuth2Identity identity) => m_identity = identity;

        /// <summary>
        /// Get user profile information
        /// </summary>
        public async Task<UserProfile> GetUserInfo()
        {
            // TODO: Replace with actual VIVERSE user info endpoint
            // This is a placeholder - VIVERSE API documentation should specify the correct endpoint
            var result = await new WebRequest(
                $"{kAccountHost}/api/v1/user/profile", 
                m_identity, 
                "GET"
            ).SendAsync();
            
            return result.Deserialize<UserProfile>();
        }
        
        /// <summary>
        /// Create a new content entry in VIVERSE World
        /// </summary>
        public async Task<CreateContentResponse> CreateContent(
            string title,
            string description,
            Options options = null)
        {
            if (options == null)
            {
                options = new Options
                {
                    title = title,
                    description_plaintext = description
                };
            }
            else
            {
                options.title = title;
                options.description_plaintext = description;
            }
            
            string json = JsonConvert.SerializeObject(options);
            
            var request = new UnityWebRequest(
                $"{kWorldApiHost}{kCreateContentEndpoint}", 
                "POST"
            );
            
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            
            // VIVERSE uses "AccessToken" header instead of "Authorization: Bearer"
            string accessToken = await m_identity.GetAccessToken();
            request.SetRequestHeader("AccessToken", accessToken);
            request.SetRequestHeader("Content-Type", "application/json");
            
            await request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new VrAssetServiceException(
                    $"Failed to create VIVERSE content: {request.error}",
                    request.downloadHandler.text
                );
            }
            
            string responseText = request.downloadHandler.text;
            request.Dispose();
            
            return JsonUtility.FromJson<CreateContentResponse>(responseText);
        }
        
        /// <summary>
        /// Upload content zip file to VIVERSE World
        /// </summary>
        public async Task UploadContent(
            string sceneSid,
            string zipPath,
            IProgress<double> progress,
            CancellationToken token)
        {
            string uploadUrl = string.Format($"{kWorldApiHost}{kUploadContentEndpoint}", sceneSid);
            
            // Load file data
            byte[] fileData = await LoadFileDataAsync(zipPath, token);
            
            if (fileData == null || fileData.Length == 0)
            {
                throw new VrAssetServiceException("File is empty or couldn't be read");
            }
            
            Debug.Log($"[ViverseService] Uploading {fileData.Length} bytes to {uploadUrl}");
            
            // Create multipart form
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", fileData, Path.GetFileName(zipPath), "application/zip"),
                new MultipartFormDataSection("meta", "{\"source\": \"open-brush\"}")
            };
            
            // Get access token
            string accessToken = await m_identity.GetAccessToken();
            
            // Create and send request
            UnityWebRequest request = UnityWebRequest.Post(uploadUrl, formData);
            request.SetRequestHeader("AccessToken", accessToken);
            
            // Track progress
            var uploadOperation = request.SendWebRequest();
            
            while (!uploadOperation.isDone)
            {
                token.ThrowIfCancellationRequested();
                
                float progressValue = request.uploadProgress;
                progress?.Report(progressValue);
                
                await Task.Yield();
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorDetails = request.downloadHandler?.text ?? "No error details";
                request.Dispose();
                throw new VrAssetServiceException(
                    $"Upload failed: {request.error}",
                    errorDetails
                );
            }
            
            Debug.Log($"[ViverseService] Upload successful: {request.downloadHandler.text}");
            request.Dispose();
        }
        
        /// <summary>
        /// Load file data, handling both Android and PC platforms
        /// </summary>
        private async Task<byte[]> LoadFileDataAsync(string filePath, CancellationToken token)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, StreamingAssets requires UnityWebRequest
            UnityWebRequest fileRequest = UnityWebRequest.Get(filePath);
            await fileRequest.SendWebRequest();
            
            token.ThrowIfCancellationRequested();
            
            if (fileRequest.result != UnityWebRequest.Result.Success)
            {
                throw new VrAssetServiceException($"Failed to read file: {fileRequest.error}");
            }
            
            byte[] data = fileRequest.downloadHandler.data;
            fileRequest.Dispose();
            return data;
#else
            // On PC/Editor, direct file read
            if (!File.Exists(filePath))
            {
                throw new VrAssetServiceException($"File not found: {filePath}");
            }
            
            return await Task.Run(() => File.ReadAllBytes(filePath), token);
#endif
        }
    }
} // namespace TiltBrush