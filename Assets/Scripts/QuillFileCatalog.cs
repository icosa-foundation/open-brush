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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace TiltBrush
{
    public class QuillFileCatalog : MonoBehaviour, IReferenceItemCatalog
    {
        public enum SourceDirectory
        {
            QuillProjects,
            Imm,
        }

        public static QuillFileCatalog Instance { get; private set; }

        [SerializeField] private string[] m_DefaultQuillFiles;

        private SourceDirectory m_SourceDirectory = SourceDirectory.QuillProjects;

        private FileWatcher m_FileWatcher;
        private List<QuillFileInfo> m_Files = new List<QuillFileInfo>();
        private string m_CurrentDirectory;
        private bool m_DirectoryScanRequired;
        private bool m_IsScanningDirectory;
        private string m_SearchText = "";
        private string m_SafRootIdentity;
        private bool UsesSaf => m_SourceDirectory == SourceDirectory.Imm &&
            UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework;

        public int ItemCount => m_Files.Count;
        public bool IsScanning => m_IsScanningDirectory;
        public string HomeDirectory => GetDirectoryForSource(m_SourceDirectory);
        public SourceDirectory CurrentSourceDirectory => m_SourceDirectory;
        public string SearchText
        {
            get
            {
                return m_SearchText;
            }
            set
            {
                m_SearchText = value;
                m_DirectoryScanRequired = true;
            }
        }

        public event Action CatalogChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            App.InitMediaLibraryPath();
            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                App.InitQuillMediaLibraryPath(m_DefaultQuillFiles);
            }
            SetSourceDirectory(m_SourceDirectory);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            StopWatchingCurrentDirectory();
        }

        private void Update()
        {
            if (UsesSaf && m_SafRootIdentity != UserStorage.Backend.RootIdentity)
            {
                m_SafRootIdentity = UserStorage.Backend.RootIdentity;
                ChangeDirectory(HomeDirectory);
                m_DirectoryScanRequired = true;
            }
            if (m_DirectoryScanRequired)
            {
                ForceCatalogScan();
            }
        }

        public QuillFileInfo GetFileAtIndex(int index)
        {
            if (index < 0 || index >= m_Files.Count)
            {
                throw new ArgumentException($"Quill catalog has {m_Files.Count} files. Requested index {index}.");
            }

            return m_Files[index];
        }

        public void SetSourceDirectory(SourceDirectory sourceDirectory)
        {
            string targetDirectory = GetDirectoryForSource(sourceDirectory);
            if (m_SourceDirectory == sourceDirectory &&
                string.Equals(m_CurrentDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            m_SourceDirectory = sourceDirectory;
            ChangeDirectory(targetDirectory);
        }

        public void ForceCatalogScan()
        {
            if (!m_IsScanningDirectory)
            {
                m_DirectoryScanRequired = false;
                StartCoroutine(ScanDirectory());
            }
        }

        public void ChangeDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = HomeDirectory;
            }

            m_CurrentDirectory = path;
            m_Files.Clear();

            // Quill's external project folder is only discovered, never created by Open Brush.
            if (!UsesSaf && m_SourceDirectory == SourceDirectory.Imm && !Directory.Exists(m_CurrentDirectory))
            {
                App.InitDirectoryAtPath(m_CurrentDirectory);
            }

            StartWatchingCurrentDirectory();
            ForceCatalogScan();
        }

        public bool IsHomeDirectory()
        {
            return string.Equals(m_CurrentDirectory, HomeDirectory, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsSubDirectoryOfHome()
        {
            if (string.IsNullOrEmpty(m_CurrentDirectory))
            {
                return false;
            }

            return m_CurrentDirectory.StartsWith(HomeDirectory, StringComparison.OrdinalIgnoreCase);
        }

        public string GetCurrentDirectory()
        {
            return m_CurrentDirectory;
        }

        private void StartWatchingCurrentDirectory()
        {
            StopWatchingCurrentDirectory();

            if (UsesSaf || !Directory.Exists(m_CurrentDirectory))
            {
                return;
            }

            m_FileWatcher = new FileWatcher(m_CurrentDirectory);
            m_FileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            m_FileWatcher.FileChanged += OnDirectoryChanged;
            m_FileWatcher.FileCreated += OnDirectoryChanged;
            m_FileWatcher.FileDeleted += OnDirectoryChanged;
            m_FileWatcher.EnableRaisingEvents = true;
        }

        private void StopWatchingCurrentDirectory()
        {
            if (m_FileWatcher == null)
            {
                return;
            }

            m_FileWatcher.EnableRaisingEvents = false;
            m_FileWatcher.FileChanged -= OnDirectoryChanged;
            m_FileWatcher.FileCreated -= OnDirectoryChanged;
            m_FileWatcher.FileDeleted -= OnDirectoryChanged;
            m_FileWatcher.Dispose();
            m_FileWatcher = null;
        }

        private void OnDirectoryChanged(object source, FileSystemEventArgs e)
        {
            m_DirectoryScanRequired = true;
        }

        internal static IEnumerator<object> SeedSafDefaults(
            IUserStorageBackend backend, string[] defaults, Func<string, byte[]> loadResource)
        {
            if (!backend.IsReady) { yield break; }
            string root = backend.RootIdentity;
            string key = OpenBrushStorage.GetSafRootScopedPreferenceKey("QuillDefaults.HandledFilesV1", root);
            var handled = DefaultMediaSeeder.GetHandledFiles(PlayerPrefs.GetString(key, ""), false, null);
            foreach (string resourcePath in defaults ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(resourcePath)) { continue; }
                string normalized = resourcePath.Replace('\\', '/');
                if (handled.Contains(normalized)) { continue; }
                if (root != backend.RootIdentity || !backend.IsReady) { yield break; }
                byte[] bytes = loadResource(resourcePath);
                if (bytes == null)
                {
                    Debug.LogWarning($"[SAF_REVIEW_DEFAULT_IMM] Missing default resource: {resourcePath}");
                    continue;
                }
                var write = new Future<SafPublicationResult>(() =>
                {
                    if (root != backend.RootIdentity)
                    {
                        return new SafPublicationResult(StorageResultCode.NotReady, "Selected folder changed.");
                    }
                    string name = Path.GetFileName(normalized);
                    StorageDirectoryResult listing = backend.List(StorageArea.MediaLibraryQuill, "", CancellationToken.None);
                    if (!listing.Success && listing.Code != StorageResultCode.NotFound)
                    {
                        return new SafPublicationResult(listing.Code, listing.Error);
                    }
                    if (listing.Success && listing.Documents.Any(document =>
                        document.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        return new SafPublicationResult(StorageResultCode.Success);
                    }
                    if (root != backend.RootIdentity)
                    {
                        return new SafPublicationResult(StorageResultCode.NotReady, "Selected folder changed.");
                    }
                    using IStorageWriteTransaction transaction = backend.BeginWrite(
                        StorageArea.MediaLibraryQuill, name, "application/octet-stream", CancellationToken.None);
                    using (Stream output = transaction.OpenWrite()) { output.Write(bytes, 0, bytes.Length); }
                    StorageMutationResult commit = transaction.Commit();
                    return new SafPublicationResult(commit.Code, commit.Error);
                }, cleanupFunction: null, longRunning: true);
                SafPublicationResult result;
                while (true)
                {
                    bool finished;
                    try { finished = write.TryGetResult(out result); }
                    catch (FutureFailed e)
                    {
                        write.Close();
                        Debug.LogWarning($"[SAF_REVIEW_DEFAULT_IMM] Could not seed {normalized}: {e.Message}");
                        yield break;
                    }
                    if (finished) { break; }
                    yield return null;
                }
                write.Close();
                if (!result.Success)
                {
                    Debug.LogWarning($"[SAF_REVIEW_DEFAULT_IMM] Could not seed {normalized}: {result.Error}");
                    yield break;
                }
                if (root != backend.RootIdentity) { yield break; }
                handled.Add(normalized);
                PlayerPrefs.SetString(key, string.Join("\n", handled.OrderBy(value => value)));
                PlayerPrefs.Save();
            }
        }

        private IEnumerator<object> ScanDirectory()
        {
            if (m_IsScanningDirectory)
            {
                yield break;
            }

            m_IsScanningDirectory = true;

            var files = new List<QuillFileInfo>();
            if (UsesSaf)
            {
                yield return SeedSafDefaults(UserStorage.Backend, m_DefaultQuillFiles, resourcePath =>
                {
                    TextAsset resource = Resources.Load<TextAsset>(resourcePath);
                    if (resource == null) { return null; }
                    byte[] bytes = resource.bytes;
                    Resources.UnloadAsset(resource);
                    return bytes;
                });
                IUserStorageBackend backend = UserStorage.Backend;
                string rootIdentity = backend.RootIdentity;
                string directory = m_CurrentDirectory;
                string relativeDirectory = Path.GetRelativePath(HomeDirectory, directory);
                var query = new Future<List<QuillFileInfo>>(() =>
                {
                    var result = new List<QuillFileInfo>();
                    StorageDirectoryResult listing = backend.List(StorageArea.MediaLibraryQuill,
                        relativeDirectory == "." ? "" : relativeDirectory.Replace('\\', '/'),
                        CancellationToken.None);
                    if (!listing.Success && listing.Code != StorageResultCode.NotFound)
                    {
                        throw new IOException(listing.Error);
                    }
                    if (!listing.Success) { return result; }
                    foreach (StorageDocument document in listing.Documents)
                    {
                        if (document.IsDirectory || !Path.GetExtension(document.DisplayName)
                                .Equals(".imm", StringComparison.OrdinalIgnoreCase)) { continue; }
                        string path = backend.Materialize(document.DocumentId,
                            MaterializationScope.File, CancellationToken.None);
                        result.Add(QuillFileInfo.FromImmFile(new FileInfo(path)));
                    }
                    return result;
                }, cleanupFunction: null, longRunning: true);
                while (true)
                {
                    bool finished;
                    try { finished = query.TryGetResult(out files); }
                    catch (FutureFailed e)
                    {
                        Debug.LogWarning($"[SAF_REVIEW_IMM] Could not scan IMM library: {e.Message}");
                        m_IsScanningDirectory = false;
                        yield break;
                    }
                    if (finished) { break; }
                    yield return null;
                }
                if (rootIdentity != backend.RootIdentity || directory != m_CurrentDirectory || !UsesSaf)
                {
                    m_IsScanningDirectory = false;
                    m_DirectoryScanRequired = true;
                    yield break;
                }
                files = files.Where(file => string.IsNullOrEmpty(m_SearchText) ||
                    file.DisplayName.IndexOf(m_SearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            else if (Directory.Exists(m_CurrentDirectory))
            {
                foreach (string path in Directory.GetFiles(m_CurrentDirectory, "*.imm", SearchOption.TopDirectoryOnly))
                {
                    if (!string.IsNullOrEmpty(m_SearchText) &&
                        Path.GetFileNameWithoutExtension(path).IndexOf(m_SearchText, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    try
                    {
                        files.Add(QuillFileInfo.FromImmFile(new FileInfo(path)));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Skipping IMM file '{path}': {ex.Message}");
                    }
                }

                foreach (string path in Directory.GetDirectories(m_CurrentDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    if (!string.IsNullOrEmpty(m_SearchText) &&
                        Path.GetFileName(path).IndexOf(m_SearchText, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    try
                    {
                        if (IsQuillProject(path))
                        {
                            files.Add(QuillFileInfo.FromQuillDirectory(new DirectoryInfo(path)));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Skipping Quill project '{path}': {ex.Message}");
                    }
                }
            }

            m_Files = files
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            m_IsScanningDirectory = false;
            CatalogChanged?.Invoke();
        }

        private static bool IsQuillProject(string directoryPath)
        {
            string quillJson = Path.Combine(directoryPath, "Quill.json");
            return File.Exists(quillJson);
        }

        private static string GetDirectoryForSource(SourceDirectory sourceDirectory)
        {
            return sourceDirectory == SourceDirectory.Imm
                ? App.QuillMediaLibraryPath()
                : App.QuillLibraryPath();
        }

        public string GetRandomFile()
        {
            if (m_Files.Count == 0)
            {
                return null;
            }

            var randomFile = m_Files[UnityEngine.Random.Range(0, m_Files.Count)];
            return randomFile.FullPath;
        }
    }
}
