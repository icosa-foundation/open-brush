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

        private SourceDirectory m_SourceDirectory = SourceDirectory.QuillProjects;

        private FileWatcher m_FileWatcher;
        private List<QuillFileInfo> m_Files = new List<QuillFileInfo>();
        private string m_CurrentDirectory;
        private bool m_DirectoryScanRequired;
        private bool m_IsScanningDirectory;
        private string m_SearchText = "";

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
            App.InitQuillLibraryPath();
            App.InitQuillImmPath();
            SetSourceDirectory(m_SourceDirectory);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (m_FileWatcher != null)
            {
                m_FileWatcher.EnableRaisingEvents = false;
                m_FileWatcher.FileChanged -= OnDirectoryChanged;
                m_FileWatcher.FileCreated -= OnDirectoryChanged;
                m_FileWatcher.FileDeleted -= OnDirectoryChanged;
                m_FileWatcher = null;
            }
        }

        private void Update()
        {
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

            if (!Directory.Exists(m_CurrentDirectory))
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
            if (m_FileWatcher != null)
            {
                m_FileWatcher.EnableRaisingEvents = false;
                m_FileWatcher.FileChanged -= OnDirectoryChanged;
                m_FileWatcher.FileCreated -= OnDirectoryChanged;
                m_FileWatcher.FileDeleted -= OnDirectoryChanged;
                m_FileWatcher = null;
            }

            if (!Directory.Exists(m_CurrentDirectory))
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

        private void OnDirectoryChanged(object source, FileSystemEventArgs e)
        {
            m_DirectoryScanRequired = true;
        }

        private IEnumerator<object> ScanDirectory()
        {
            if (m_IsScanningDirectory)
            {
                yield break;
            }

            m_IsScanningDirectory = true;

            var files = new List<QuillFileInfo>();
            if (Directory.Exists(m_CurrentDirectory))
            {
                if (m_SourceDirectory == SourceDirectory.Imm)
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
                }

                if (m_SourceDirectory == SourceDirectory.QuillProjects)
                {
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
                ? App.QuillImmPath()
                : App.QuillLibraryPath();
        }
    }
}
