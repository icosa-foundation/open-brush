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

using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace TiltBrush
{
    public static class ChoicesHelper
    {
        public static bool IsValidChoice<T>(string choice) where T : class
        {
            var fieldValues = typeof(T)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToArray();
            return fieldValues.Contains(choice);
        }

        public static string[] GetAllChoices<T>() where T : class
        {
            return typeof(T)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .Select(f => (string)f.GetValue(null))
                .ToArray();
        }
    }

    public class CategoryChoices
    {
        public static string
            ANY = "",
            ANIMALS = "ANIMALS",
            ARCHITECTURE = "ARCHITECTURE",
            ART = "ART",
            CULTURE = "CULTURE",
            EVENTS = "EVENTS",
            FOOD = "FOOD",
            HISTORY = "HISTORY",
            HOME = "HOME",
            MISCELLANEOUS = "MISCELLANEOUS",
            NATURE = "NATURE",
            OBJECTS = "OBJECTS",
            PEOPLE = "PEOPLE",
            PLACES = "PLACES",
            SCIENCE = "SCIENCE",
            SPORTS = "SPORTS",
            TECH = "TECH",
            TRANSPORT = "TRANSPORT",
            TRAVEL = "TRAVEL";

        public static string GetFriendlyName(string category)
        {
            return category switch
            {
                "ANY" => "Any",
                "ANIMALS" => "Animals & Pets",
                "ARCHITECTURE" => "Architecture",
                "ART" => "Art",
                "CULTURE" => "Culture & Humanity",
                "EVENTS" => "Current Events",
                "FOOD" => "Food & Drink",
                "HISTORY" => "History",
                "HOME" => "Furniture & Home",
                "MISCELLANEOUS" => "Miscellaneous",
                "NATURE" => "Nature",
                "OBJECTS" => "Objects",
                "PEOPLE" => "People & Characters",
                "PLACES" => "Places & Scenes",
                "SCIENCE" => "Science",
                "SPORTS" => "Sports & Fitness",
                "TECH" => "Tools & Technology",
                "TRANSPORT" => "Transport",
                "TRAVEL" => "Travel & Leisure",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }
    }

    public class LicenseChoices
    {
        public static readonly string
            ANY = "",
            CC0 = "CREATIVE_COMMONS_0",
            REMIXABLE = "REMIXABLE",
            ALL_CC = "ALL_CC",
            CREATIVE_COMMONS_BY = "CREATIVE_COMMONS_BY",
            CREATIVE_COMMONS_BY_NC = "CREATIVE_COMMONS_BY_NC",
            CREATIVE_COMMONS_BY_ND = "CREATIVE_COMMONS_BY_ND",
            ALL_RIGHTS_RESERVED = "ALL_RIGHTS_RESERVED";

        public static string GetFriendlyName(string licence)
        {
            return licence switch
            {
                "ANY" => "Any License",
                "CC0" => "Creative Commons Zero (Public Domain)",
                "CREATIVE_COMMONS_BY" => "Creative Commons Attribution",
                "CREATIVE_COMMONS_BY_NC" => "Creative Commons Attribution, Non-Commercial",
                "CREATIVE_COMMONS_BY_ND" => "Creative Commons Attribution, No Derivatives",
                "REMIXABLE" => "Any Remixable Licence",
                "ALL_CC" => "Any Creative Commons License",
                "ALL_RIGHTS_RESERVED" => "All Rights Reserved",
                _ => throw new ArgumentOutOfRangeException(nameof(licence), licence, null)
            };
        }
    }

    public class OrderByChoices
    {
        public const string
            NEWEST = "NEWEST",  // Same as CREATE_TIME
            OLDEST = "OLDEST",  // Same as -CREATE_TIME
            BEST = "BEST",
            TRIANGLE_COUNT = "TRIANGLE_COUNT",
            LIKED_TIME = "LIKED_TIME",
            CREATE_TIME = "CREATE_TIME",
            UPDATE_TIME = "UPDATE_TIME",
            LIKES = "LIKES",
            DOWNLOADS = "DOWNLOADS",
            DISPLAY_NAME = "DISPLAY_NAME",
            AUTHOR_NAME = "AUTHOR_NAME";

        public static string GetFriendlyName(string orderBy)
        {
            return orderBy switch
            {
                "NEWEST" => "Newest",
                "OLDEST" => "Oldest",
                "BEST" => "Best",
                "TRIANGLE_COUNT" => "Triangle Count",
                "LIKED_TIME" => "Recently Liked",
                "CREATE_TIME" => "Creation Time",
                "UPDATE_TIME" => "Update Time",
                "LIKES" => "Likes",
                "DOWNLOADS" => "Downloads",
                "DISPLAY_NAME" => "Title",
                "AUTHOR_NAME" => "Author",
                _ => throw new ArgumentOutOfRangeException(nameof(orderBy), orderBy, null)
            };
        }
    }

    public class FormatChoices
    {
        public static string
            ANY = "",
            TILT = "TILT",
            BLOCKS = "BLOCKS",
            GLTF = "GLTF",
            GLTF1 = "GLTF1",
            GLTF2 = "GLTF2",
            OBJ = "OBJ",
            FBX = "FBX",
            NOT_TILT = "-TILT",
            NOT_BLOCKS = "-BLOCKS",
            NOT_GLTF = "-GLTF",
            NOT_GLTF1 = "-GLTF1",
            NOT_GLTF2 = "-GLTF2",
            NOT_OBJ = "-OBJ",
            NOT_FBX = "-FBX";
    }

    public class CuratedChoices
    {
        public static string
            ANY = "",
            TRUE = "true",
            FALSE = "false";
    }

    /// Used as an accessor for files downloaded from Poly and cached on local storage.
    public partial class IcosaAssetCatalog : MonoBehaviour
    {
        // TODO limit for non-desktop builds?
        const int kAssetDiskCacheSize = 1000;
        const float kThumbnailFetchRate = 15;
        const int kThumbnailFetchMaxCount = 30;
        const int kThumbnailReadRate = 4;
        private const int DEFAULT_MODEL_TRIANGLE_COUNT_MAX = 80000;

        // This may be a bit broader than an asset id, but it's a safe set of
        // filename characters.
        // Change - added . % ~ to allow urlencoded urls
        static readonly Regex sm_AssetIdPattern = new Regex(@"^[a-zA-Z0-9-_%~\.]+$");

        public enum AssetLoadState
        {
            Unknown,
            NotDownloaded,
            Downloading,
            Downloaded, // On disk but not in memory
            // DownloadFailed,  // We don't keep track of download errors, so this becomes "NotDownloaded"
            Loading,
            LoadFailed, // This shows up as a !Valid model in the model catalog
            Loaded
        }

        public class AssetDetails
        {
            // Disabled only because there isn't a pressing reason to enable it.
            const bool kLazyLoadThumbnail = false;

            private readonly IcosaAssetCatalog m_Owner;
            private readonly Texture2D m_Thumbnail;
            private string m_ThumbnailUrl; // if non-null, have not attempted to fetch it yet

            public string AssetId { get; }
            public string HumanName { get; }
            public string AccountName { get; }
            public Quaternion? ModelRotation { get; }

            public Texture2D Thumbnail
            {
                get
                {
                    if (m_ThumbnailUrl != null)
                    {
                        string url = m_ThumbnailUrl;
                        m_ThumbnailUrl = null;
                        DownloadThumbnailAsync(url);
                    }
                    return m_Thumbnail;
                }
            }

            public Model Model { get { return App.IcosaAssetCatalog.GetModel(AssetId); } }

            public AssetDetails(
                JToken json, string accountName, string thumbnailSuffix)
            {
                m_Owner = App.IcosaAssetCatalog;
                HumanName = json["displayName"].ToString();
                AssetId = json["assetId"].ToString();
                AccountName = accountName;
                var rotation = json["presentationParams"]?["orientingRotation"];
                if (rotation != null)
                {
                    ModelRotation = new Quaternion(
                        rotation["x"]?.Value<float>() ?? 0,
                        rotation["y"]?.Value<float>() ?? 0,
                        rotation["z"]?.Value<float>() ?? 0,
                        rotation["w"]?.Value<float>() ?? 0
                    );
                }
                else
                {
                    ModelRotation = null;
                }

                m_Thumbnail = new Texture2D(4, 4, TextureFormat.ARGB32, false);
                m_ThumbnailUrl = json?["thumbnail"]?["url"]?.ToString();
                if (!string.IsNullOrEmpty(thumbnailSuffix))
                {
                    m_ThumbnailUrl = string.Format("{0}={1}", m_ThumbnailUrl, thumbnailSuffix);
                }
                if (!kLazyLoadThumbnail)
                {
                    // Pre-emptive thumbnail fetch
                    _ = Thumbnail;
                }
            }

            /// Returns the contents of path, or null if the cache doesn't exist / can't be read.
            /// It's ok to pass null.
            /// Does not raise exceptions.
            private static byte[] SafeReadCache(string path)
            {
                if (path != null && File.Exists(path))
                {
                    try
                    {
                        return File.ReadAllBytes(path);
                    }
                    catch (IOException e)
                    {
                        Debug.LogWarning($"Could not read cache {path}: {e}");
                    }
                }
                return null;
            }

            /// Updates the contents of path.
            /// It's ok to pass null path or contents; passing contents=null clears the cache file.
            /// Does not raise exceptions.
            private static void SafeWriteCache(string path, byte[] contents)
            {
                if (path == null) { return; }
                try { File.Delete(path); }
                catch { }
                if (contents != null)
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                    {
                        try { Directory.CreateDirectory(dir); }
                        catch { }
                    }
                    try { File.WriteAllBytes(path, contents); }
                    catch { }
                }
            }

            async void DownloadThumbnailAsync(string thumbnailUrl)
            {
                string cachePath = Path.Combine(m_Owner.m_ThumbnailCacheDir, AssetId);
                byte[] thumbnailBytes = SafeReadCache(cachePath);

                if (thumbnailBytes == null)
                {
                    await m_Owner.m_thumbnailFetchLimiter.WaitAsync();
                    WebRequest www = new WebRequest(thumbnailUrl);
                    await www.SendAsync();

                    while (m_Owner.m_thumbnailReadLimiter.IsBlocked())
                    {
                        await Awaiters.NextFrame;
                    }
                    thumbnailBytes = www.ResultBytes;
                    SafeWriteCache(cachePath, thumbnailBytes);
                }

                if (thumbnailBytes != null)
                {
                    try
                    {
                        // TODO: fix aspect ratio of thumbnail
                        RawImage imageData = await new ThreadedImageReader(thumbnailBytes, thumbnailUrl);

                        UnityEngine.Profiling.Profiler.BeginSample("AssetDetails.DownloadThumbnail:LoadImage");
                        if (imageData != null)
                        {
                            m_Thumbnail.Reinitialize(imageData.ColorWidth, imageData.ColorHeight,
                                TextureFormat.ARGB32, false);
                            m_Thumbnail.SetPixels32(imageData.ColorData);
                            m_Thumbnail.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                        }
                        UnityEngine.Profiling.Profiler.EndSample();

                        // m_Thumbnail still points to the same Texture2D, so we don't need to send CatalogChanged
                    }
                    catch (Exception)
                    {
                        SafeWriteCache(cachePath, null);
                        throw;
                    }
                }
            }
        }

        public struct IcosaQueryParameters
        {
            public string SearchText;
            public int TriangleCountMax;
            public string License;
            public string OrderBy;
            public string[] Formats;
            public string Curated;
            public string Category;
        }

        private static Vector3? GetCameraForward(JToken cameraParams)
        {
            if (cameraParams == null)
            {
                return null;
            }
            JToken cameraMatrix = cameraParams["matrix4x4"];
            if (cameraMatrix == null) { return null; }
            // The third column holds the camera's forward.
            Vector3 cameraForward = new Vector3();
            cameraForward.x = float.Parse(cameraMatrix[2].ToString());
            cameraForward.y = float.Parse(cameraMatrix[6].ToString());
            cameraForward.z = float.Parse(cameraMatrix[10].ToString());
            return cameraForward;
        }

        private class AssetSet
        {
            public List<AssetDetails> m_Models = new List<AssetDetails>();
            public IEnumerator<Null> m_FetchMetadataCoroutine;
            public bool m_RefreshRequested;
            public float m_CooldownTimer;
            public IcosaQueryParameters QueryParams;
        }

        /// A request to pull a Model into memory.
        /// It's assumed that the Model already exists on disk.
        public class ModelLoadRequest
        {
            public readonly Model Model;
            /// The reason for the Model being pulled into memory.
            public readonly string Reason;
            public string AssetId => Model.AssetId;
            public ModelLoadRequest(Model model, string reason)
            {
                Model = model;
                Reason = reason;
            }
        }

        public event Action CatalogChanged;

        [SerializeField] private string m_ThumbnailSuffix = "s128";
        /// Assets being downloaded to disk.
        /// When done, these get moved to m_RequestLoadQueue.
        private List<AssetGetter> m_ActiveRequests;
        /// Assets that someone wants to bring from disk into memory.
        /// Precondition: they are on disk
        /// These get moved onto m_LoadQueue periodically.
        /// May contain duplicates?
        /// TODO: figure out why we have this intermediate stage
        private List<ModelLoadRequest> m_RequestLoadQueue;
        private List<ModelLoadRequest> m_LoadQueue;
        /// Memoization data for IsLoading().
        /// Set this to null to invalidate it; or (if you are very confident) mutate it.
        /// Invariant: either null, or the union of m_ActiveRequests, m_RequestLoadQueue, m_LoadQueue.
        private HashSet<string> m_IsLoadingMemo = null;
        private string m_CacheDir;
        private string m_ThumbnailCacheDir;
        private Dictionary<string, Model> m_ModelsByAssetId;
        private Dictionary<string, JObject> m_AssetJsonByAssetId;
        private Dictionary<IcosaSetType, AssetSet> m_AssetSetByType;
        private bool m_NotifyListeners;

        private AwaitableRateLimiter m_thumbnailFetchLimiter =
            new AwaitableRateLimiter(kThumbnailFetchRate, kThumbnailFetchMaxCount);
        private RateLimiter m_thumbnailReadLimiter =
            new RateLimiter(maxEventsPerFrame: kThumbnailReadRate);

        /// Returns true if the assetId is in any of our download or load queues
        public bool IsLoading(string assetId)
        {
            // This needs caching because it's hammered every frame by the model buttons :-/
            if (m_IsLoadingMemo == null)
            {
                m_IsLoadingMemo = new HashSet<string>();
                m_IsLoadingMemo.UnionWith(m_ActiveRequests.Select(request => request.Asset.Id));
                m_IsLoadingMemo.UnionWith(m_RequestLoadQueue.Select(request => request.AssetId));
                m_IsLoadingMemo.UnionWith(m_LoadQueue.Select(request => request.AssetId));
            }
            return m_IsLoadingMemo.Contains(assetId);
        }

        private void EnsureCatalogsExist()
        {
            Debug.Log($"XXX EnsureCatalogsExist");
            if (m_AssetSetByType == null || m_AssetSetByType.Count == 0)
            {
                InitCatalogQueries();
            }
        }

        public void RequestAutoRefresh(IcosaSetType type)
        {
            EnsureCatalogsExist();
            // We don't update featured except on startup
            if (type != IcosaSetType.Featured && App.IcosaIsLoggedIn)
            {
                m_AssetSetByType[type].m_RefreshRequested = true;
            }
        }

        public void RequestForcedRefresh(IcosaSetType type)
        {
            EnsureCatalogsExist();
            var set = m_AssetSetByType[type];
            if (set.m_FetchMetadataCoroutine != null)
            {
                StopCoroutine(set.m_FetchMetadataCoroutine);
                set.m_FetchMetadataCoroutine = null;
            }
            set.m_Models.Clear();
            set.m_CooldownTimer = -1f;
            set.m_RefreshRequested = true;
        }

        public void Init()
        {
            string cacheDir = Path.Combine(Application.persistentDataPath, "assetCache");
            m_CacheDir = cacheDir.Replace("\\", "/");
            // Use a different directory from m_CacheDir to avoid having to make ValidModelCache()
            // smart enough to allow directories with only a thumbnail and no asset data.
            m_ThumbnailCacheDir = Path.Combine(Application.persistentDataPath, "assetThumbnail")
                .Replace("\\", "/");
            m_ActiveRequests = new List<AssetGetter>();
            m_RequestLoadQueue = new List<ModelLoadRequest>();
            m_LoadQueue = new List<ModelLoadRequest>();

            FileUtils.InitializeDirectoryWithUserError(m_CacheDir, "Failed to create asset cache");

            m_ModelsByAssetId = new Dictionary<string, Model>();
            // InitCatalogQueries();

            try
            {
                foreach (string folderPath in EnumerateCacheDirectories())
                {
                    string assetId = Path.GetFileName(folderPath);
                    string modelFile = ValidModelCache(folderPath);
                    if (modelFile != null)
                    {
                        string path = Path.Combine(folderPath, assetId);
                        path = Path.Combine(path, modelFile);
                        m_ModelsByAssetId[assetId] = new Model(assetId, path);
                    }
                    else
                    {
                        Debug.LogWarningFormat("Deleting invalid cache folder {0}", folderPath);
                        Directory.Delete(folderPath, true);
                    }
                }
            }
            catch (DirectoryNotFoundException e)
            {
                Debug.LogException(e);
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogException(e);
            }

            m_AssetSetByType = new Dictionary<IcosaSetType, AssetSet>();
            // InitCatalogQueries();

            App.Instance.AppExit += () =>
            {
                var models = EnumerateCacheDirectories()
                    .OrderBy(d => Directory.GetLastAccessTimeUtc(d)).ToArray();
                for (int excess = models.Count() - kAssetDiskCacheSize; excess > 0; excess--)
                {
                    Directory.Delete(models[excess - 1], true);
                }
            };
        }

        public void InitCatalogQueries()
        {
            m_AssetSetByType[IcosaSetType.User] = new AssetSet
            {
                QueryParams = new IcosaQueryParameters
                {
                    SearchText = "",
                    TriangleCountMax = DEFAULT_MODEL_TRIANGLE_COUNT_MAX,
                    License = LicenseChoices.ANY,
                    OrderBy = OrderByChoices.NEWEST,
                    Formats = new[] { FormatChoices.GLTF2, FormatChoices.OBJ },
                    Curated = CuratedChoices.ANY,
                    Category = CategoryChoices.ANY
                }
            };

            m_AssetSetByType[IcosaSetType.Liked] = new AssetSet
            {
                QueryParams = new IcosaQueryParameters
                {
                    SearchText = "",
                    TriangleCountMax = DEFAULT_MODEL_TRIANGLE_COUNT_MAX,
                    License = LicenseChoices.REMIXABLE,
                    OrderBy = OrderByChoices.LIKED_TIME,
                    Formats = new[] { FormatChoices.GLTF2, FormatChoices.OBJ },
                    Curated = CuratedChoices.ANY,
                    Category = CategoryChoices.ANY
                }
            };

            // Old way - newest curated
            // "?curated=true&orderBy=NEWEST"
            // For now try just sorting by "best"
            // Something like orderBy=TRENDING would be good - BEST but weighted by recency
            m_AssetSetByType[IcosaSetType.Featured] = new AssetSet
            {
                m_RefreshRequested = true,
                QueryParams = new IcosaQueryParameters
                {
                    SearchText = "",
                    TriangleCountMax = DEFAULT_MODEL_TRIANGLE_COUNT_MAX,
                    License = LicenseChoices.REMIXABLE,
                    OrderBy = OrderByChoices.BEST,
                    Formats = new[] { FormatChoices.GLTF2, FormatChoices.OBJ },
                    Curated = CuratedChoices.TRUE,
                    Category = CategoryChoices.ANY
                }
            };

            if (App.IcosaIsLoggedIn)
            {
                m_AssetSetByType[IcosaSetType.Featured].m_RefreshRequested = true;
            }

            RefreshFetchCoroutines();
        }

        public AssetLoadState GetAssetLoadState(string assetId)
        {
            if (GetModel(assetId) is Model m)
            {
                // A model may be present in memory but also be still loading -- eg if someone
                // requested that the load be retried. In this case it's kind of in two states;
                // I'm somewhat arbitrarily choosing one.
                if (m.m_Valid) { return AssetLoadState.Loaded; }
                else if (m.Error != null) { return AssetLoadState.LoadFailed; }
                else if (IsLoading(assetId))
                {
                    foreach (var elt in m_RequestLoadQueue)
                    {
                        if (elt.AssetId == assetId)
                        {
                            return AssetLoadState.Loading;
                        }
                    }
                    foreach (var elt in m_LoadQueue)
                    {
                        if (elt.AssetId == assetId)
                        {
                            return AssetLoadState.Loading;
                        }
                    }
                    // This should never happen and probably indicates some bug where m_AssetLoading hasn't
                    // been kept in sync with m_[Request]LoadQueue.
                    Debug.LogWarning($"Model for {assetId} is in an indeterminate state!");
                    return AssetLoadState.Unknown;
                }
                else
                {
                    return AssetLoadState.Downloaded;
                }
            }
            else
            {
                foreach (var downloadRequest in m_ActiveRequests)
                {
                    if (downloadRequest.Asset.Id == assetId)
                    {
                        return AssetLoadState.Downloading;
                    }
                }
                return AssetLoadState.NotDownloaded;
            }
        }

        public string GetCacheDirectoryForAsset(string asset)
        {
            if (!sm_AssetIdPattern.IsMatch(asset))
            {
                Debug.LogWarningFormat("Not an asset id: {0}", asset);
                return null;
            }
            return Path.Combine(m_CacheDir, asset);
        }

        /// On any error, returns an empty enumeration
        public IEnumerable<string> EnumerateCacheDirectories()
        {
            try
            {
                return Directory.GetDirectories(m_CacheDir);
            }
            catch (UnauthorizedAccessException e) { Debug.LogException(e); }
            catch (DirectoryNotFoundException e) { Debug.LogException(e); }
            return new string[] { };
        }

        void Start()
        {
            OAuth2Identity.ProfileUpdated += OnProfileUpdated;
        }

        public Model GetModel(string assetId)
        {
            Model model;
            if (!m_ModelsByAssetId.TryGetValue(assetId, out model))
            {
                // null is actually the default for reference types, just being explicit here.
                // ReSharper disable once RedundantAssignment
                model = null;
            }
            return model;
        }

        /// Checks to see if it's time to kick off a new refresh
        /// Polls any refresh coroutines going on.
        void Update()
        {
            m_thumbnailFetchLimiter.Tick(Time.deltaTime);
            if (!VrAssetService.m_Instance.Available)
            {
                return;
            }

            foreach (var entry in m_AssetSetByType)
            {
                var type = entry.Key;
                var set = entry.Value;

                if (set.m_FetchMetadataCoroutine != null)
                {
                    // Pump existing update coroutine
                    try
                    {
                        if (!set.m_FetchMetadataCoroutine.MoveNext())
                        {
                            set.m_FetchMetadataCoroutine = null;
                        }
                    }
                    catch (VrAssetServiceException e)
                    {
                        ControllerConsoleScript.m_Instance.AddNewLine(e.Message);
                        Debug.LogException(e);
                        set.m_FetchMetadataCoroutine = null;
                    }
                }
                else if (set.m_RefreshRequested)
                {
                    // Kick off a new refresh coroutine if it is time.
                    if (set.m_CooldownTimer <= 0)
                    {
                        set.m_FetchMetadataCoroutine = RefreshAssetSet(type);
                        set.m_RefreshRequested = false;
                        set.m_CooldownTimer = VrAssetService.m_Instance.m_SketchbookRefreshInterval;
                    }
                }
                if (set.m_CooldownTimer >= 0)
                {
                    set.m_CooldownTimer -= Time.deltaTime;
                }
            }
        }

        /// Pass the reason the Model is being pulled into memory, for logging purposes.
        public void RequestModelLoad(Model model, string reason)
        {
            // Verify assumption that byAssetId[model.asset] == model; otherwise, caller may wait
            // indefinitely for model's loaded state to change and that bug will be hard to track down.
            string assetId = model.GetLocation().AssetId;
            m_ModelsByAssetId.TryGetValue(assetId, out Model model2);
            if (model2 != model)
            {
                // If we pretend to try to load the model, the caller may wait infinitely for the Model
                // to load.
                throw new InvalidOperationException($"Duplicate {assetId}");
            }
            RequestModelLoad(assetId, reason);
        }

        /// Request loading a model with a given Poly Asset ID.
        /// Pass the reason the Model is being pulled into memory, for logging purposes.
        ///
        /// Upon completion, the asset will:
        /// - be in "failed download" state (don't know how to check for this)
        /// - be in "download succeeded, load failed" state (check Model.Error != null)
        /// - be in "download succeeded, load succeeded" state (check Model.m_Valid)
        ///
        /// The intent is that this method will ignore previous failures and try again.
        /// If you don't want to retry a failed load-into-memory, you should check Model.Error first.
        /// If you aren't trying to do a hot-reload, you should check Model.m_Valid first.
        public void RequestModelLoad(string assetId, string reason)
        {
            Debug.Log($"RequestModelLoad {assetId} for {reason}");
            // Don't attempt to load models which are already loading.
            if (IsLoading(assetId))
            {
                return;
            }

            if (m_ModelsByAssetId.ContainsKey(assetId))
            {
                // Already downloaded.
                // It may be in memory already, but it's safe to ask for it to be brought in again.
                // That way we get the behavior of "ignore a failed load-into-memory"
                m_RequestLoadQueue.Add(new ModelLoadRequest(m_ModelsByAssetId[assetId], reason));
                m_IsLoadingMemo.Add(assetId);
            }
            else
            {
                // Not downloaded yet.
                // Kick off a download; when done the load will  and arrange for the download-complete to kick off the
                // load-into-memory work.
                string assetDir = GetCacheDirectoryForAsset(assetId);
                try
                {
                    // For the case that the folder exists, but the files were removed.
                    if (!Directory.Exists(assetDir))
                    {
                        Directory.CreateDirectory(assetDir);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.LogError("Cannot create directory for online asset download.");
                }

                // In order of preference
                var formats = new[]
                {
                    VrAssetFormat.GLTF2,
                    VrAssetFormat.GLTF,
                    VrAssetFormat.OBJ_NGON,
                    VrAssetFormat.OBJ,
                    VrAssetFormat.PLY
                };

                // Then request the asset from Poly.
                AssetGetter request = VrAssetService.m_Instance.GetAsset(
                    assetId, formats, reason);
                StartCoroutine(request.GetAssetCoroutine());
                m_ActiveRequests.Add(request);
                m_IsLoadingMemo.Add(assetId);
            }
        }

        /// The inverse of RequestModelLoad().
        /// Returns true if the model is no longer in any load queues.
        /// The current implementation is only a half-hearted effort, so it may return false.
        public bool CancelRequestModelLoad(string assetId)
        {
            if (IsLoading(assetId))
            {
                // Might be tricky to safely remove from this queue, but at least mark it so that
                // it doesn't go from "downloading" to "loading into memory"
                bool isInActiveRequests = false;
                foreach (var elt in m_ActiveRequests)
                {
                    if (elt.Asset.Id == assetId)
                    {
                        elt.IsCanceled = true;
                        isInActiveRequests = true;
                    }
                }

                // Removing from RequestLoadQueue is easy; there's no computation associated with it (yet)
                m_RequestLoadQueue.RemoveAll(elt => elt.AssetId == assetId);
                bool isInRequestLoadQueue = false;

                {
                    bool wasInLoadQueue = false;
                    foreach (var elt in m_LoadQueue)
                    {
                        if (elt.AssetId == assetId)
                        {
                            elt.Model.CancelLoadModelAsync();
                            wasInLoadQueue = true;
                        }
                    }
                    if (wasInLoadQueue)
                    {
                        m_LoadQueue = m_LoadQueue.Where(elt => elt.AssetId != assetId).ToList();
                    }
                }
                bool isInLoadQueue = false;

                // Could just invalidate the cache, but we're going to have to rebuild it in just a moment,
                // and we have enough information to mutate it properly.
                if (!isInActiveRequests && !isInRequestLoadQueue && !isInLoadQueue)
                {
                    m_IsLoadingMemo.Remove(assetId);
                }
            }

            return !IsLoading(assetId);
        }

        /// Downloads models referenced by the passed sketch.
        /// Pass the reason this is happening.
        /// TODO: maybe annotate the download request so we can choose whether they turn
        /// into model loads?
        public void PrecacheModels(SceneFileInfo sceneFileInfo, string reason)
        {
            // TODO precaching can end up getting us rate limited on archive.org
            //StartCoroutine(PrecacheModelsCoroutine(sceneFileInfo, reason));
        }

        /// Waits for the json data to be read on a background thread, and then executes a precache
        /// coroutine for each found asset.
        private IEnumerator<Null> PrecacheModelsCoroutine(SceneFileInfo sceneFileInfo, string reason)
        {
            var getIdsFuture = new Future<List<string>>(() => GetModelIds(sceneFileInfo));
            List<string> ids;
            while (true)
            {
                try
                {
                    if (getIdsFuture.TryGetResult(out ids)) { break; }
                }
                catch (FutureFailed e)
                {
                    throw new Exception($"While reading {sceneFileInfo}", e);
                }
                yield return null;
            }

            if (ids == null) { yield break; }
            List<IEnumerator<Null>> precacheCoroutines = new List<IEnumerator<Null>>();
            // Only trigger off one precache routine per frame.
            foreach (string id in ids)
            {
                if (m_ModelsByAssetId.ContainsKey(id))
                {
                    // Already cached
                    continue;
                }
                if (!FileUtils.InitializeDirectory(GetCacheDirectoryForAsset(id)))
                {
                    continue;
                }

                // In order of preference
                var formats = new[]
                {
                    VrAssetFormat.GLTF2,
                    VrAssetFormat.GLTF,
                    VrAssetFormat.OBJ,
                    VrAssetFormat.PLY
                };

                precacheCoroutines.Add(PrecacheCoroutine(
                    VrAssetService.m_Instance.GetAsset(id, formats, reason)));
                yield return null;
            }

            var cr = CoroutineUtil.CompleteAllCoroutines(precacheCoroutines);
            while (cr.MoveNext())
            {
                yield return cr.Current;
            }
        }

        /// Returns all non-null asset ids from the passed sketch's metadata.
        /// null return value means "empty list".
        /// Raises exception on error.
        private static List<string> GetModelIds(SceneFileInfo sceneFileInfo)
        {
            // Json deserializing is in a separate method that doesn't access Unity objects so that it
            // can be called on a thread. The json deserializing can be pretty slow and can cause
            // frame drops if performed on the main thread.
            Stream metadata = SaveLoadScript.GetMetadataReadStream(sceneFileInfo);
            if (metadata == null)
            {
                if (sceneFileInfo.Exists)
                {
                    // ??? Let's try to provoke an exception to propagate to the caller
                    using (var dummy = File.OpenRead(sceneFileInfo.FullPath)) { }
                    throw new Exception($"Unknown error opening metadata {sceneFileInfo.FullPath}");
                }
                else
                {
                    throw new Exception(
                        "Reading metadata from nonexistent " +
                        $"{sceneFileInfo.InfoType} {sceneFileInfo.HumanName}");
                }
            }
            using (var jsonReader = new JsonTextReader(new StreamReader(metadata)))
            {
                var jsonData = SaveLoadScript.m_Instance.DeserializeMetadata(jsonReader);
                if (SaveLoadScript.m_Instance.LastMetadataError != null)
                {
                    throw new Exception($"Deserialize error: {SaveLoadScript.m_Instance.LastMetadataError}");
                }
                if (jsonData.ModelIndex == null) { return null; }
                return jsonData.ModelIndex.Select(m => m.AssetId).Where(a => a != null).ToList();
            }
        }

        // The directory for the asset must have already been created
        IEnumerator<Null> PrecacheCoroutine(AssetGetter request)
        {
            string assetId = request.Asset.Id;
            var cr = request.GetAssetCoroutine();
            while (true)
            {
                try
                {
                    bool result = cr.MoveNext();
                    if (!result)
                    {
                        break;
                    }
                }
                catch (VrAssetServiceException e)
                {
                    ControllerConsoleScript.m_Instance.AddNewLine(e.Message);
                    Debug.LogException(e);
                    yield break;
                }
                yield return cr.Current;
            }
            while (!request.IsReady) { yield return null; }
            request.Asset.WriteToDisk();
            string path = Path.Combine(GetCacheDirectoryForAsset(assetId), request.Asset.RootFilePath);
            m_ModelsByAssetId[assetId] = new Model(assetId, path);
        }

        public void UpdateCatalog()
        {
            // Walk backwards so removal doesn't mess up our indexing.
            for (int i = m_ActiveRequests.Count - 1; i >= 0; --i)
            {
                AssetGetter request = m_ActiveRequests[i];
                if (request.IsReady || request.IsCanceled)
                {
                    if (request.Asset.ValidAsset)
                    {
                        if (request.Asset.WriteToDisk())
                        {
                            string assetId = request.Asset.Id;
                            string path =
                                Path.Combine(GetCacheDirectoryForAsset(assetId), request.Asset.RootFilePath);
                            // TODO: This assumes PolyRawAssets are models.  This may not be true in the
                            // future and should the VrAssetProtos.ElementType request parameter to
                            // VrAssetService.m_Instance.GetAsset to decide how to store and index the asset.

                            // Populate map entry for this new model.
                            m_ModelsByAssetId[assetId] = new Model(assetId, path);

                            // After download the model should be loaded too, unless the request was canceled.
                            // TODO: this seems a littttle suspect. Just because it finished downloading,
                            // does that mean we still want to bring it into memory?
                            if (!request.IsCanceled)
                            {
                                m_RequestLoadQueue.Add(
                                    new ModelLoadRequest(
                                        m_ModelsByAssetId[assetId], $"{request.Reason} fetched"));
                            }
                            else
                            {
                                // Just reset, in case asset is on one of the other queues.
                                m_IsLoadingMemo = null;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Downloaded asset is empty " + request.Asset.RootFilePath);
                    }

                    m_ActiveRequests.RemoveAt(i);
                    m_NotifyListeners = true;
                }
            }

            if (m_RequestLoadQueue.Count > 0 && m_LoadQueue.Count == 0)
            {
                // Move a single item from "request load" to "load". Too many items on the load queue
                // causes bad stuttering (at least for heavy models).
                var toMove = m_RequestLoadQueue[0];
                // TODO: how is it possible for m_RequestLoadQueue to contain duplicates?
                m_RequestLoadQueue = m_RequestLoadQueue
                    .Where(elt => elt.AssetId != toMove.AssetId)
                    .ToList();
                m_LoadQueue.Add(toMove);
            }

            // Always call this to poll the async loader.
            LoadModelsInQueueAsync();

            // Shout from the hills.
            if (m_NotifyListeners)
            {
                if (CatalogChanged != null)
                {
                    CatalogChanged();
                }
                m_NotifyListeners = false;
            }
        }

        void LoadModelsInQueueAsync()
        {
            UnityEngine.Profiling.Profiler.BeginSample("PAC.LoadModelsInQueueAsync");
            for (int i = m_LoadQueue.Count - 1; i >= 0; --i)
            {
                Model model = m_LoadQueue[i].Model;
                model.LoadModel();
                // TODO Back to async loading
                // AsyncHelpers.RunSync(model.LoadModelAsync);
                m_LoadQueue.RemoveAt(i);
                m_IsLoadingMemo = null;
                m_NotifyListeners = true;
                // if (!model.IsLoading())
                // {
                //     // If the overlay is up, hitching is okay; so avoid the slow threaded image load.
                //     bool useThreadedImageLoad =
                //         OverlayManager.m_Instance.CurrentOverlayState == OverlayState.Hidden;
                //     // model.LoadModelAsync(useThreadedImageLoad);
                //     model.LoadModel();
                // }
                // else
                // {
                //     if (model.TryLoadModel(true))
                //     {
                //         m_LoadQueue.RemoveAt(i);
                //         m_IsLoadingMemo = null;
                //         m_NotifyListeners = true;
                //     }
                // }
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }

        private static HashSet<T> SetMinus<T>(HashSet<T> lhs, HashSet<T> rhs)
        {
            var result = new HashSet<T>(lhs);
            result.ExceptWith(rhs);
            return result;
        }

        private IEnumerator<Null> RefreshAssetSet(IcosaSetType type)
        {
            List<AssetDetails> models = new List<AssetDetails>();
            // When the list is empty, make it the actual list acted upon so that results start
            // showing up immediately.
            if (m_AssetSetByType[type].m_Models.Count == 0)
            {
                m_AssetSetByType[type].m_Models = models;
            }
            AssetLister lister = VrAssetService.m_Instance.ListAssets(type, QueryOptionParametersForSet(type));
            bool firstPass = true;
            while (lister.HasMore || firstPass)
            {
                firstPass = false;
                // TODO - it makes sense for a user to be allowed to access their own private assets
                // But it might break assumptions in the rest of the code and in other apps.
                // As well as presenting some challenges in terms of non-surprising behaviour
                // So for now, just don't show them.
                // bool includePrivate = type == IcosaSetType.User;
                bool includePrivate = false;

                using (var cr = lister.NextPage(models, m_ThumbnailSuffix, includePrivate))
                {
                    int prevCount = models.Count;
                    while (true)
                    {
                        try
                        {
                            if (!cr.MoveNext())
                            {
                                break;
                            }
                        }
                        catch (VrAssetServiceException e)
                        {
                            ControllerConsoleScript.m_Instance.AddNewLine(e.Message);
                            Debug.LogException(e);
                            yield break;
                        }
                        if (models.Count - prevCount > 5)  // Avoid updating the catalog too often
                        {
                            addFoundModels();
                            prevCount = models.Count;
                        }
                        yield return cr.Current;
                    }
                }
                if (models.Count == 0)
                {
                    break;
                }
            }
            // Add any remaining models
            addFoundModels();

            void addFoundModels()
            {
                // As the assets may already have models loaded into them, just add any new models and
                // remove old ones.
                var newIds = new HashSet<string>(models.Select(m => m.AssetId));
                var oldIds = new HashSet<string>(m_AssetSetByType[type].m_Models.Select(m => m.AssetId));
                // These must be reified; if they are left as lazy IEnumerables, O(n^2) behavior results
                HashSet<string> toAdd = SetMinus(newIds, oldIds);
                HashSet<string> toRemove = SetMinus(oldIds, newIds);
                m_AssetSetByType[type].m_Models.RemoveAll(m => toRemove.Contains(m.AssetId));
                m_AssetSetByType[type].m_Models.InsertRange(0, models.Where(m => toAdd.Contains(m.AssetId)));
                if (CatalogChanged != null)
                {
                    CatalogChanged();
                }
            }
        }

        void RefreshFetchCoroutines()
        {
            if (App.IcosaIsLoggedIn)
            {
                m_AssetSetByType[IcosaSetType.User].m_RefreshRequested = true;
                m_AssetSetByType[IcosaSetType.Liked].m_RefreshRequested = true;
            }
            else
            {
                AssetSet set = m_AssetSetByType[IcosaSetType.User];
                if (set.m_FetchMetadataCoroutine != null)
                {
                    StopCoroutine(set.m_FetchMetadataCoroutine);
                    set.m_FetchMetadataCoroutine = null;
                }
                set.m_Models.Clear();
                set = m_AssetSetByType[IcosaSetType.Liked];
                if (set.m_FetchMetadataCoroutine != null)
                {
                    StopCoroutine(set.m_FetchMetadataCoroutine);
                    set.m_FetchMetadataCoroutine = null;
                }
                set.m_Models.Clear();
                if (CatalogChanged != null)
                {
                    CatalogChanged();
                }
            }
        }

        void OnProfileUpdated(OAuth2Identity _)
        {
            RefreshFetchCoroutines();
        }

        void OnDestroy()
        {
            OAuth2Identity.ProfileUpdated -= OnProfileUpdated;
        }

        public int NumCloudModels(IcosaSetType type)
        {
            EnsureCatalogsExist();
            return m_AssetSetByType[type].m_Models.Count();
        }

        public AssetDetails GetIcosaAsset(IcosaSetType type, int index)
        {
            return m_AssetSetByType[type].m_Models[index];
        }

        // Ideally we would check against the format info from Poly that we have all the required
        // elements but for now we know that there should be exactly one .gltf/.gltf2 and a .bin
        // Returns the filename of the .gltf/.gltf2 file, or null if not valid.
        private static string ValidModelCache(string dir)
        {
            // We now don't require a .bin file, as some assets are glbs
            // if (Directory.GetFiles(dir, "*.bin").Length == 0)
            // {
            //     return null;
            // }

            var filesGltf1 = Directory.GetFiles(dir, "*.gltf");
            var filesGltf2 = Directory.GetFiles(dir, "*.gltf2");
            var filesObj = Directory.GetFiles(dir, "*.obj");

            if (filesGltf1.Length + filesGltf2.Length + filesObj.Length != 1)
            {
                return null;
            }

            // We used to prefer gltf1 for some reason. Stop doing that.
            if (filesGltf2.Length == 1)
            {
                return filesGltf2[0];
            }
            if (filesGltf1.Length == 1)
            {
                return filesGltf1[0];
            }
            return filesObj[0];
        }

        public void ClearLoadingQueue()
        {
            m_LoadQueue.Clear();
            m_RequestLoadQueue.Clear();
            m_IsLoadingMemo = null;
            foreach (var req in m_ActiveRequests)
            {
                req.IsCanceled = true;
            }
        }

        public void UnloadUnusedModels()
        {
            foreach (var model in m_ModelsByAssetId.Values.Where(x => x != null && x.m_UsageCount == 0))
            {
                model.UnloadModel();
            }
        }

        public IcosaQueryParameters QueryOptionParametersForSet(IcosaSetType set)
        {
            return m_AssetSetByType[set].QueryParams;
        }

        private void RefreshPanel()
        {
            var panel = (IcosaPanel)PanelManager.m_Instance.GetActivePanelByType(BasePanel.PanelType.Icosa);
            if (panel == null) panel = (IcosaPanel)PanelManager.m_Instance.GetActivePanelByType(BasePanel.PanelType.IcosaMobile);
            if (panel != null)
            {
                panel.RefreshCurrentSet(true);
            }
        }

        public void UpdateSearchText(IcosaSetType set, string mLastInput, bool requestRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(set);
            queryParams.SearchText = mLastInput;
            m_AssetSetByType[set].QueryParams = queryParams;
            if (requestRefresh) RefreshPanel();
        }

        public void UpdateTriangleCountMax(IcosaSetType set, int triangleCountMax, bool requestRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(set);
            queryParams.TriangleCountMax = triangleCountMax;
            m_AssetSetByType[set].QueryParams = queryParams;
            if (requestRefresh) RefreshPanel();
        }

        public void UpdateLicense(IcosaSetType set, string license, bool requestRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(set);
            if (ChoicesHelper.IsValidChoice<LicenseChoices>(license))
            {
                queryParams.License = license;
                m_AssetSetByType[set].QueryParams = queryParams;
                if (requestRefresh) RefreshPanel();
            }
        }

        public void UpdateOrderBy(IcosaSetType set, string orderBy, bool requestRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(set);
            if (ChoicesHelper.IsValidChoice<OrderByChoices>(orderBy))
            {
                queryParams.OrderBy = orderBy;
                m_AssetSetByType[set].QueryParams = queryParams;
                if (requestRefresh) RefreshPanel();
            }
        }

        public void UpdateFormat(IcosaSetType set, string format, bool requestRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(set);
            if (ChoicesHelper.IsValidChoice<FormatChoices>(format))
            {
                queryParams.Formats = new[] { format };
                m_AssetSetByType[set].QueryParams = queryParams;
                if (requestRefresh) RefreshPanel();
            }
        }

        public void UpdateCurated(IcosaSetType set, string curated, bool requestRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(set);
            if (ChoicesHelper.IsValidChoice<CuratedChoices>(curated))
            {
                queryParams.Curated = curated;
                m_AssetSetByType[set].QueryParams = queryParams;
                if (requestRefresh) RefreshPanel();
            }
        }

        public void UpdateCategory(IcosaSetType set, string category, bool requestRefresh = false)
        {
            var queryParams = QueryOptionParametersForSet(set);
            if (ChoicesHelper.IsValidChoice<CategoryChoices>(category))
            {
                queryParams.Category = category;
                m_AssetSetByType[set].QueryParams = queryParams;
                if (requestRefresh) RefreshPanel();
            }
        }

        public JObject GetJsonForAsset(string assetId)
        {
            if (m_AssetJsonByAssetId == null)
            {
                m_AssetJsonByAssetId = new Dictionary<string, JObject>();
            }
            m_AssetJsonByAssetId.TryGetValue(assetId, out JObject json);
            return json;
        }

        public void SetJsonForAsset(string toString, JObject asset)
        {
            m_AssetJsonByAssetId ??= new Dictionary<string, JObject>();
            m_AssetJsonByAssetId[toString] = asset;
        }
    }

} // namespace TiltBrush
