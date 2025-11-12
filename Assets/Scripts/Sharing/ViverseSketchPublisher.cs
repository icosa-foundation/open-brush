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
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using ICSharpCode.SharpZipLib.Zip;

namespace TiltBrush
{
    public class ViverseSketchPublisher : MonoBehaviour
    {
        [Header("VIVERSE Settings")]
        [Tooltip("Your VIVERSE app ID (from VIVERSE Studio)")]
        public string appId = "syz2h3yf9u";

        private ViversePublishManager m_PublishManager;
        private string m_TempDirectory;

        public event Action<bool, string> OnPublishComplete;
        public event Action<float> OnExportProgress;
        public event Action<float> OnUploadProgress;

        void Start()
        {
            m_PublishManager = FindObjectOfType<ViversePublishManager>();

            if (m_PublishManager != null)
            {
                m_PublishManager.OnPublishComplete += HandlePublishComplete;
                m_PublishManager.OnUploadProgress += HandleUploadProgress;
            }

            #if UNITY_ANDROID && !UNITY_EDITOR
            // Use external storage on Android (accessible via file browser)
            m_TempDirectory = Path.Combine(Application.persistentDataPath, "ViverseExports");
            #else
            m_TempDirectory = Path.Combine(Application.temporaryCachePath, "ViverseExports");
            #endif
            
            if (!Directory.Exists(m_TempDirectory))
            {
                Directory.CreateDirectory(m_TempDirectory);
            }
            
            Debug.Log($"[ViverseSketch] Export directory: {m_TempDirectory}");
        }

        public void PublishCurrentSketch(string title, string description = null)
        {
            if (m_PublishManager == null)
            {
                Debug.LogError("[ViverseSketch] ViversePublishManager not found!");
                OnPublishComplete?.Invoke(false, "ViversePublishManager not found");
                return;
            }

            if (!m_PublishManager.IsAuthenticated())
            {
                Debug.LogError("[ViverseSketch] Not authenticated!");
                OnPublishComplete?.Invoke(false, "Please login first");
                return;
            }

            if (SketchMemoryScript.m_Instance.StrokeCount == 0)
            {
                Debug.LogWarning("[ViverseSketch] No strokes to export!");
                OnPublishComplete?.Invoke(false, "No strokes to export. Draw something first!");
                return;
            }

            StartCoroutine(ExportAndPublishCoroutine(title, description));
        }

        private IEnumerator ExportAndPublishCoroutine(string title, string description)
        {
            Debug.Log($"[ViverseSketch] Starting export: {title}");
            Debug.Log($"[ViverseSketch] Stroke count: {SketchMemoryScript.m_Instance.StrokeCount}");
            OnExportProgress?.Invoke(0.1f);

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string exportDir = Path.Combine(m_TempDirectory, $"sketch_{timestamp}");

            string glbDir = Path.Combine(exportDir, "assets");
            string glbPath = Path.Combine(glbDir, "scene.glb");
            string htmlPath = Path.Combine(exportDir, "index.html");
            string zipPath = Path.Combine(m_TempDirectory, $"sketch_{timestamp}.zip");

            Directory.CreateDirectory(exportDir);
            Directory.CreateDirectory(glbDir);

            Debug.Log($"[ViverseSketch] Export directory: {exportDir}");
            OnExportProgress?.Invoke(0.15f);
            
            // Create world and get scene SID
            string sceneSid = null;
            bool createSuccess = false;

            yield return StartCoroutine(CreateWorldAndGetSceneSid(title, description, (success, sid, error) =>
            {
                createSuccess = success;
                sceneSid = sid;
                if (!success)
                {
                    Debug.LogError($"[ViverseSketch] Failed to create world: {error}");
                }
            }));

            if (!createSuccess || string.IsNullOrEmpty(sceneSid))
            {
                OnPublishComplete?.Invoke(false, "Failed to create world content");
                CleanupDirectory(exportDir);
                yield break;
            }

            Debug.Log($"[ViverseSketch] World created with scene_sid: {sceneSid}");
            OnExportProgress?.Invoke(0.2f);

            // Copy WebViewer files
            bool copySuccess = false;
            
            #if UNITY_ANDROID && !UNITY_EDITOR
            // On Android: Extract pre-packed WebViewer.zip
            Debug.Log("[ViverseSketch] Extracting WebViewer from zip...");
            yield return StartCoroutine(ExtractWebViewerZip(exportDir, success =>
            {
                copySuccess = success;
            }));
            #else
            // On other platforms: Copy from StreamingAssets normally
            try
            {
                Debug.Log("[ViverseSketch] Copying WebViewer files...");
                string streamingWebViewerPath = Path.Combine(Application.streamingAssetsPath, "WebViewer");
                
                if (Directory.Exists(streamingWebViewerPath))
                {
                    CopyDirectory(streamingWebViewerPath, exportDir);
                    Debug.Log("[ViverseSketch] WebViewer files copied");
                    copySuccess = true;
                }
                else
                {
                    Debug.LogError($"[ViverseSketch] WebViewer folder not found at: {streamingWebViewerPath}");
                    copySuccess = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViverseSketch] Failed to copy WebViewer: {ex}");
                copySuccess = false;
            }
            #endif

            if (!copySuccess)
            {
                OnPublishComplete?.Invoke(false, "Failed to copy WebViewer files");
                CleanupDirectory(exportDir);
                yield break;
            }

            OnExportProgress?.Invoke(0.3f);

            // Export GLB
            bool exportSuccess = false;
            string exportError = null;

            try
            {
                exportSuccess = ExportSketchAsGLB(glbPath, out exportError);
                OnExportProgress?.Invoke(0.5f);
            }
            catch (Exception ex)
            {
                exportError = ex.Message;
                Debug.LogError($"[ViverseSketch] Export failed: {ex}");
            }

            if (!exportSuccess)
            {
                OnPublishComplete?.Invoke(false, $"Export failed: {exportError}");
                CleanupDirectory(exportDir);
                yield break;
            }

            FileInfo glbInfo = new FileInfo(glbPath);
            Debug.Log($"[ViverseSketch] GLB created: {glbInfo.Length / 1024}KB");

            // Generate HTML
            try
            {
                Debug.Log($"[ViverseSketch] Generating HTML viewer with app ID: {sceneSid}");
                const string kRelativeGlbPath = "./assets/scene.glb";
                string htmlContent = ViewerHTMLGenerator.GenerateViewerHTML(kRelativeGlbPath, sceneSid);
                File.WriteAllText(htmlPath, htmlContent);
                Debug.Log($"[ViverseSketch] HTML created");
                OnExportProgress?.Invoke(0.6f);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViverseSketch] HTML generation failed: {ex}");
                OnPublishComplete?.Invoke(false, $"HTML generation failed: {ex.Message}");
                CleanupDirectory(exportDir);
                yield break;
            }

            // Create ZIP using SharpZipLib
            bool zipSuccess = false;

            try
            {
                Debug.Log("[ViverseSketch] Creating ZIP package using SharpZipLib...");
                CreateZipFromDirectory(exportDir, zipPath);
                OnExportProgress?.Invoke(0.8f);
                CleanupDirectory(exportDir);

                FileInfo zipInfo = new FileInfo(zipPath);
                Debug.Log($"[ViverseSketch] ZIP created: {zipInfo.Length / 1024}KB");
                zipSuccess = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViverseSketch] ZIP creation failed: {ex}");
                OnPublishComplete?.Invoke(false, $"Failed to package: {ex.Message}");
                CleanupDirectory(exportDir);
                yield break;
            }

            if (!zipSuccess)
            {
                OnPublishComplete?.Invoke(false, "Failed to create package");
                CleanupDirectory(exportDir);
                yield break;
            }

            // Upload
            yield return StartCoroutine(UploadToWorld(sceneSid, zipPath));

            if (File.Exists(zipPath))
            {
                Debug.Log($"[ViverseSketch] Export ZIP kept at: {zipPath}");
            }
        }

        private IEnumerator ExtractWebViewerZip(string destDir, Action<bool> callback)
        {
            string zipPath = Path.Combine(Application.streamingAssetsPath, "WebViewer.zip");
            
            using (UnityWebRequest www = UnityWebRequest.Get(zipPath))
            {
                yield return www.SendWebRequest();
                
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[ViverseSketch] Failed to load WebViewer.zip: {www.error}");
                    callback?.Invoke(false);
                    yield break;
                }

                byte[] zipData = www.downloadHandler.data;
                string tempZip = Path.Combine(Application.temporaryCachePath, "temp_webviewer.zip");
                
                try
                {
                    File.WriteAllBytes(tempZip, zipData);

                    // Extract using SharpZipLib
                    using (ZipInputStream zipStream = new ZipInputStream(File.OpenRead(tempZip)))
                    {
                        ZipEntry entry;
                        while ((entry = zipStream.GetNextEntry()) != null)
                        {
                            string entryPath = Path.Combine(destDir, entry.Name);
                            string dirPath = Path.GetDirectoryName(entryPath);
                            
                            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                            {
                                Directory.CreateDirectory(dirPath);
                            }

                            if (!entry.IsDirectory && !string.IsNullOrEmpty(entry.Name))
                            {
                                using (FileStream streamWriter = File.Create(entryPath))
                                {
                                    byte[] buffer = new byte[4096];
                                    int bytesRead;
                                    while ((bytesRead = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        streamWriter.Write(buffer, 0, bytesRead);
                                    }
                                }
                            }
                        }
                    }

                    File.Delete(tempZip);
                    Debug.Log("[ViverseSketch] WebViewer extracted successfully");
                    callback?.Invoke(true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ViverseSketch] Failed to extract WebViewer.zip: {ex}");
                    if (File.Exists(tempZip)) File.Delete(tempZip);
                    callback?.Invoke(false);
                }
            }
        }

        private IEnumerator CreateWorldAndGetSceneSid(string title, string description, Action<bool, string, string> callback)
        {
            if (string.IsNullOrEmpty(description))
            {
                description = $"OpenBrush sketch - {SketchMemoryScript.m_Instance.StrokeCount} strokes";
            }

            bool done = false;
            bool success = false;
            string sceneSid = null;
            string error = null;

            StartCoroutine(CallCreateWorld(title, description, (s, sid, err) =>
            {
                success = s;
                sceneSid = sid;
                error = err;
                done = true;
            }));

            yield return new WaitUntil(() => done);
            callback?.Invoke(success, sceneSid, error);
        }

        private IEnumerator CallCreateWorld(string title, string description, Action<bool, string, string> callback)
        {
            var method = typeof(ViversePublishManager).GetMethod("CreateWorldContent",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                var enumerator = (IEnumerator)method.Invoke(m_PublishManager, new object[] { title, description, callback });
                yield return StartCoroutine(enumerator);
            }
            else
            {
                callback?.Invoke(false, null, "Could not access CreateWorldContent method");
            }
        }

        private IEnumerator UploadToWorld(string sceneSid, string zipPath)
        {
            var method = typeof(ViversePublishManager).GetMethod("UploadWorldContent",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                var enumerator = (IEnumerator)method.Invoke(m_PublishManager, new object[] { sceneSid, zipPath });
                yield return StartCoroutine(enumerator);
                OnExportProgress?.Invoke(1.0f);
            }
            else
            {
                OnPublishComplete?.Invoke(false, "Could not access UploadWorldContent method");
            }
        }

        private bool ExportSketchAsGLB(string glbPath, out string error)
        {
            error = null;

            try
            {
                Debug.Log("[ViverseSketch] Starting GLB export...");

                ExportGlTF exporter = new ExportGlTF();

                var result = exporter.ExportBrushStrokes(
                    glbPath,
                    AxisConvention.kGltf2,
                    binary: true,
                    doExtras: true,
                    includeLocalMediaContent: true,
                    gltfVersion: 2,
                    selfContained: true
                );

                if (!result.success)
                {
                    error = "Export failed (see console for details)";
                    return false;
                }

                Debug.Log($"[ViverseSketch] GLB export successful: {result.numTris} triangles");
                if (result.exportedFiles != null && result.exportedFiles.Length > 0)
                {
                    Debug.Log($"[ViverseSketch] Exported files: {string.Join(", ", result.exportedFiles)}");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogError($"[ViverseSketch] Export failed: {ex}");
                return false;
            }
        }

        private void CreateZipFromDirectory(string sourceDir, string zipPath)
        {
            using (FileStream fsOut = File.Create(zipPath))
            using (ZipOutputStream zipStream = new ZipOutputStream(fsOut))
            {
                zipStream.SetLevel(9);
                int folderOffset = sourceDir.Length + (sourceDir.EndsWith("\\") ? 0 : 1);
                CompressFolder(sourceDir, zipStream, folderOffset);
                zipStream.IsStreamOwner = true;
                zipStream.Close();
            }
        }

        private void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset)
        {
            string[] files = Directory.GetFiles(path);

            foreach (string filename in files)
            {
                try
                {
                    if (!File.Exists(filename))
                    {
                        Debug.LogWarning($"[ViverseSketch] Skipping missing file: {filename}");
                        continue;
                    }

                    FileInfo fi = new FileInfo(filename);
                    string entryName = filename.Substring(folderOffset);
                    entryName = ZipEntry.CleanName(entryName);

                    ZipEntry newEntry = new ZipEntry(entryName);
                    newEntry.DateTime = fi.LastWriteTime;

                    zipStream.PutNextEntry(newEntry);

                    byte[] buffer = new byte[4096];
                    using (FileStream streamReader = File.OpenRead(filename))
                    {
                        int bytesRead;
                        while ((bytesRead = streamReader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            zipStream.Write(buffer, 0, bytesRead);
                        }
                    }

                    zipStream.CloseEntry();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ViverseSketch] Failed to add file to zip: {filename} - {ex.Message}");
                }
            }

            string[] folders = Directory.GetDirectories(path);
            foreach (string folder in folders)
            {
                CompressFolder(folder, zipStream, folderOffset);
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        private void CleanupDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                    Debug.Log($"[ViverseSketch] Cleaned up directory: {directory}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ViverseSketch] Failed to cleanup directory: {ex.Message}");
            }
        }

        private void HandlePublishComplete(bool success, string message)
        {
            if (success)
            {
                Debug.Log($"[ViverseSketch] Publish successful: {message}");
            }
            else
            {
                Debug.LogError($"[ViverseSketch] Publish failed: {message}");
            }

            OnPublishComplete?.Invoke(success, message);
        }

        private void HandleUploadProgress(float progress)
        {
            OnUploadProgress?.Invoke(progress);
        }

        public bool IsAuthenticated()
        {
            return m_PublishManager != null && m_PublishManager.IsAuthenticated();
        }

        public string GetLastSceneSid()
        {
            return m_PublishManager != null ? m_PublishManager.GetLastSceneSid() : null;
        }

        public void CleanupOldExports()
        {
            if (!Directory.Exists(m_TempDirectory)) return;

            try
            {
                var files = Directory.GetFiles(m_TempDirectory, "sketch_*.zip");
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ViverseSketch] Failed to delete old export: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ViverseSketch] Failed to cleanup old exports: {ex.Message}");
            }
        }
    }
}