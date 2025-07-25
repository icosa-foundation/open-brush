﻿// Copyright 2020 The Tilt Brush Authors
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
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TiltBrush
{

    public class ModelCatalog : MonoBehaviour, IReferenceItemCatalog
    {
        static public ModelCatalog m_Instance;

        [SerializeField] private string[] m_DefaultModels;

        public event Action CatalogChanged;
        public Material m_ObjLoaderStandardMaterial;
        public Material m_ObjLoaderTransparentMaterial;
        public Material m_ObjLoaderPointCloudMaterial;
        public Material m_ObjLoaderPointCloudInvisibleMaterial;
        [NonSerialized] public Dictionary<string, Model> m_ModelsByRelativePath;

        // Transforms for missing models.
        // One dictionary for the pre-m13 format (normalized to unit box about the origin)
        private Dictionary<string, TrTransform[]> m_MissingNormalizedModelsByRelativePath;
        // The other is post-m13 and contains raw transforms (original model's pivot and size)
        private Dictionary<string, TrTransform[]> m_MissingModelsByRelativePath;

        private Dictionary<string, List<string>> m_OrderedModelNames;
        private bool m_FolderChanged;
        private FileWatcher m_FileWatcher;
        private string m_CurrentModelsDirectory;
        public string CurrentModelsDirectory => m_CurrentModelsDirectory;
        private string m_ChangedFile;
        private bool m_RecurseDirectories = false;

        public bool IsScanning
        {
            get { return false; } // ModelCatalog has no background processing.
        }

        public int ItemCount
        {
            get { return m_OrderedModelNames[m_CurrentModelsDirectory].Count; }
        }

        public IEnumerable<TiltModels75> MissingModels
        {
            get
            {
                var missingModels = m_MissingModelsByRelativePath.Select(e => new TiltModels75
                {
                    FilePath = e.Key,
                    Transforms = m_MissingNormalizedModelsByRelativePath.ContainsKey(e.Key) ?
                        m_MissingNormalizedModelsByRelativePath[e.Key] : null,
                    RawTransforms = e.Value
                });
                var missingNormalizedModels = m_MissingNormalizedModelsByRelativePath.Select(e =>
                    m_MissingModelsByRelativePath.ContainsKey(e.Key) ? null :
                        new TiltModels75
                        {
                            FilePath = e.Key,
                            Transforms = e.Value
                        }).Where(m => m != null);
                return missingModels.Concat(missingNormalizedModels);
            }
        }

        void Awake()
        {
            m_Instance = this;
            Init();
        }

        public void Init()
        {
            App.InitMediaLibraryPath();
            App.InitModelLibraryPath(m_DefaultModels);
            m_ModelsByRelativePath = new Dictionary<string, Model>();
            m_MissingNormalizedModelsByRelativePath = new Dictionary<string, TrTransform[]>();
            m_MissingModelsByRelativePath = new Dictionary<string, TrTransform[]>();
            m_OrderedModelNames = new Dictionary<string, List<string>>();
            ChangeDirectory(HomeDirectory);
        }

        public void ChangeDirectory(string newPath)
        {
            m_CurrentModelsDirectory = newPath;

            if (Directory.Exists(m_CurrentModelsDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentModelsDirectory);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnChanged;
                m_FileWatcher.FileCreated += OnChanged;
                m_FileWatcher.FileDeleted += OnChanged;
                m_FileWatcher.EnableRaisingEvents = true;
            }

            LoadModelsForNewDirectory(m_CurrentModelsDirectory);
        }

        public string HomeDirectory => App.ModelLibraryPath();
        public bool IsHomeDirectory() => m_CurrentModelsDirectory == HomeDirectory;

        public bool IsSubDirectoryOfHome()
        {
            return m_CurrentModelsDirectory.StartsWith(HomeDirectory);
        }

        public string GetCurrentDirectory()
        {
            return m_CurrentModelsDirectory;
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            m_FolderChanged = true;

            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                m_ChangedFile = e.FullPath;
            }
            else
            {
                m_ChangedFile = null;
            }
        }

        public void ClearMissingModels()
        {
            m_MissingNormalizedModelsByRelativePath.Clear();
            m_MissingModelsByRelativePath.Clear();
        }

        public void AddMissingModel(
            string relativePath, TrTransform[] xfs, TrTransform[] rawXfs)
        {
            if (xfs != null)
            {
                m_MissingNormalizedModelsByRelativePath[relativePath] = xfs;
            }
            if (rawXfs != null)
            {
                m_MissingModelsByRelativePath[relativePath] = rawXfs;
            }
        }

        public void PrintMissingModelWarnings()
        {
            var missing =
                m_MissingModelsByRelativePath.Keys.Concat(m_MissingNormalizedModelsByRelativePath.Keys).Distinct().ToList();
            if (!missing.Any()) { return; }
            ControllerConsoleScript.m_Instance.AddNewLine("Models not found!", true);
            foreach (var name in missing)
            {
                ControllerConsoleScript.m_Instance.AddNewLine(name);
            }
        }

        public Model GetModelAtIndex(int i)
        {
            return m_ModelsByRelativePath[m_OrderedModelNames[m_CurrentModelsDirectory][i]];
        }

        public void LoadModels()
        {
            var oldModels = new Dictionary<string, Model>(m_ModelsByRelativePath);

            // If we changed a file, pretend like we don't have it.
            if (m_ChangedFile != null)
            {
                if (oldModels.ContainsKey(m_ChangedFile))
                {
                    oldModels.Remove(m_ChangedFile);
                }
                m_ChangedFile = null;
            }

            m_ModelsByRelativePath.Clear();
            ProcessDirectory(m_CurrentModelsDirectory, oldModels);

            if (oldModels.Count > 0)
            {
                foreach (var entry in oldModels)
                {
                    // Verified that destroy a gameObject removes all children transforms,
                    // all components, and most importantly all textures no longer used by the destroyed objects
                    if (entry.Value.m_ModelParent != null)
                    {
                        Destroy(entry.Value.m_ModelParent.gameObject);
                    }
                }
                Resources.UnloadUnusedAssets();
            }

            m_OrderedModelNames[m_CurrentModelsDirectory] = m_ModelsByRelativePath.Keys.ToList();
            m_OrderedModelNames[m_CurrentModelsDirectory].Sort();

            foreach (string relativePath in m_OrderedModelNames[m_CurrentModelsDirectory])
            {
                if (m_MissingModelsByRelativePath.ContainsKey(relativePath))
                {
                    ModelWidget.CreateModelsFromRelativePath(
                        relativePath, null, null, m_MissingModelsByRelativePath[relativePath], null, null, null);
                    m_MissingModelsByRelativePath.Remove(relativePath);
                }
                if (m_MissingNormalizedModelsByRelativePath.ContainsKey(relativePath))
                {
                    ModelWidget.CreateModelsFromRelativePath(
                        relativePath, null, m_MissingNormalizedModelsByRelativePath[relativePath], null, null, null, null);
                    m_MissingModelsByRelativePath.Remove(relativePath);
                }
            }

            m_FolderChanged = false;

            if (CatalogChanged != null)
            {
                CatalogChanged();
            }
        }

        public void LoadModelsForNewDirectory(string path)
        {
            var oldModels = new Dictionary<string, Model>(m_ModelsByRelativePath);
            ProcessDirectory(path, oldModels);
            // Convert directory to a path relative to HomeDirectory
            var modelsInDirectory = m_ModelsByRelativePath.Keys.Where(m =>
            {
                var dirPath = Path.GetDirectoryName(Path.Join(HomeDirectory, m));
                return dirPath == path;
            }).ToList();
            modelsInDirectory.Sort();
            m_OrderedModelNames[path] = modelsInDirectory;

            foreach (string relativePath in m_OrderedModelNames[path])
            {
                if (m_MissingModelsByRelativePath.ContainsKey(relativePath))
                {
                    ModelWidget.CreateModelsFromRelativePath(
                        relativePath, null, m_MissingModelsByRelativePath[relativePath], null, null, null, null);
                    m_MissingModelsByRelativePath.Remove(relativePath);
                }
                if (m_MissingNormalizedModelsByRelativePath.ContainsKey(relativePath))
                {
                    ModelWidget.CreateModelsFromRelativePath(
                        relativePath, null, m_MissingNormalizedModelsByRelativePath[relativePath], null, null, null, null);
                    m_MissingModelsByRelativePath.Remove(relativePath);
                }
            }
            if (CatalogChanged != null)
            {
                CatalogChanged();
            }
        }

        public void ForceCatalogScan()
        {
            LoadModels();
            if (CatalogChanged != null)
            {
                CatalogChanged();
            }
        }

        void Update()
        {
            if (m_FolderChanged)
            {
                ForceCatalogScan();
            }
        }

        void ProcessDirectory(string sPath, Dictionary<string, Model> oldModels)
        {
            if (Directory.Exists(sPath))
            {
                string[] aFiles = Directory.GetFiles(sPath);
                // Models we download from Poly are called ".gltf2", but ".gltf" is more standard
                List<string> extensions = new() { ".gltf2", ".gltf", ".glb", ".ply", ".svg", ".obj" };

#if USD_SUPPORTED
                extensions.AddRange(new [] { ".usda", ".usdc", ".usd" });
#endif
#if FBX_SUPPORTED
                extensions.Add( ".fbx" );
#endif

                for (int i = 0; i < aFiles.Length; ++i)
                {
                    string sExtension = Path.GetExtension(aFiles[i]).ToLower();
                    if (extensions.Contains(sExtension))
                    {
                        Model rNewModel;
                        string path = aFiles[i].Replace("\\", "/");
                        try
                        {
                            rNewModel = oldModels[path];
                            oldModels.Remove(path);
                        }
                        catch (KeyNotFoundException)
                        {
                            rNewModel = new Model(WidgetManager.GetModelSubpath(path));
                        }
                        // Should we skip this loop earlier if m_ModelsByRelativePath already contains the key?
                        m_ModelsByRelativePath.TryAdd(rNewModel.RelativePath, rNewModel);
                    }
                }

                // We used to recurse for models but we now have directory navigation
                // I'm keeping this around for now
                if (m_RecurseDirectories)
                {
                    string[] aSubdirectories = Directory.GetDirectories(sPath);
                    for (int i = 0; i < aSubdirectories.Length; ++i)
                    {
                        ProcessDirectory(aSubdirectories[i], oldModels);
                    }
                }
            }
        }

        /// GetModel, for .tilt files written by TB 7.5 and up
        /// Paths are always relative to Media Library/, unless someone hacked the tilt file
        /// in which case we ignore the model.
        public Model GetModel(string relativePath)
        {
            Model m;
            m_ModelsByRelativePath.TryGetValue(relativePath, out m);
            if (m == null)
            {
                // The directory probably hasn't been processed yet
                string relativeDirPath = Path.GetDirectoryName(relativePath);
                LoadModelsForNewDirectory(Path.Combine(HomeDirectory, relativeDirPath));
                m_ModelsByRelativePath.TryGetValue(relativePath, out m);
            }
            return m;
        }
    }
} // namespace TiltBrush
