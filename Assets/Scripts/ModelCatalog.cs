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
using System.Threading.Tasks;
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
        private readonly Dictionary<string, TiltModels75> m_MissingModelData = new Dictionary<string, TiltModels75>();
        private readonly ModelRestoreGate m_ModelRestoreGate = new ModelRestoreGate();

        private Dictionary<string, List<string>> m_OrderedModelNames;
        private volatile bool m_FolderChanged;
        private List<FileWatcher> m_FileWatchers;
        private string m_CurrentModelsDirectory;
        public string CurrentModelsDirectory => m_CurrentModelsDirectory;
        private readonly CatalogChangeQueue m_ChangedFiles = new CatalogChangeQueue();
        private bool m_RecurseDirectories = false;
        private Dictionary<string, string> m_ModelRootsByRelativePath;
        private bool m_SafScanInProgress;
        private TaskCompletionSource<bool> m_SafScanCompletion;
        private bool m_SafRescanRequested;
        private string m_SafCatalogRootIdentity;
        private IUserStorageBackend m_SafCatalogBackend;
        private bool m_SafSeedAttempted;
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
                return m_MissingModelsByRelativePath.Keys
                    .Union(m_MissingNormalizedModelsByRelativePath.Keys)
                    .Select(GetMissingModelData);
            }
        }

        void Awake()
        {
            m_Instance = this;
            Init();
        }

        private void OnDestroy()
        {
            StopWatchingModelDirectories();
            m_ModelRestoreGate.Invalidate();
            m_SafScanCompletion?.TrySetResult(false);
        }

        public void Init()
        {
            m_ModelRestoreGate.Invalidate();
            m_MissingModelData.Clear();
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

        internal static bool PrepareModelWatchDirectory(string directory, string modelLibraryPath)
        {
            // Only Open Brush's own library is ours to create. Blocks is an optional,
            // externally managed source and must already exist before we watch it.
            if (string.Equals(directory, modelLibraryPath, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(directory);
            }
            return Directory.Exists(directory);
        }

        private string GetModelRoot(string path)
        {
            return GetModelDirectories()
                .FirstOrDefault(directory => path.StartsWith(directory, StringComparison.OrdinalIgnoreCase));
        }

        public void ChangeDirectory(string newPath)
        {
            m_CurrentModelsDirectory = newPath;
            StopWatchingModelDirectories();

            m_FileWatchers = new List<FileWatcher>();
            IEnumerable<string> watchedDirectories =
                UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework
                    ? new[] { App.BlocksModelLibraryPath() }
                    : GetModelDirectories();
            foreach (var directory in watchedDirectories.Where(
                         path => !string.IsNullOrEmpty(path)))
            {
                if (!PrepareModelWatchDirectory(directory, App.ModelLibraryPath())) { continue; }
                var watcher = new FileWatcher(directory)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
                };
                watcher.FileChanged += OnChanged;
                watcher.FileCreated += OnChanged;
                watcher.FileDeleted += OnChanged;
                watcher.EnableRaisingEvents = true;
                m_FileWatchers.Add(watcher);
            }

            LoadModelsForNewDirectory(m_CurrentModelsDirectory);
        }

        private void StopWatchingModelDirectories()
        {
            if (m_FileWatchers == null) { return; }
            foreach (FileWatcher watcher in m_FileWatchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.FileChanged -= OnChanged;
                watcher.FileCreated -= OnChanged;
                watcher.FileDeleted -= OnChanged;
                watcher.Dispose();
            }
            m_FileWatchers.Clear();
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
            if (!(source is FileWatcher watcher) || m_FileWatchers == null || !m_FileWatchers.Contains(watcher)) { return; }
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                m_ChangedFiles.Add(WidgetManager.GetModelSubpath(e.FullPath));
            }
            m_FolderChanged = true;
        }

        public void ClearMissingModels()
        {
            m_ModelRestoreGate.Invalidate();
            m_MissingModelData.Clear();
            m_MissingNormalizedModelsByRelativePath.Clear();
            m_MissingModelsByRelativePath.Clear();
        }

        public void AddMissingModel(
            string relativePath, TrTransform[] xfs, TrTransform[] rawXfs)
        {
            m_MissingModelData.Remove(relativePath);
            if (xfs != null)
            {
                m_MissingNormalizedModelsByRelativePath[relativePath] = xfs;
            }
            if (rawXfs != null)
            {
                m_MissingModelsByRelativePath[relativePath] = rawXfs;
            }
        }

        public void AddMissingModel(TiltModels75 data)
        {
            if (data.FilePath == null) { return; }
            AddMissingModel(data.FilePath, data.Transforms, data.RawTransforms);
            m_MissingModelData[data.FilePath] = data;
        }

        private TiltModels75 GetMissingModelData(string path)
        {
            if (m_MissingModelData.TryGetValue(path, out TiltModels75 data)) { return data; }
            m_MissingNormalizedModelsByRelativePath.TryGetValue(path, out TrTransform[] normalized);
            m_MissingModelsByRelativePath.TryGetValue(path, out TrTransform[] raw);
            return new TiltModels75 { FilePath = path, Transforms = normalized, RawTransforms = raw };
        }

        internal Func<bool> CaptureModelRestoreSceneValidation()
        {
            int generation = m_ModelRestoreGate.Generation;
            return () => generation == m_ModelRestoreGate.Generation;
        }

        internal Func<bool> CaptureModelRestoreValidation()
        {
            int generation = m_ModelRestoreGate.Generation;
            IUserStorageBackend backend = UserStorage.Backend;
            return () => generation == m_ModelRestoreGate.Generation &&
                ReferenceEquals(backend, UserStorage.Backend);
        }

        private void RecoverMissingModels()
        {
            // The full index, not the panel's current folder, determines what can be restored.
            foreach (TiltModels75 data in MissingModels.ToArray())
            {
                if (m_ModelsByRelativePath.ContainsKey(data.FilePath))
                {
                    _ = RecoverMissingModelAsync(data);
                }
            }
        }

        private async Task RecoverMissingModelAsync(TiltModels75 data)
        {
            string path = data.FilePath;
            Func<bool> sceneCurrent = CaptureModelRestoreSceneValidation();
            Func<bool> sourceCurrent = CaptureModelRestoreValidation();
            Func<bool> pendingCurrent = () => sourceCurrent() &&
                ReferenceEquals(GetMissingModelData(path).Transforms, data.Transforms) &&
                ReferenceEquals(GetMissingModelData(path).RawTransforms, data.RawTransforms) &&
                (!m_MissingModelData.TryGetValue(path, out TiltModels75 pending) || ReferenceEquals(pending, data));
            try
            {
                await m_ModelRestoreGate.RunAsync(path,
                    current => ModelWidget.CreateModelsFromRelativePath(
                        path, data.Subtrees, data.Transforms, data.RawTransforms, data.PinStates,
                        data.GroupIds, data.LayerIds, data.SplitMeshPaths, data.NotSplittableMeshPaths,
                        () => current() && pendingCurrent()),
                    () =>
                    {
                        if (!pendingCurrent()) { return; }
                        m_MissingModelsByRelativePath.Remove(path);
                        m_MissingNormalizedModelsByRelativePath.Remove(path);
                        m_MissingModelData.Remove(path);
                    }, pendingCurrent);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"MODEL_RESTORE Could not recover {path}: {e.Message}");
            }
            finally
            {
                // The new source's scan may have attempted recovery while this path was
                // still in flight against the old source. Retry after releasing the gate.
                if (sceneCurrent() && !sourceCurrent()) { ForceCatalogScan(); }
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
            m_FolderChanged = false;
            var oldModels = new Dictionary<string, Model>(m_ModelsByRelativePath);
            m_ModelRootsByRelativePath.Clear();

            // If we changed a file, pretend like we don't have it.
            foreach (string changedPath in m_ChangedFiles.Drain())
            {
                if (oldModels.TryGetValue(changedPath, out Model changedModel)) { changedModel.ReleaseFromCatalog(); }
                oldModels.Remove(changedPath);
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
                    entry.Value.ReleaseFromCatalog();
                }
                Resources.UnloadUnusedAssets();
            }

            // Note: Do not populate m_OrderedModelNames here - it will be populated by LoadModelsForNewDirectory
            // to ensure proper filtering based on the current directory
            // Note: CatalogChanged event is fired by LoadModelsForNewDirectory, not here

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

            RecoverMissingModels();
            if (CatalogChanged != null)
            {
                CatalogChanged();
            }
        }

        public void ForceCatalogScan()
        {
            if (m_SafScanInProgress) { m_SafRescanRequested = true; }
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
                !m_SafSeedAttempted)
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
            m_SafSeedAttempted = true;
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

            string handledKey = $"{kSafSeedPreference}.HandledFilesV1";
            var handled = DefaultMediaSeeder.GetHandledFiles(
                PlayerPrefs.HasKey(handledKey) ? PlayerPrefs.GetString(handledKey) : null,
                App.Instance.HasPlayedBefore ||
                 PlayerPrefs.GetInt(kSafSeedPreference, 0) != 0 ||
                 (listing.Success && listing.Documents.Count > 0),
                new[] { "DefaultModels/Andy.glb", "DefaultModels/Tiltasaurus.glb" });
            // Persist migration before writes, including when all legacy defaults were deleted.
            PlayerPrefs.SetString(handledKey, string.Join("\n", handled.OrderBy(value => value)));
            PlayerPrefs.Save();
            {
                foreach (string resourcePath in m_DefaultModels)
                {
                    if (string.IsNullOrEmpty(resourcePath)) { continue; }
                    string normalizedResource = resourcePath.Replace('\\', '/');
                    if (handled.Contains(normalizedResource)) { continue; }
                    string displayName = Path.GetFileName(normalizedResource);
                    if (listing.Success && listing.Documents.Any(document =>
                        document.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase)))
                    {
                        handled.Add(normalizedResource);
                        PlayerPrefs.SetString(handledKey, string.Join("\n", handled.OrderBy(value => value)));
                        PlayerPrefs.Save();
                        continue;
                    }
                    TextAsset resource = Resources.Load<TextAsset>(resourcePath);
                    if (resource == null)
                    {
                        Debug.LogWarning(
                            $"SAF_STORAGE Missing default model: {resourcePath}");
                        continue;
                    }
                    byte[] bytes = resource.bytes;
                    Resources.UnloadAsset(resource);
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
                    handled.Add(normalizedResource);
                    PlayerPrefs.SetString(handledKey, string.Join("\n", handled.OrderBy(value => value)));
                    PlayerPrefs.Save();
                }
            }

            PlayerPrefs.SetInt(kSafSeedPreference, 1);
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
                    HashSet<string> extensions = GetSupportedExtensions();

                    for (int i = 0; i < aFiles.Length; ++i)
                    {
                        string filename = Path.GetFileName(aFiles[i]);
                        string sExtension = Path.GetExtension(aFiles[i]);

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
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            m_SafScanCompletion = completion;
            try
            {
                using (IEnumerator<object> scan = LoadSafModelsForNewDirectoryImpl(path))
                {
                    while (scan.MoveNext()) { yield return scan.Current; }
                }
            }
            finally
            {
                // A queued rescan may already have installed its own completion source.
                if (ReferenceEquals(m_SafScanCompletion, completion))
                {
                    m_SafScanCompletion = null;
                    m_SafScanInProgress = false;
                }
                completion.TrySetResult(true);
            }
        }

        private IEnumerator<object> LoadSafModelsForNewDirectoryImpl(string path)
        {
            m_SafScanInProgress = true;
            m_FolderChanged = false;
            IUserStorageBackend backend = UserStorage.Backend;
            string scanRootIdentity = backend.RootIdentity;
            if (m_SafCatalogRootIdentity != null &&
                (!ReferenceEquals(m_SafCatalogBackend, backend) || !string.Equals(
                    m_SafCatalogRootIdentity,
                    scanRootIdentity,
                    StringComparison.Ordinal)))
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
                    oldModel.ReleaseFromCatalog();
                }
                m_ModelsByRelativePath = localModels;
                m_ModelRootsByRelativePath = localModels.Keys.ToDictionary(
                    relativePath => relativePath,
                    _ => localBlocksRoot);
                m_OrderedModelNames.Clear();
                CatalogChanged?.Invoke();
            }
            m_SafCatalogRootIdentity = scanRootIdentity;
            m_SafCatalogBackend = backend;
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
            if (!ReferenceEquals(backend, UserStorage.Backend) || !string.Equals(
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
            foreach (string changedPath in m_ChangedFiles.Drain()) { oldBlocks.Remove(changedPath); }
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
                    model = Model.ForLibraryFile(record.RelativePath, identity);
                }
                m_ModelsByRelativePath.TryAdd(model.RelativePath, model);
                m_ModelRootsByRelativePath[model.RelativePath] = HomeDirectory;
            }

            var retained = new HashSet<Model>(m_ModelsByRelativePath.Values);
            foreach (Model oldModel in previous.Values.Distinct())
            {
                if (!retained.Contains(oldModel))
                {
                    oldModel.ReleaseFromCatalog();
                }
            }
            if (previous.Values.Any(model => !retained.Contains(model)))
            {
                Resources.UnloadUnusedAssets();
            }

            PopulateOrderedModels(m_CurrentModelsDirectory);
            m_SafScanInProgress = false;
            RecoverMissingModels();
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

        internal static HashSet<string> GetSupportedExtensions()
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".gltf2", ".gltf", ".glb", ".ply", ".spz", ".sog", ".svg", ".obj", ".vox" };
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
        public async Task<Model> GetModelAsync(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) { return null; }
            int generation = m_ModelRestoreGate.Generation;
            IUserStorageBackend backend = UserStorage.Backend;
            if (backend.Kind != StorageBackendKind.StorageAccessFramework) { return GetModel(relativePath); }

            string root = backend.RootIdentity;
            if (!m_SafScanInProgress && ReferenceEquals(m_SafCatalogBackend, backend) && m_SafCatalogRootIdentity == root &&
                m_ModelsByRelativePath.TryGetValue(relativePath, out Model cached)) { return cached; }
            if (!m_SafScanInProgress) { LoadModelsForNewDirectory(m_CurrentModelsDirectory); }
            Task scan = m_SafScanCompletion?.Task;
            while (scan != null)
            {
                await scan;
                if (generation != m_ModelRestoreGate.Generation ||
                    !ReferenceEquals(backend, UserStorage.Backend) || root != backend.RootIdentity)
                {
                    return null;
                }
                // A queued rescan can replace the scan we were awaiting.
                Task nextScan = m_SafScanCompletion?.Task;
                if (ReferenceEquals(scan, nextScan)) { break; }
                scan = nextScan;
            }
            if (generation != m_ModelRestoreGate.Generation ||
                !ReferenceEquals(backend, UserStorage.Backend) || root != backend.RootIdentity)
            {
                return null;
            }
            m_ModelsByRelativePath.TryGetValue(relativePath, out Model model);
            return model;
        }

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
