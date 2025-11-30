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
        public Material m_VoxLoaderStandardMaterial;
        [NonSerialized] public Dictionary<string, Model> m_ModelsByRelativePath;

        // Transforms for missing models.
        // One dictionary for the pre-m13 format (normalized to unit box about the origin)
        private Dictionary<string, TrTransform[]> m_MissingNormalizedModelsByRelativePath;
        // The other is post-m13 and contains raw transforms (original model's pivot and size)
        private Dictionary<string, TrTransform[]> m_MissingModelsByRelativePath;

        private Dictionary<string, List<string>> m_OrderedModelNames;
        private bool m_FolderChanged;
        private List<FileWatcher> m_FileWatchers;
        private string m_CurrentModelsDirectory;
        public string CurrentModelsDirectory => m_CurrentModelsDirectory;
        private string m_ChangedFile;
        private bool m_RecurseDirectories = false;
        private Dictionary<string, string> m_ModelRootsByRelativePath;

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
            m_ModelRootsByRelativePath = new Dictionary<string, string>();
            ChangeDirectory(HomeDirectory);
        }

        private IEnumerable<string> GetModelDirectories()
        {
            return new List<string> { App.ModelLibraryPath(), App.BlocksModelLibraryPath() }
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct();
        }

        private string GetModelRoot(string path)
        {
            return GetModelDirectories()
                .FirstOrDefault(directory => path.StartsWith(directory, StringComparison.OrdinalIgnoreCase));
        }

        public void ChangeDirectory(string newPath)
        {
            m_CurrentModelsDirectory = newPath;

            if (m_FileWatchers != null)
            {
                foreach (var watcher in m_FileWatchers)
                {
                    watcher.FileChanged -= OnChanged;
                    watcher.FileCreated -= OnChanged;
                    watcher.FileDeleted -= OnChanged;
                    watcher.Dispose();
                }
            }

            m_FileWatchers = new List<FileWatcher>();
            foreach (var directory in GetModelDirectories())
            {
                Directory.CreateDirectory(directory);
                var watcher = new FileWatcher(directory)
                {
                    NotifyFilter = NotifyFilters.LastWrite
                };
                watcher.FileChanged += OnChanged;
                watcher.FileCreated += OnChanged;
                watcher.FileDeleted += OnChanged;
                watcher.EnableRaisingEvents = true;
                m_FileWatchers.Add(watcher);
            }

            LoadModelsForNewDirectory(m_CurrentModelsDirectory);
        }

        public string HomeDirectory => App.ModelLibraryPath();

        public bool IsHomeDirectory()
        {
            return m_CurrentModelsDirectory == HomeDirectory;
        }

        public bool IsSubDirectoryOfHome()
        {
            // Check if current directory is under the main Models directory OR is the Blocks root
            var blocksRoot = App.BlocksModelLibraryPath();
            bool isUnderMainRoot = m_CurrentModelsDirectory.StartsWith(HomeDirectory, StringComparison.OrdinalIgnoreCase);
            bool isBlocksRoot = !string.IsNullOrEmpty(blocksRoot) &&
                               m_CurrentModelsDirectory.Equals(blocksRoot, StringComparison.OrdinalIgnoreCase);

            return isUnderMainRoot || isBlocksRoot;
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
                m_ChangedFile = WidgetManager.GetModelSubpath(e.FullPath);
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
            m_ModelRootsByRelativePath.Clear();

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
            foreach (var directory in GetModelDirectories())
            {
                // Always recurse to scan all subdirectories
                // Blocks uses recursion to flatten its hierarchy
                // Main Models directory uses recursion to populate all subdirectories
                ProcessDirectory(directory, oldModels, recurse: true);
            }

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
                        relativePath, null, null, m_MissingModelsByRelativePath[relativePath], null, null, null, null, null);
                    m_MissingModelsByRelativePath.Remove(relativePath);
                }
                if (m_MissingNormalizedModelsByRelativePath.ContainsKey(relativePath))
                {
                    ModelWidget.CreateModelsFromRelativePath(
                        relativePath, null, m_MissingNormalizedModelsByRelativePath[relativePath], null, null, null, null, null, null);
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
            LoadModels();
            // Get the root directory that 'path' belongs to
            var pathRoot = GetModelRoot(path) ?? HomeDirectory;
            var blocksRoot = App.BlocksModelLibraryPath();
            bool isBlocksRoot = !string.IsNullOrEmpty(blocksRoot) &&
                               path.Equals(blocksRoot, StringComparison.OrdinalIgnoreCase);

            // Convert directory to a path relative to HomeDirectory
            var modelsInDirectory = m_ModelsByRelativePath.Keys.Where(m =>
            {
                if (!m_ModelRootsByRelativePath.TryGetValue(m, out var modelRoot))
                {
                    return false; // Skip models without a known root
                }

                // Only include models from the same root directory as the path we're viewing
                if (modelRoot != pathRoot)
                {
                    return false;
                }

                // For Blocks root directory, show all models from that tree (flat hierarchy)
                if (isBlocksRoot && modelRoot == blocksRoot)
                {
                    return true;
                }

                var dirPath = Path.GetDirectoryName(Path.Join(modelRoot, m));
                return dirPath == path;
            }).ToList();
            modelsInDirectory.Sort();
            m_OrderedModelNames[path] = modelsInDirectory;

            foreach (string relativePath in m_OrderedModelNames[path])
            {
                if (m_MissingModelsByRelativePath.ContainsKey(relativePath))
                {
                    ModelWidget.CreateModelsFromRelativePath(
                        relativePath, null, m_MissingModelsByRelativePath[relativePath], null, null, null, null, null, null);
                    m_MissingModelsByRelativePath.Remove(relativePath);
                }
                if (m_MissingNormalizedModelsByRelativePath.ContainsKey(relativePath))
                {
                    ModelWidget.CreateModelsFromRelativePath(
                        relativePath, null, m_MissingNormalizedModelsByRelativePath[relativePath], null, null, null, null, null, null);
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

        void ProcessDirectory(string sPath, Dictionary<string, Model> oldModels, bool recurse = false)
        {
            if (Directory.Exists(sPath))
            {
                string[] aFiles = Directory.GetFiles(sPath);
                string rootDirectory = GetModelRoot(sPath);
                var blocksRoot = App.BlocksModelLibraryPath();
                bool isBlocksTree = !string.IsNullOrEmpty(blocksRoot) && rootDirectory == blocksRoot;
                bool isBlocksRoot = isBlocksTree && sPath.Equals(blocksRoot, StringComparison.OrdinalIgnoreCase);

                // For Blocks: skip files in the root directory (only process subdirectories)
                if (!isBlocksRoot)
                {
                    // Models we download from Poly are called ".gltf2", but ".gltf" is more standard
                    List<string> extensions = new() { ".gltf2", ".gltf", ".glb", ".ply", ".svg", ".obj", ".vox" };

#if USD_SUPPORTED
                    extensions.AddRange(new [] { ".usda", ".usdc", ".usd" });
#endif
#if FBX_SUPPORTED
                    extensions.Add( ".fbx" );
#endif

                    for (int i = 0; i < aFiles.Length; ++i)
                    {
                        string filename = Path.GetFileName(aFiles[i]);
                        string sExtension = Path.GetExtension(aFiles[i]).ToLower();

                        // For Blocks tree: only process files named "model.obj"
                        if (isBlocksTree && !filename.Equals("model.obj", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (extensions.Contains(sExtension))
                        {
                            Model rNewModel;
                            string path = aFiles[i].Replace("\\", "/");
                            string relativePath = WidgetManager.GetModelSubpath(path);
                            if (relativePath == null || rootDirectory == null)
                            {
                                continue;
                            }
                            if (!oldModels.TryGetValue(relativePath, out rNewModel))
                            {
                                rNewModel = new Model(relativePath);
                            }
                            else
                            {
                                oldModels.Remove(relativePath);
                            }
                            // Should we skip this loop earlier if m_ModelsByRelativePath already contains the key?
                            m_ModelsByRelativePath.TryAdd(rNewModel.RelativePath, rNewModel);
                            m_ModelRootsByRelativePath[rNewModel.RelativePath] = rootDirectory;
                        }
                    }
                }

                // Recurse into subdirectories if requested
                if (recurse || m_RecurseDirectories)
                {
                    string[] aSubdirectories = Directory.GetDirectories(sPath);
                    for (int i = 0; i < aSubdirectories.Length; ++i)
                    {
                        ProcessDirectory(aSubdirectories[i], oldModels, recurse);
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
                string baseDirectory = GetModelRootForRelativePath(relativePath) ?? HomeDirectory;
                LoadModelsForNewDirectory(Path.Combine(baseDirectory, relativeDirPath ?? string.Empty));
                m_ModelsByRelativePath.TryGetValue(relativePath, out m);
            }
            return m;
        }

        private string GetModelRootForRelativePath(string relativePath)
        {
            if (m_ModelRootsByRelativePath.TryGetValue(relativePath, out var root))
            {
                return root;
            }
            return GetModelDirectories().FirstOrDefault();
        }
    }
} // namespace TiltBrush
