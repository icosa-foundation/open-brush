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
using System.Threading;
using UnityEngine;

namespace TiltBrush
{

    public class ModelCatalog : MonoBehaviour, IReferenceItemCatalog
    {
        private const string kSafSeedPreference =
            "GooglePlayStorage.SeededDefaultModelsFdV1";
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
        private bool m_SafScanInProgress;
        private bool m_SafRescanRequested;
        private string m_SafCatalogRootIdentity;
        private string m_SafSeedAttemptedRootIdentity;
        private bool m_SeedingSafDefaults;

        public bool IsScanning
        {
            get { return m_SafScanInProgress; }
        }

        public int ItemCount
        {
            get
            {
                return m_OrderedModelNames.TryGetValue(
                    m_CurrentModelsDirectory, out List<string> models)
                    ? models.Count
                    : 0;
            }
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
            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                App.InitMediaLibraryPath();
                App.InitModelLibraryPath(m_DefaultModels);
            }
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
            IEnumerable<string> watchedDirectories =
                UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework
                    ? new[] { App.BlocksModelLibraryPath() }
                    : GetModelDirectories();
            foreach (var directory in watchedDirectories.Where(
                         path => !string.IsNullOrEmpty(path)))
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
            return m_ModelsByRelativePath[
                m_OrderedModelNames[m_CurrentModelsDirectory][i]];
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

            // Note: Do not populate m_OrderedModelNames here - it will be populated by LoadModelsForNewDirectory
            // to ensure proper filtering based on the current directory
            // Note: CatalogChanged event is fired by LoadModelsForNewDirectory, not here

            m_FolderChanged = false;
        }

        public void LoadModelsForNewDirectory(string path)
        {
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                if (m_SafScanInProgress)
                {
                    m_SafRescanRequested = true;
                }
                else
                {
                    StartCoroutine(LoadSafModelsForNewDirectory(path));
                }
                return;
            }

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

            // Update the entry for the current directory to ensure ItemCount uses the filtered list
            m_OrderedModelNames[m_CurrentModelsDirectory] = modelsInDirectory;

            foreach (string relativePath in modelsInDirectory)
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
            if (!m_SafScanInProgress)
            {
                LoadModelsForNewDirectory(m_CurrentModelsDirectory);
            }
        }

        void Update()
        {
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework &&
                UserStorage.Backend.IsReady &&
                !m_SeedingSafDefaults &&
                m_SafSeedAttemptedRootIdentity !=
                    UserStorage.Backend.RootIdentity &&
                PlayerPrefs.GetInt(
                    OpenBrushStorage.GetSafRootScopedPreferenceKey(
                        kSafSeedPreference),
                    0) == 0)
            {
                StartCoroutine(SeedSafDefaults());
            }
            if (m_FolderChanged)
            {
                ForceCatalogScan();
            }
        }

        private IEnumerator<object> SeedSafDefaults()
        {
            m_SeedingSafDefaults = true;
            IUserStorageBackend backend = UserStorage.Backend;
            string seedRootIdentity = backend.RootIdentity;
            m_SafSeedAttemptedRootIdentity = seedRootIdentity;
            var listingFuture = new Future<StorageDirectoryResult>(
                () => backend.List(
                    StorageArea.MediaLibraryModels, "", CancellationToken.None),
                cleanupFunction: null,
                longRunning: true);
            StorageDirectoryResult listing = null;
            while (true)
            {
                bool finished;
                try
                {
                    finished = listingFuture.TryGetResult(out listing);
                }
                catch (FutureFailed e)
                {
                    Debug.LogWarning(
                        $"SAF_STORAGE Could not inspect default model destination: " +
                        $"{e.InnerException?.Message ?? e.Message}");
                    m_SeedingSafDefaults = false;
                    yield break;
                }
                if (finished)
                {
                    break;
                }
                yield return null;
            }
            if (!listing.Success && listing.Code != StorageResultCode.NotFound)
            {
                m_SeedingSafDefaults = false;
                yield break;
            }

            if (listing.Code == StorageResultCode.NotFound ||
                listing.Documents.Count == 0)
            {
                foreach (string resourcePath in m_DefaultModels)
                {
                    TextAsset resource = Resources.Load<TextAsset>(resourcePath);
                    if (resource == null)
                    {
                        Debug.LogWarning(
                            $"SAF_STORAGE Missing default model: {resourcePath}");
                        continue;
                    }
                    byte[] bytes = resource.bytes;
                    Resources.UnloadAsset(resource);
                    string displayName = Path.GetFileName(resourcePath);
                    var writeFuture = new Future<StorageMutationResult>(
                        () => WriteSafDefaultModel(
                            backend, displayName, bytes),
                        cleanupFunction: null,
                        longRunning: true);
                    StorageMutationResult result;
                    while (true)
                    {
                        bool finished;
                        try
                        {
                            finished = writeFuture.TryGetResult(out result);
                        }
                        catch (FutureFailed e)
                        {
                            Debug.LogWarning(
                                $"SAF_STORAGE Failed to seed {displayName}: " +
                                $"{e.InnerException?.Message ?? e.Message}");
                            m_SeedingSafDefaults = false;
                            yield break;
                        }
                        if (finished)
                        {
                            break;
                        }
                        yield return null;
                    }
                    if (!result.Success)
                    {
                        Debug.LogWarning(
                            $"SAF_STORAGE Failed to seed {displayName}: {result.Error}");
                        m_SeedingSafDefaults = false;
                        yield break;
                    }
                }
            }

            if (!string.Equals(
                    seedRootIdentity,
                    backend.RootIdentity,
                    StringComparison.Ordinal))
            {
                m_SeedingSafDefaults = false;
                yield break;
            }
            PlayerPrefs.SetInt(
                OpenBrushStorage.GetSafRootScopedPreferenceKey(
                    kSafSeedPreference, seedRootIdentity),
                1);
            PlayerPrefs.Save();
            m_SeedingSafDefaults = false;
            ForceCatalogScan();
        }

        private static StorageMutationResult WriteSafDefaultModel(
            IUserStorageBackend backend, string displayName, byte[] bytes)
        {
            using (IStorageWriteTransaction transaction = backend.BeginWrite(
                StorageArea.MediaLibraryModels,
                displayName,
                "application/octet-stream",
                CancellationToken.None))
            {
                using (Stream output = transaction.OpenWrite())
                {
                    output.Write(bytes, 0, bytes.Length);
                }
                return transaction.Commit();
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

        private sealed class SafModelRecord
        {
            public StorageDocument Document;
            public string RelativePath;
        }

        private IEnumerator<object> LoadSafModelsForNewDirectory(string path)
        {
            m_SafScanInProgress = true;
            IUserStorageBackend backend = UserStorage.Backend;
            string scanRootIdentity = backend.RootIdentity;
            if (m_SafCatalogRootIdentity != null &&
                !string.Equals(
                    m_SafCatalogRootIdentity,
                    scanRootIdentity,
                    StringComparison.Ordinal))
            {
                string localBlocksRoot = App.BlocksModelLibraryPath();
                var localModels = m_ModelsByRelativePath.Where(pair =>
                    m_ModelRootsByRelativePath.TryGetValue(
                        pair.Key, out string modelRoot) &&
                    string.Equals(
                        modelRoot, localBlocksRoot, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                var removedModels = m_ModelsByRelativePath
                    .Where(pair => !localModels.ContainsKey(pair.Key))
                    .Select(pair => pair.Value)
                    .Distinct();
                foreach (Model oldModel in removedModels)
                {
                    if (oldModel.m_ModelParent != null)
                    {
                        Destroy(oldModel.m_ModelParent.gameObject);
                    }
                }
                m_ModelsByRelativePath = localModels;
                m_ModelRootsByRelativePath = localModels.Keys.ToDictionary(
                    relativePath => relativePath,
                    _ => localBlocksRoot);
                m_OrderedModelNames.Clear();
                CatalogChanged?.Invoke();
            }
            m_SafCatalogRootIdentity = scanRootIdentity;
            var scan = new Future<List<SafModelRecord>>(
                () => ListSafModelsRecursively(backend, ""),
                cleanupFunction: null,
                longRunning: true);
            List<SafModelRecord> records = null;
            while (true)
            {
                bool finished;
                try
                {
                    finished = scan.TryGetResult(out records);
                }
                catch (FutureFailed e)
                {
                    Debug.LogWarning(
                        $"SAF_CATALOG Model query failed; retaining the previous catalog: " +
                        $"{e.InnerException?.Message ?? e.Message}");
                    m_SafScanInProgress = false;
                    if (m_SafRescanRequested)
                    {
                        m_SafRescanRequested = false;
                        LoadModelsForNewDirectory(m_CurrentModelsDirectory);
                    }
                    yield break;
                }
                if (finished)
                {
                    break;
                }
                yield return null;
            }
            if (!string.Equals(
                    scanRootIdentity,
                    backend.RootIdentity,
                    StringComparison.Ordinal))
            {
                m_SafScanInProgress = false;
                m_SafRescanRequested = false;
                LoadModelsForNewDirectory(m_CurrentModelsDirectory);
                yield break;
            }

            Dictionary<string, Model> previous = m_ModelsByRelativePath;
            var previousByIdentity = previous.Values
                .GroupBy(model => model.CatalogIdentity)
                .ToDictionary(group => group.Key, group => group.First());
            m_ModelsByRelativePath = new Dictionary<string, Model>();
            m_ModelRootsByRelativePath.Clear();

            string blocksRoot = App.BlocksModelLibraryPath();
            var oldBlocks = new Dictionary<string, Model>(previous);
            ProcessDirectory(blocksRoot, oldBlocks, recurse: true);

            HashSet<string> supportedExtensions = GetSupportedExtensions();
            foreach (SafModelRecord record in records)
            {
                string extension = Path.GetExtension(record.RelativePath).ToLowerInvariant();
                if (!supportedExtensions.Contains(extension))
                {
                    continue;
                }

                string identity =
                    $"{record.Document.DocumentId.Value}|" +
                    $"{record.Document.LastModified:o}|{record.Document.Size}";
                if (!previousByIdentity.TryGetValue(identity, out Model model))
                {
                    StorageDocumentId documentId = record.Document.DocumentId;
                    model = new Model(
                        record.RelativePath,
                        identity,
                        () => backend.Materialize(
                            documentId,
                            MaterializationScope.DependencyTree,
                            CancellationToken.None));
                }
                m_ModelsByRelativePath.TryAdd(model.RelativePath, model);
                m_ModelRootsByRelativePath[model.RelativePath] = HomeDirectory;
            }

            var retained = new HashSet<Model>(m_ModelsByRelativePath.Values);
            foreach (Model oldModel in previous.Values.Distinct())
            {
                if (!retained.Contains(oldModel) && oldModel.m_ModelParent != null)
                {
                    Destroy(oldModel.m_ModelParent.gameObject);
                }
            }
            if (previous.Values.Any(model => !retained.Contains(model)))
            {
                Resources.UnloadUnusedAssets();
            }

            PopulateOrderedModels(m_CurrentModelsDirectory);
            m_FolderChanged = false;
            m_SafScanInProgress = false;
            CatalogChanged?.Invoke();
            if (m_SafRescanRequested)
            {
                m_SafRescanRequested = false;
                LoadModelsForNewDirectory(m_CurrentModelsDirectory);
            }
        }

        private static List<SafModelRecord> ListSafModelsRecursively(
            IUserStorageBackend backend, string relativeDirectory)
        {
            StorageDirectoryResult listing = backend.List(
                StorageArea.MediaLibraryModels,
                relativeDirectory,
                CancellationToken.None);
            if (!listing.Success)
            {
                throw new IOException($"{listing.Code}: {listing.Error}");
            }

            var records = new List<SafModelRecord>();
            foreach (StorageDocument document in listing.Documents)
            {
                string relativePath = string.IsNullOrEmpty(relativeDirectory)
                    ? document.DisplayName
                    : Path.Combine(relativeDirectory, document.DisplayName);
                if (document.IsDirectory)
                {
                    records.AddRange(ListSafModelsRecursively(
                        backend, relativePath.Replace('\\', '/')));
                }
                else
                {
                    records.Add(new SafModelRecord
                    {
                        Document = document,
                        RelativePath = relativePath,
                    });
                }
            }
            return records;
        }

        private void PopulateOrderedModels(string path)
        {
            string pathRoot = GetModelRoot(path) ?? HomeDirectory;
            string blocksRoot = App.BlocksModelLibraryPath();
            bool isBlocksRoot = !string.IsNullOrEmpty(blocksRoot) &&
                path.Equals(blocksRoot, StringComparison.OrdinalIgnoreCase);
            List<string> modelsInDirectory = m_ModelsByRelativePath.Keys.Where(relativePath =>
            {
                if (!m_ModelRootsByRelativePath.TryGetValue(
                        relativePath, out string modelRoot) ||
                    modelRoot != pathRoot)
                {
                    return false;
                }
                if (isBlocksRoot && modelRoot == blocksRoot)
                {
                    return true;
                }
                return Path.GetDirectoryName(Path.Join(modelRoot, relativePath)) == path;
            }).ToList();
            modelsInDirectory.Sort();
            m_OrderedModelNames[m_CurrentModelsDirectory] = modelsInDirectory;
        }

        private static HashSet<string> GetSupportedExtensions()
        {
            var extensions = new HashSet<string>
                { ".gltf2", ".gltf", ".glb", ".ply", ".svg", ".obj", ".vox" };
#if USD_SUPPORTED
            extensions.UnionWith(new[] { ".usda", ".usdc", ".usd" });
#endif
#if FBX_SUPPORTED
            extensions.Add(".fbx");
#endif
            return extensions;
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
