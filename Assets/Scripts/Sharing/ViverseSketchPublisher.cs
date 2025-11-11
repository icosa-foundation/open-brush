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
using System.IO.Compression;
using UnityEngine;

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

            m_TempDirectory = Path.Combine(Application.temporaryCachePath, "ViverseExports");
            if (!Directory.Exists(m_TempDirectory))
            {
                Directory.CreateDirectory(m_TempDirectory);
            }
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

            // GLB and associated assets (brushes) go into the 'assets' folder
            string glbDir = Path.Combine(exportDir, "assets");
            string glbPath = Path.Combine(glbDir, "scene.glb");

            string htmlPath = Path.Combine(exportDir, "index.html");
            string zipPath = Path.Combine(m_TempDirectory, $"sketch_{timestamp}.zip");

            Directory.CreateDirectory(exportDir);
            Directory.CreateDirectory(glbDir); // Creates the assets folder

            Debug.Log($"[ViverseSketch] Export directory: {exportDir}");
            OnExportProgress?.Invoke(0.15f);
            
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

            // Copy libs and assets FIRST (before GLB export)
            bool copySuccess = false;
            string copyError = null;

            try
            {
                Debug.Log("[ViverseSketch] Copying libs and assets...");

                string streamingWebViewerPath = Path.Combine(Application.streamingAssetsPath, "WebViewer");

                // Copy libs folder
                string sourceLibsPath = Path.Combine(streamingWebViewerPath, "libs");
                string destLibsPath = Path.Combine(exportDir, "libs");
                if (Directory.Exists(sourceLibsPath))
                {
                    CopyDirectory(sourceLibsPath, destLibsPath);
                    Debug.Log($"[ViverseSketch] Libs copied");
                }
                else
                {
                    Debug.LogWarning($"[ViverseSketch] Libs folder not found at: {sourceLibsPath}");
                }

                // Copy static assets (icons, fonts, etc.)
                string sourceAssetsPath = Path.Combine(streamingWebViewerPath, "assets");
                string destAssetsPath = Path.Combine(exportDir, "assets");
                if (Directory.Exists(sourceAssetsPath))
                {
                    CopyDirectory(sourceAssetsPath, destAssetsPath);
                    Debug.Log($"[ViverseSketch] Static assets copied");
                }
                else
                {
                    Debug.LogWarning($"[ViverseSketch] Assets folder not found at: {sourceAssetsPath}");
                }

                copySuccess = true;
                OnExportProgress?.Invoke(0.3f);
            }
            catch (Exception ex)
            {
                copyError = ex.Message;
                Debug.LogError($"[ViverseSketch] Failed to copy libs/assets: {ex}");
            }

            if (!copySuccess)
            {
                OnPublishComplete?.Invoke(false, $"Failed to copy dependencies: {copyError}");
                CleanupDirectory(exportDir);
                yield break;
            }
            string brushesSourcePath = Path.Combine(Application.dataPath, "..", "Support", "TiltBrush.com", "shaders", "brushes");
            if (Directory.Exists(brushesSourcePath))
            {
                string brushesDestPath = Path.Combine(exportDir, "assets", "brushes");
                CopyDirectory(brushesSourcePath, brushesDestPath);
                Debug.Log("[ViverseSketch] Brush textures copied");
            }

            // Export GLB (after assets are copied)
            bool exportSuccess = false;
            string exportError = null;

            try
            {
                // Export GLB and its brush assets into glbDir (i.e., exportDir/assets)
                // This will add scene.glb and brush shaders to the existing assets folder
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
            bool htmlSuccess = false;
            string htmlError = null;

            try
            {
                Debug.Log($"[ViverseSketch] Generating HTML viewer with app ID: {sceneSid}");

                // GLB path must be relative to index.html in the root
                const string kRelativeGlbPath = "./assets/scene.glb";

                string htmlContent = ViewerHTMLGenerator.GenerateViewerHTML(kRelativeGlbPath, sceneSid);

                File.WriteAllText(htmlPath, htmlContent);
                Debug.Log($"[ViverseSketch] HTML created");
                htmlSuccess = true;
                OnExportProgress?.Invoke(0.6f);
            }
            catch (Exception ex)
            {
                htmlError = ex.Message;
                Debug.LogError($"[ViverseSketch] HTML generation failed: {ex}");
            }

            if (!htmlSuccess)
            {
                OnPublishComplete?.Invoke(false, $"HTML generation failed: {htmlError}");
                CleanupDirectory(exportDir);
                yield break;
            }

            // Create ZIP and Upload
            bool zipSuccess = false;
            string zipError = null;

            try
            {
                Debug.Log("[ViverseSketch] Creating ZIP package...");

                using (var zip = new ZipArchive(File.Create(zipPath), ZipArchiveMode.Create))
                {
                    AddDirectoryToZip(zip, exportDir, "");
                }

                OnExportProgress?.Invoke(0.8f);
                CleanupDirectory(exportDir);

                FileInfo zipInfo = new FileInfo(zipPath);
                Debug.Log($"[ViverseSketch] ZIP created: {zipInfo.Length / 1024}KB");

                zipSuccess = true;
            }
            catch (Exception ex)
            {
                zipError = ex.Message;
                Debug.LogError($"[ViverseSketch] ZIP creation failed: {ex}");
            }

            if (!zipSuccess)
            {
                OnPublishComplete?.Invoke(false, $"Failed to package: {zipError}");
                CleanupDirectory(exportDir);
                yield break;
            }

            // Upload
            yield return StartCoroutine(UploadToWorld(sceneSid, zipPath));

            // Cleanup ZIP file
            if (File.Exists(zipPath))
            {
                try
                {
                    File.Delete(zipPath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ViverseSketch] Failed to delete ZIP: {ex.Message}");
                }
            }
        }
        
        // Helpers
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

            m_PublishManager.OnPublishComplete += (s, msg) =>
            {
            };

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
                    doExtras: false,
                    includeLocalMediaContent: true,
                    gltfVersion: 2,
                    selfContained: true
                );

                if (!result.success)
                {
                    error = "Export failed (see console for details)";
                    return false;
                }

                Debug.Log($"[ViverseSketch] GLB export successful! {result.numTris} triangles");
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

        private void AddDirectoryToZip(ZipArchive zip, string sourceDir, string entryPrefix)
        {
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                try
                {
                    string fileName = Path.GetFileName(file);
                    string entryName = string.IsNullOrEmpty(entryPrefix) ? fileName : Path.Combine(entryPrefix, fileName);
                    zip.CreateEntryFromFile(file, entryName.Replace("\\", "/"));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ViverseSketch] Skipping file: {Path.GetFileName(file)} - {ex.Message}");
                }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                try
                {
                    string dirName = Path.GetFileName(subDir);
                    string newPrefix = string.IsNullOrEmpty(entryPrefix) ? dirName : Path.Combine(entryPrefix, dirName);
                    AddDirectoryToZip(zip, subDir, newPrefix);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ViverseSketch] Skipping directory: {Path.GetFileName(subDir)} - {ex.Message}");
                }
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
                var files = Directory.GetFiles(m_TempDirectory, "sketch_*.*");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if ((DateTime.Now - fileInfo.CreationTime).TotalHours > 1)
                    {
                        File.Delete(file);
                        Debug.Log($"[ViverseSketch] Cleaned up: {file}");
                    }
                }

                var dirs = Directory.GetDirectories(m_TempDirectory, "sketch_*");
                foreach (var dir in dirs)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if ((DateTime.Now - dirInfo.CreationTime).TotalHours > 1)
                    {
                        Directory.Delete(dir, true);
                        Debug.Log($"[ViverseSketch] Cleaned up directory: {dir}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ViverseSketch] Cleanup failed: {ex.Message}");
            }
        }

        void OnDestroy()
        {
            if (m_PublishManager != null)
            {
                m_PublishManager.OnPublishComplete -= HandlePublishComplete;
                m_PublishManager.OnUploadProgress -= HandleUploadProgress;
            }
        }
    }
} // namespace TiltBrush
