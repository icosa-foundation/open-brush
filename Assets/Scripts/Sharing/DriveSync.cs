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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using DriveData = Google.Apis.Drive.v3.Data;

namespace TiltBrush
{
    public partial class DriveSync
    {
        public const long kMinimumFreeSpace = 512 * 1024 * 1024; // Half a Gig

        public class DataTransferError : Exception
        {
            public DataTransferError(string message, Exception inner) : base(message, inner) { }
        }

        [Flags]
        private enum SyncType : short
        {
            Download = 0x1,
            Upload = 0x2,
            UploadAndDownload = Download | Upload,
        }

        private enum SyncDecision
        {
            None,
            Upload,
            Download,
            Conflict,
        }

        [Serializable]
        public enum SyncedFolderType
        {
            Sketches = 0,
            Snapshots = 1,
            Videos = 2,
            MediaLibrary = 3,
            Exports = 4,
            Scripts = 5,
            Num = 6
        }

        private class SyncedFolder
        {
            public string Name;
            public StorageArea Area;
            public string RelativeDirectory;
            public string StorageRootIdentity;
            public string ParentDriveId;
            public DriveData.File Drive;
            public SyncType SyncType;
            public bool Recursive;
            public string[] IncludeExtensions;
            public string[] ExcludeExtensions;
            public SyncedFolderType FolderType;

            public bool Upload => (SyncType & SyncType.Upload) > 0;
            public bool Download => (SyncType & SyncType.Download) > 0;
        }

        public class SyncItem
        {
            public string Name;
            public StorageArea Area;
            public string RelativeDirectory;
            public StorageDocumentId DocumentId;
            public string StorageRootIdentity;
            public string ParentId;
            public string FileId;
            public DriveData.File DriveFile;
            public string LedgerRelativePath;
            public StorageDocumentId LedgerDocumentId;
            public bool ConflictCopy;
            public bool Overwrite;
            public DateTime LastModified;
            public bool Upload;
            public long Size;
            public SyncedFolderType FolderType;
        }

        /// This is a sorted queue for storing SyncItems. It has O(logN) access times, and the items are
        /// sorted by their modification date. You 'Insert' rather than 'Enqueue' as the item will be
        /// inserted into the queue by modified time rather than put on the end. When you Dequeue it will
        /// remove and return the item with the most recent modified time.
        private class SyncItemQueue
        {
            private SortedDictionary<DateTime, Queue<SyncItem>> m_Queue;
            private int m_Count = 0;

            public int Count => m_Count;

            public SyncItemQueue()
            {
                m_Queue = new SortedDictionary<DateTime, Queue<SyncItem>>();
            }

            public void Insert(SyncItem item)
            {
                Queue<SyncItem> itemQueue;
                lock (m_Queue)
                {
                    if (!m_Queue.TryGetValue(item.LastModified, out itemQueue))
                    {
                        itemQueue = new Queue<SyncItem>();
                        m_Queue[item.LastModified] = itemQueue;
                    }
                    itemQueue.Enqueue(item);
                    m_Count++;
                }
            }

            public SyncItem Dequeue()
            {
                SyncItem item;
                lock (m_Queue)
                {
                    if (m_Count == 0)
                    {
                        return null;
                    }
                    m_Count--;
                    var itemQueue = m_Queue.Last().Value;
                    item = itemQueue.Dequeue();
                    if (itemQueue.Count == 0)
                    {
                        m_Queue.Remove(item.LastModified);
                    }
                }
                return item;
            }

            public void Clear()
            {
                lock (m_Queue)
                {
                    m_Queue.Clear();
                    m_Count = 0;
                }
            }
        }

        /// Class for storing a user preference, along with storing it in PlayerPrefs.
        private class UserSyncFlag
        {
            private string m_PreferenceName;
            private bool m_Value;
            public UserSyncFlag(string user, string preference)
            {
                m_PreferenceName = $"{user}_{preference}";
                m_Value = PlayerPrefs.GetInt(m_PreferenceName, 0) == 1;
            }
            public bool Value
            {
                get => m_Value;
                set
                {
                    m_Value = value;
                    PlayerPrefs.SetInt(m_PreferenceName, m_Value ? 1 : 0);
                }
            }
        }

        private class Transfer : IProgress<long>
        {
            public SyncItem Item { get; private set; }
            public TaskAndCts TaskAndCts { get; private set; }
            public long BytesTransferred { get; private set; }
            public Task Task => TaskAndCts.Task;
            public Transfer(DriveSync ds, SyncItem item)
            {
                Item = item;
                TaskAndCts = new TaskAndCts();
                TaskAndCts.Task = item.Upload
                    ? ds.UploadItemAsync(this, TaskAndCts.Token)
                    : ds.DownloadItemAsync(this, TaskAndCts.Token);
            }
            public void Report(long value)
            {
                BytesTransferred = value;
            }
            public void Cancel()
            {
                TaskAndCts.Cancel();
            }
        }

        private sealed class BackendReadStreamSource : IReopenableReadStream
        {
            private readonly IUserStorageBackend m_Backend;
            private readonly StorageDocumentId m_DocumentId;

            public BackendReadStreamSource(
                IUserStorageBackend backend, StorageDocumentId documentId)
            {
                m_Backend = backend;
                m_DocumentId = documentId;
            }

            public Stream Open()
            {
                return m_Backend.OpenRead(
                    m_DocumentId, requireSeekable: true, CancellationToken.None);
            }
        }

        private List<SyncedFolder> m_Folders = new List<SyncedFolder>();
        private SyncItemQueue m_ToTransfer = new SyncItemQueue();
        private ConcurrentDictionary<Transfer, object> m_Transfers =
            new ConcurrentDictionary<Transfer, object>();
        private TaskAndCts m_InitTask;
        private TaskAndCts m_SyncTask;
        private TaskAndCts m_UpdateTask;
        private bool m_Uninitializing = false;
        private bool m_Initialized = false;
        private UserSyncFlag m_SyncEnabled;
        private UserSyncFlag[] m_SyncedFolderFlags;
        private DriveAccess m_DriveAccess;
        private OAuth2Identity m_GoogleIdentity;
        private long m_TotalBytesToTransfer;
        private long m_PreviousTotalBytesToTransfer;
        private long m_BytesTransferred;
        private bool m_IsCancelling;
        private DriveSyncLedger m_Ledger;
        private string m_LedgerIdentity;
        private readonly object m_LedgerGate = new object();

        public event Action SyncEnabledChanged;

        // True if initialization is in progress.
        public bool Initializing => m_GoogleIdentity.LoggedIn && m_InitTask != null;

        public bool Initialized => m_Initialized;

        public float Progress
        {
            get
            {
                // We use the larger of the byte totals from the current and interrupted sync, so that the
                // progress does not jump around while the scan is performed.
                long totalBytes = Math.Max(m_TotalBytesToTransfer, m_PreviousTotalBytesToTransfer);
                if (totalBytes == 0)
                {
                    return 0;
                }
                long bytesTransferred = m_BytesTransferred;
                bytesTransferred += m_Transfers.Keys.Sum(x => x.BytesTransferred);
                float progress = m_TotalBytesToTransfer == 0
                    ? 0
                    : Mathf.Clamp01((float)bytesTransferred / totalBytes);
                return progress;
            }
        }

        /// This is whether the drive is set to sync. It does not imply that syncing has been successfully
        /// initialized. Its value cannot be changed while the service is being cancelled / shut down.
        public bool SyncEnabled
        {
            get => m_SyncEnabled?.Value ?? false;
            set
            {
                if (m_Uninitializing)
                {
                    return;
                }
                if (m_SyncEnabled == null)
                {
                    if (value && !m_DriveAccess.Ready && !m_DriveAccess.Initializing
                        && m_GoogleIdentity.LoggedIn)
                    {
                        // It seems like Drive Access initialization failed. Let's try again.
                        m_DriveAccess.InitializeDriveLinkAsync();
                    }
                    return;
                }
                bool changed = value != m_SyncEnabled.Value;
                if (changed)
                {
                    m_SyncEnabled.Value = value;
                    SyncEnabledChanged?.Invoke();
                }
                if (value)
                {
                    if (!Initializing && !m_Initialized)
                    {
                        InitializeDriveSyncAsync();
                    }
                }
                else
                {
                    if (Initializing || m_Initialized)
                    {
                        UninitializeAsync().AsAsyncVoid();
                    }
                }
            }
        }

        public bool Syncing
        {
            get
            {
                if (!m_Initialized) { return false; }
                if (m_SyncTask != null || m_ToTransfer.Count != 0)
                {
                    return true;
                }
                return m_Transfers.Any();
            }
        }

        public bool DriveIsLowOnSpace => m_DriveAccess.HasSpaceQuota &&
            (m_DriveAccess.DriveFreeSpace < kMinimumFreeSpace);

        public void InitUserSyncOptions()
        {
            string userId = m_GoogleIdentity.Profile.id.Substring(7);
            m_SyncEnabled = new UserSyncFlag(userId, "GoogleDriveSyncEnabled");
            m_SyncedFolderFlags = new UserSyncFlag[(int)SyncedFolderType.Num];
            for (int i = 0; i < (int)SyncedFolderType.Num; ++i)
            {
                SyncedFolderType folderType = (SyncedFolderType)i;
                m_SyncedFolderFlags[i] = new UserSyncFlag(userId, $"GoogleDriveSyncFlag_{folderType}");
            }
        }

        public void UninitUserSyncOptions()
        {
            m_SyncEnabled = null;
            m_SyncedFolderFlags = null;
        }

        public void ToggleSyncOnFolderOfType(SyncedFolderType type)
        {
            if (m_SyncedFolderFlags == null)
            {
                return;
            }
            int flagIndex = (int)type;
            Debug.Assert(flagIndex >= 0 && flagIndex < m_SyncedFolderFlags.Length);
            if (flagIndex >= 0 && flagIndex < m_SyncedFolderFlags.Length)
            {
                m_SyncedFolderFlags[flagIndex].Value ^= true;
            }
        }

        public bool IsFolderOfTypeSynced(SyncedFolderType type)
        {
            if (m_SyncedFolderFlags == null)
            {
                return false;
            }
            int flagIndex = (int)type;
            Debug.Assert(flagIndex >= 0 && flagIndex < m_SyncedFolderFlags.Length);
            if (flagIndex >= 0 && flagIndex < m_SyncedFolderFlags.Length)
            {
                return m_SyncedFolderFlags[flagIndex].Value;
            }
            return false;
        }

        public DriveSync(DriveAccess driveAccess, OAuth2Identity googleIdentity)
        {
            m_DriveAccess = driveAccess;
            m_GoogleIdentity = googleIdentity;
            m_DriveAccess.OnReadyChanged += OnDriveAccessReady;
        }

        /// This reacts to drive access being turned on or off.
        private void OnDriveAccessReady()
        {
            if (m_DriveAccess.Ready)
            {
                InitUserSyncOptions();
                if (SyncEnabled)
                {
                    InitializeDriveSyncAsync();
                }
            }
            else
            {
                UninitializeAsync().AsAsyncVoid();
                UninitUserSyncOptions();
            }
        }

        /// Initializes the Google Drive sync. Will create the required folders on Drive, and afterwards
        /// kick off a process to sync them.
        public async void InitializeDriveSyncAsync()
        {
            async Task InitializeAsync(CancellationToken token)
            {
                // Make sure we have a root folder
                await m_DriveAccess.CreateRootFolderAsync(token);
                // Make sure we have a folder for the device
                await m_DriveAccess.CreateDeviceFolderAsync(token);

                if (m_DriveAccess.HasSpaceQuota)
                {
                    Debug.Log($"User has {m_DriveAccess.DriveFreeSpace} free space on Drive.\n" +
                        $"That's {m_DriveAccess.DriveFreeSpace - kMinimumFreeSpace} " +
                        "before hitting low free space.");
                }
                else
                {
                    Debug.Log("User has no quota on their Drive.");
                }

                m_Initialized = true;

                // TODO: Do an upload-only sync for Export, Snapshots and Videos.
            }

            if (!m_GoogleIdentity.LoggedIn)
            {
                return;
            }

            if (!m_DriveAccess.Ready)
            {
                return;
            }

            while (m_Uninitializing)
            {
                await new WaitForUpdate();
            }

            if (m_InitTask != null || Initialized)
            {
                await UninitializeAsync();
            }

            m_InitTask = new TaskAndCts();
            m_InitTask.Task = InitializeAsync(m_InitTask.Token);

            try
            {
                await m_InitTask.Task;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                m_InitTask = null;
            }

            // Don't wait for the sync to finish so that we can start transfers immediately.
            SyncLocalFilesAsync().AsAsyncVoid();

            // A background task handles the transfers.
            m_UpdateTask = new TaskAndCts();
            m_UpdateTask.Task = ManageTransfersAsync(m_UpdateTask.Token);
            m_UpdateTask.Task.AsAsyncVoid();
        }

        private async Task SetupSyncFoldersAsync(CancellationToken token)
        {
            var deviceRootId = m_DriveAccess.DeviceFolder;
            m_Folders.Clear();
            var folderSyncs = new List<Task>();
            if (IsFolderOfTypeSynced(SyncedFolderType.Sketches))
            {
                folderSyncs.Add(AddSyncedFolderAsync(
                    "Sketches",
                    StorageArea.Sketches,
                    deviceRootId,
                    SyncType.Upload,
                    SyncedFolderType.Sketches,
                    token));
            }
            if (IsFolderOfTypeSynced(SyncedFolderType.MediaLibrary))
            {
                var mediaLibrary =
                    await m_DriveAccess.CreateFolderAsync("Media Library", deviceRootId, token);
                folderSyncs.Add(AddSyncedFolderAsync(
                    "Images",
                    StorageArea.MediaLibraryImages,
                    mediaLibrary.Id,
                    SyncType.Upload,
                    SyncedFolderType.MediaLibrary,
                    token));
                if (!App.Config.IsMobileHardware)
                {
                    folderSyncs.Add(AddSyncedFolderAsync(
                        "Models",
                        StorageArea.MediaLibraryModels,
                        mediaLibrary.Id,
                        SyncType.Upload,
                        SyncedFolderType.MediaLibrary,
                        token,
                        recursive: true));
                }
                folderSyncs.Add(AddSyncedFolderAsync(
                    "BackgroundImages",
                    StorageArea.MediaLibraryBackgroundImages,
                    mediaLibrary.Id,
                    SyncType.Upload,
                    SyncedFolderType.MediaLibrary,
                    token));
                folderSyncs.Add(AddSyncedFolderAsync(
                    "Videos",
                    StorageArea.MediaLibraryVideos,
                    mediaLibrary.Id,
                    SyncType.Upload,
                    SyncedFolderType.MediaLibrary,
                    token));
            }
            if (IsFolderOfTypeSynced(SyncedFolderType.Snapshots))
            {
                folderSyncs.Add(AddSyncedFolderAsync(
                    "Snapshots",
                    StorageArea.Snapshots,
                    deviceRootId,
                    SyncType.Upload,
                    SyncedFolderType.Snapshots,
                    token));
            }
            if (IsFolderOfTypeSynced(SyncedFolderType.Scripts))
            {
                folderSyncs.Add(AddSyncedFolderAsync(
                    "Scripts",
                    StorageArea.Scripts,
                    deviceRootId,
                    SyncType.UploadAndDownload,
                    SyncedFolderType.Scripts,
                    token,
                    recursive: true,
                    includeExtensions: new[] { ".html" }));

                folderSyncs.Add(AddSyncedFolderAsync(
                    "Plugins",
                    StorageArea.Plugins,
                    deviceRootId,
                    SyncType.UploadAndDownload,
                    SyncedFolderType.Scripts,
                    token,
                    recursive: true,
                    includeExtensions: new[] { ".lua" }));
            }

            if (!App.Config.IsMobileHardware)
            {
                if (IsFolderOfTypeSynced(SyncedFolderType.Videos))
                {
                    folderSyncs.Add(AddSyncedFolderAsync(
                        "Videos",
                        StorageArea.Videos,
                        deviceRootId,
                        SyncType.Upload,
                        SyncedFolderType.Videos,
                        token,
                        excludeExtensions: new[] { ".bat", ".usda" }));
                    folderSyncs.Add(AddSyncedFolderAsync(
                        "VrVideos",
                        StorageArea.VrVideos,
                        deviceRootId,
                        SyncType.Upload,
                        SyncedFolderType.Videos,
                        token));
                }
            }

            if (IsFolderOfTypeSynced(SyncedFolderType.Exports))
            {
                folderSyncs.Add(AddSyncedFolderAsync(
                    "Exports",
                    StorageArea.Exports,
                    deviceRootId,
                    SyncType.Upload,
                    SyncedFolderType.Exports,
                    token,
                    recursive: true));
            }
            await m_DriveAccess.RefreshFreeSpaceAsync(token);
            await Task.WhenAll(folderSyncs);
        }

        /// Uninitializes the drive sync, cancels any in-flight transfers, and cancels any in-flight
        /// initialization.
        public async Task UninitializeAsync()
        {
            try
            {
                m_Uninitializing = true;
                m_ToTransfer.Clear();

                async Task CancelTaskCts(TaskAndCts taskAndCts)
                {
                    try
                    {
                        if (taskAndCts != null && !taskAndCts.Task.IsCompleted)
                        {
                            taskAndCts.Cancel();
                            await taskAndCts.Task;
                        }
                    }
                    catch (OperationCanceledException) { }
                }

                // Wait for five seconds for cancellation to happen - if it still hasn't, well - continue
                // anyway and hope everything is fine.
                var maxWait = Task.Delay(TimeSpan.FromSeconds(5));
                var allTasks = Task.WhenAll(m_Transfers.Keys.Select(x => x.TaskAndCts)
                    .Concat(new[] { m_InitTask, m_SyncTask, m_UpdateTask })
                    .Select(CancelTaskCts));
                await Task.WhenAny(allTasks, maxWait);
                m_Initialized = false;
                m_Transfers.Clear();
                m_InitTask = null;
                m_SyncTask = null;
                m_UpdateTask = null;
                m_Folders.Clear();
                m_Ledger = null;
                m_LedgerIdentity = null;
                m_TotalBytesToTransfer = 0;
                m_BytesTransferred = 0;
            }
            finally
            {
                m_Uninitializing = false;
            }
        }

        private async Task ClearAllTransfers()
        {
            m_ToTransfer.Clear();
            foreach (var transfer in m_Transfers.Keys)
            {
                transfer.Cancel();
            }
            await Task.WhenAll(m_Transfers.Keys.Select(x => x.Task));
            m_Transfers.Clear();
        }

        public bool SyncPossible() =>
            UserStorage.Backend.IsReady &&
            m_GoogleIdentity.LoggedIn &&
            SyncEnabled &&
            !DriveIsLowOnSpace &&
            !m_IsCancelling;

        /// Syncs the local files with the device's Google Drive folder. If a sync is already in progress
        /// it will be cancelled before a new sync is performed. The sync prepares the transfers required
        /// to sync and then the actual transfers happen in the Update function.
        public async Task SyncLocalFilesAsync()
        {
            if (!SyncPossible()) return;

            if (m_SyncTask != null)
            {
                m_IsCancelling = true;
                try
                {
                    await m_SyncTask.Task;
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    m_IsCancelling = false;
                }
            }

            try
            {
                m_SyncTask = new TaskAndCts();
                m_SyncTask.Task = PrepareAllFolderTransfersAsync(m_SyncTask.Token);
                await m_SyncTask.Task;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                m_SyncTask = null;
            }
        }

        /// Adds a folder to the list of synced folders.
        private async Task AddSyncedFolderAsync(
            string name,
            StorageArea area,
            string parentId,
            SyncType syncType,
            SyncedFolderType folderType,
            CancellationToken token,
            bool recursive = false,
            string[] includeExtensions = null,
            string[] excludeExtensions = null
            )
        {
            var folder = new SyncedFolder()
            {
                Name = name,
                Area = area,
                RelativeDirectory = "",
                StorageRootIdentity = UserStorage.Backend.RootIdentity,
                Drive = await m_DriveAccess.GetFolderAsync(name, parentId, token),
                SyncType = syncType,
                Recursive = recursive,
                ParentDriveId = parentId,
                ExcludeExtensions = excludeExtensions,
                IncludeExtensions = includeExtensions,
                FolderType = folderType,
            };
            m_Folders.Add(folder);
        }


        /// Enumerates the transfers required to sync all folders, sorts those transfers in descending
        /// order of modification date, and stores the transfer information in the transfer queue.
        private async Task PrepareAllFolderTransfersAsync(CancellationToken token)
        {
            if (!App.GoogleUserSettings.Initialized || !m_Initialized || m_Uninitializing)
            {
                return;
            }
            // If this is called while there are still things being transferred, we store off the total
            // number of bytes that was being transferred by the old sync.
            m_PreviousTotalBytesToTransfer = m_TotalBytesToTransfer;
            m_ToTransfer.Clear();
            // Cancel transfers for disabled folders or a previously selected storage root.
            string currentRootIdentity = UserStorage.Backend.RootIdentity;
            var toRemove = m_Transfers.Where(x =>
                    !IsFolderOfTypeSynced(x.Key.Item.FolderType) ||
                    !string.Equals(
                        x.Key.Item.StorageRootIdentity,
                        currentRootIdentity,
                        StringComparison.Ordinal))
                .Select(x => x.Key).ToArray();
            foreach (var transfer in toRemove)
            {
                m_Transfers.TryRemove(transfer, out _);
                transfer.Cancel();
            }
            foreach (var transfer in toRemove)
            {
                try
                {
                    await transfer.Task;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            // We set the new total to be the number of bytes in the files currently in-flight, and add the
            // total number of bytes made up by completely transferred files. This will get increased as the
            // scan is performed, and the new total to transfer will be larger than the old one.
            m_TotalBytesToTransfer = m_Transfers.Keys.Sum(x => x.Item.Size) + m_BytesTransferred;

            await SetupSyncFoldersAsync(token);

            if (!m_IsCancelling)
            {
                var enumerateTasks =
                    m_Folders.Select(x => EnumerateFolderTransfersAsync(x, token)).ToArray();
                await Task.WhenAll(enumerateTasks);
            }
            // now the scan is complete, clear the previous total.
            m_PreviousTotalBytesToTransfer = 0;
        }

        /// Compares the contents of a local folder, and a one on Drive, for parity and creates a queue
        /// of transfers in each direction to sync them.
        private async Task EnumerateFolderTransfersAsync(SyncedFolder folder, CancellationToken token)
        {
            IUserStorageBackend backend = UserStorage.Backend;
            if (!backend.IsReady)
            {
                throw new IOException("User storage is unavailable for Google Drive sync.");
            }
            if (!string.Equals(
                    folder.StorageRootIdentity,
                    backend.RootIdentity,
                    StringComparison.Ordinal))
            {
                throw new OperationCanceledException(
                    "The selected user-storage root changed during Google Drive sync.");
            }

            if (folder.Drive == null && folder.Upload)
            {
                Debug.Log($"Creating new Drive folder called {folder.Name}.");
                folder.Drive = await m_DriveAccess.CreateFolderAsync(
                    folder.Name, folder.ParentDriveId, CancellationToken.None);
            }

            if (folder.Drive == null || m_IsCancelling)
            {
                return;
            }

            StorageDirectoryResult localResult = backend.List(
                folder.Area, folder.RelativeDirectory, token);
            if (!localResult.Success && localResult.Code != StorageResultCode.NotFound)
            {
                throw new IOException(
                    $"Could not enumerate {folder.Area}/{folder.RelativeDirectory}: " +
                    $"{localResult.Error}");
            }
            IReadOnlyList<StorageDocument> localContents = localResult.Success
                ? localResult.Documents
                : Array.Empty<StorageDocument>();
            var driveContents =
                await m_DriveAccess.GetFolderContentsAsync(folder.Drive.Id, true, true,
                    token);
            var driveFiles = new Dictionary<string, DriveData.File>();
            foreach (var item in driveContents
                .Where(x => x.MimeType != "application/vnd.google-apps.folder"))
            {
                if (driveFiles.ContainsKey(item.Name))
                {
                    Debug.LogWarning($"Error! two copies of {folder.Name}/{item.Name} found.");
                    if (item.ModifiedTime > driveFiles[item.Name].ModifiedTime)
                    {
                        driveFiles[item.Name] = item;
                    }
                }
                else
                {
                    driveFiles.Add(item.Name, item);
                }
            }
            var localFiles = new Dictionary<string, StorageDocument>(
                StringComparer.OrdinalIgnoreCase);
            foreach (StorageDocument document in localContents.Where(
                document => !document.IsDirectory))
            {
                if (!localFiles.TryAdd(document.DisplayName, document))
                {
                    throw new IOException(
                        $"Duplicate storage document name: " +
                        $"{CombineLogicalPath(folder.RelativeDirectory, document.DisplayName)}");
                }
            }
            localFiles = localFiles.Values
                .Where(document => FolderIncludesFile(folder, document.DisplayName))
                .ToDictionary(
                    document => document.DisplayName,
                    document => document,
                    StringComparer.OrdinalIgnoreCase);
            driveFiles = driveFiles.Values
                .Where(file => FolderIncludesFile(folder, file.Name))
                .ToDictionary(
                    file => file.Name,
                    file => file,
                    StringComparer.OrdinalIgnoreCase);
            var localSet = new HashSet<string>(
                localFiles.Keys, StringComparer.OrdinalIgnoreCase);
            var driveSet = new HashSet<string>(
                driveFiles.Keys, StringComparer.OrdinalIgnoreCase);

            var allFileNames = new HashSet<string>(
                localSet.Concat(driveSet), StringComparer.OrdinalIgnoreCase);
            foreach (string fileName in allFileNames)
            {
                if (!string.Equals(
                        folder.StorageRootIdentity,
                        backend.RootIdentity,
                        StringComparison.Ordinal))
                {
                    throw new OperationCanceledException(
                        "The selected user-storage root changed during Google Drive sync.");
                }
                localFiles.TryGetValue(fileName, out StorageDocument localFile);
                driveFiles.TryGetValue(fileName, out DriveData.File driveFile);
                string logicalPath = CombineLogicalPath(
                    folder.RelativeDirectory, fileName);
                SyncDecision decision = GetSyncDecision(
                    folder, logicalPath, localFile, driveFile, token);
                if (decision == SyncDecision.None)
                {
                    continue;
                }
                if (decision == SyncDecision.Conflict && !folder.Download)
                {
                    ConfirmLedger(
                        folder.Area,
                        logicalPath,
                        localFile,
                        driveFile,
                        "ConflictDeferred",
                        token);
                    ReportDriveConflict(logicalPath, copied: false);
                    continue;
                }

                bool conflictCopy = decision == SyncDecision.Conflict;
                if (conflictCopy &&
                    TryRecoverCommittedConflictCopy(
                        folder,
                        logicalPath,
                        localFile,
                        driveFile,
                        localFiles,
                        token))
                {
                    continue;
                }
                bool upload = decision == SyncDecision.Upload;
                string destinationName = conflictCopy
                    ? GetDriveConflictName(fileName, driveFile, localSet)
                    : fileName;
                if (conflictCopy)
                {
                    localSet.Add(destinationName);
                }
                if (m_Transfers.Keys.Any(transfer => IsSameStoragePath(
                    transfer.Item,
                    folder.Area,
                    folder.RelativeDirectory,
                    destinationName)))
                {
                    continue;
                }
                var item = new SyncItem
                {
                    Name = destinationName,
                    Area = folder.Area,
                    RelativeDirectory = folder.RelativeDirectory,
                    DocumentId = upload
                        ? localFile.DocumentId
                        : conflictCopy
                            ? default
                            : localFile?.DocumentId ?? default,
                    LedgerDocumentId = localFile?.DocumentId ?? default,
                    StorageRootIdentity = folder.StorageRootIdentity,
                    ParentId = folder.Drive.Id,
                    FileId = driveFile?.Id,
                    DriveFile = driveFile,
                    LedgerRelativePath = logicalPath,
                    ConflictCopy = conflictCopy,
                    Overwrite = !upload && !conflictCopy && localFile != null,
                    LastModified = upload
                        ? localFile.LastModified ?? DateTime.MinValue
                        : driveFile?.ModifiedTime ?? DateTime.MinValue,
                    Upload = upload,
                    Size = upload
                        ? localFile.Size ?? 0
                        : driveFile?.Size ?? 0,
                    FolderType = folder.FolderType,
                };
                m_ToTransfer.Insert(item);
                m_TotalBytesToTransfer += item.Size;
            }

            if (!folder.Recursive)
            {
                return;
            }

            var driveFolders = driveContents
                .Where(x => x.MimeType == "application/vnd.google-apps.folder")
                .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
            var localFolders = localContents
                .Where(document => document.IsDirectory)
                .ToDictionary(
                    document => document.DisplayName,
                    document => document,
                    StringComparer.OrdinalIgnoreCase);
            var folderNames = new HashSet<string>(
                driveFolders.Keys.Concat(localFolders.Keys),
                StringComparer.OrdinalIgnoreCase);
            foreach (var subFolderName in folderNames)
            {
                bool OnDrive = driveFolders.ContainsKey(subFolderName);

                if (m_IsCancelling)
                {
                    return;
                }

                var subfolder = new SyncedFolder
                {
                    Name = subFolderName,
                    Area = folder.Area,
                    RelativeDirectory = CombineLogicalPath(
                        folder.RelativeDirectory, subFolderName),
                    StorageRootIdentity = folder.StorageRootIdentity,
                    Drive = OnDrive ? driveFolders[subFolderName] : null,
                    SyncType = folder.SyncType,
                    Recursive = folder.Recursive,
                    ParentDriveId = folder.Drive.Id,
                    IncludeExtensions = folder.IncludeExtensions,
                    ExcludeExtensions = folder.ExcludeExtensions,
                    FolderType = folder.FolderType,
                };
                await EnumerateFolderTransfersAsync(subfolder, token);
            }
        }

        private static bool FolderIncludesFile(SyncedFolder folder, string displayName)
        {
            string extension = Path.GetExtension(displayName);
            return (folder.IncludeExtensions == null ||
                    folder.IncludeExtensions.Contains(
                        extension, StringComparer.OrdinalIgnoreCase)) &&
                (folder.ExcludeExtensions == null ||
                 !folder.ExcludeExtensions.Contains(
                     extension, StringComparer.OrdinalIgnoreCase));
        }

        private static bool IsDriveNewer(
            DriveData.File driveFile, StorageDocument storageDocument)
        {
            return driveFile.ModifiedTime.HasValue &&
                storageDocument.LastModified.HasValue &&
                (driveFile.ModifiedTime.Value - storageDocument.LastModified.Value)
                    .TotalSeconds >= 2.5;
        }

        private static bool IsStorageDocumentNewer(
            StorageDocument storageDocument, DriveData.File driveFile)
        {
            return driveFile.ModifiedTime.HasValue &&
                storageDocument.LastModified.HasValue &&
                (storageDocument.LastModified.Value - driveFile.ModifiedTime.Value)
                    .TotalSeconds >= 2.5;
        }

        private SyncDecision GetSyncDecision(
            SyncedFolder folder,
            string logicalPath,
            StorageDocument storageDocument,
            DriveData.File driveFile,
            CancellationToken token)
        {
            if (storageDocument == null)
            {
                return driveFile != null && folder.Download
                    ? SyncDecision.Download
                    : SyncDecision.None;
            }
            if (driveFile == null)
            {
                return folder.Upload ? SyncDecision.Upload : SyncDecision.None;
            }
            if (UserStorage.Backend.Kind == StorageBackendKind.Local)
            {
                if (IsDriveNewer(driveFile, storageDocument))
                {
                    return folder.Download
                        ? SyncDecision.Download
                        : SyncDecision.None;
                }
                if (IsStorageDocumentNewer(storageDocument, driveFile))
                {
                    return folder.Upload
                        ? SyncDecision.Upload
                        : SyncDecision.None;
                }
                return SyncDecision.None;
            }

            DriveSyncLedger ledger = GetLedger();
            DriveSyncLedger.Entry entry = ledger.Get(folder.Area, logicalPath);
            ContentHashes hashes = null;
            ContentHashes GetHashes()
            {
                return hashes ?? (hashes = ComputeStorageHashes(
                    UserStorage.Backend, storageDocument.DocumentId, token));
            }

            if (entry != null)
            {
                bool storageMatches = ledger.StorageMatches(
                    entry, storageDocument, () => GetHashes().Sha256);
                if ((folder.Area == StorageArea.Scripts ||
                     folder.Area == StorageArea.Plugins) &&
                    !string.IsNullOrEmpty(entry.StorageSha256))
                {
                    storageMatches = string.Equals(
                        entry.StorageSha256,
                        GetHashes().Sha256,
                        StringComparison.Ordinal);
                }
                bool driveMatches = ledger.DriveMatches(entry, driveFile);
                if (storageMatches && driveMatches)
                {
                    return SyncDecision.None;
                }
                if (!storageMatches && driveMatches)
                {
                    if (!folder.Download &&
                        string.Equals(
                            entry.LastDirection,
                            "ConflictDeferred",
                            StringComparison.Ordinal))
                    {
                        return SyncDecision.Conflict;
                    }
                    return folder.Upload ? SyncDecision.Upload : SyncDecision.None;
                }
                if (storageMatches && !driveMatches)
                {
                    return folder.Download
                        ? SyncDecision.Download
                        : SyncDecision.Conflict;
                }
                return SyncDecision.Conflict;
            }

            if (IsDriveNewer(driveFile, storageDocument))
            {
                return folder.Download
                    ? SyncDecision.Download
                    : SyncDecision.Conflict;
            }
            if (IsStorageDocumentNewer(storageDocument, driveFile))
            {
                return folder.Upload ? SyncDecision.Upload : SyncDecision.None;
            }

            ContentHashes initialHashes = GetHashes();
            if (!string.IsNullOrEmpty(driveFile.Md5Checksum) &&
                string.Equals(
                    initialHashes.Md5,
                    driveFile.Md5Checksum,
                    StringComparison.OrdinalIgnoreCase))
            {
                ledger.Confirm(
                    folder.Area,
                    logicalPath,
                    storageDocument,
                    initialHashes.Sha256,
                    initialHashes.Md5,
                    driveFile,
                    "Matched");
                return SyncDecision.None;
            }
            return SyncDecision.Conflict;
        }

        private DriveSyncLedger GetLedger()
        {
            string accountIdentity = m_GoogleIdentity.Profile?.id;
            if (string.IsNullOrEmpty(accountIdentity) ||
                string.IsNullOrEmpty(m_DriveAccess.DeviceFolder))
            {
                throw new IOException(
                    "Google Drive sync identities are unavailable.");
            }
            string identity = $"{accountIdentity}\n{m_DriveAccess.DeviceFolder}\n" +
                $"{UserStorage.Backend.RootIdentity}";
            lock (m_LedgerGate)
            {
                if (m_Ledger == null ||
                    !string.Equals(
                        identity, m_LedgerIdentity, StringComparison.Ordinal))
                {
                    m_Ledger = new DriveSyncLedger(
                        accountIdentity,
                        m_DriveAccess.DeviceFolder,
                        UserStorage.Backend.RootIdentity);
                    m_LedgerIdentity = identity;
                }
                return m_Ledger;
            }
        }

        private void ConfirmLedger(
            StorageArea area,
            string logicalPath,
            StorageDocument storageDocument,
            DriveData.File driveFile,
            string direction,
            CancellationToken token,
            ContentHashes knownStorageHashes = null)
        {
            if (storageDocument == null || driveFile == null)
            {
                return;
            }
            ContentHashes hashes = knownStorageHashes ?? ComputeStorageHashes(
                UserStorage.Backend, storageDocument.DocumentId, token);
            GetLedger().Confirm(
                area,
                logicalPath,
                storageDocument,
                hashes.Sha256,
                hashes.Md5,
                driveFile,
                direction);
        }

        private static ContentHashes ComputeStorageHashes(
            IUserStorageBackend backend,
            StorageDocumentId documentId,
            CancellationToken token)
        {
            byte[] buffer = new byte[64 * 1024];
            using (Stream stream = backend.OpenRead(
                documentId, requireSeekable: false, token))
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                    md5.TransformBlock(buffer, 0, read, null, 0);
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return new ContentHashes(
                    ToHex(sha256.Hash), ToHex(md5.Hash));
            }
        }

        private sealed class ContentHashes
        {
            public string Sha256 { get; }
            public string Md5 { get; }

            public ContentHashes(string sha256, string md5)
            {
                Sha256 = sha256;
                Md5 = md5;
            }
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static string GetDriveConflictName(
            string fileName,
            DriveData.File driveFile,
            HashSet<string> reservedNames)
        {
            string extension = Path.GetExtension(fileName);
            string baseName = GetDriveConflictBaseName(fileName, driveFile);
            for (int suffix = 0; suffix < 10000; ++suffix)
            {
                string candidate = suffix == 0
                    ? $"{baseName}{extension}"
                    : $"{baseName}-{suffix}{extension}";
                if (!reservedNames.Contains(candidate))
                {
                    return candidate;
                }
            }
            throw new IOException(
                $"Could not reserve a Drive conflict name for {fileName}.");
        }

        private bool TryRecoverCommittedConflictCopy(
            SyncedFolder folder,
            string logicalPath,
            StorageDocument canonicalDocument,
            DriveData.File driveFile,
            IReadOnlyDictionary<string, StorageDocument> localFiles,
            CancellationToken token)
        {
            if (canonicalDocument == null ||
                driveFile == null ||
                string.IsNullOrEmpty(driveFile.Md5Checksum))
            {
                return false;
            }
            string fileName = Path.GetFileName(logicalPath);
            string extension = Path.GetExtension(fileName);
            string baseName = GetDriveConflictBaseName(fileName, driveFile);
            foreach (KeyValuePair<string, StorageDocument> candidate in localFiles)
            {
                if (!IsConflictCopyName(
                        candidate.Key, baseName, extension))
                {
                    continue;
                }
                ContentHashes hashes = ComputeStorageHashes(
                    UserStorage.Backend, candidate.Value.DocumentId, token);
                if (!string.Equals(
                        hashes.Md5,
                        driveFile.Md5Checksum,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                ConfirmLedger(
                    folder.Area,
                    logicalPath,
                    canonicalDocument,
                    driveFile,
                    "ConflictRecovered",
                    token);
                ReportDriveConflict(
                    CombineLogicalPath(folder.RelativeDirectory, candidate.Key),
                    copied: true);
                return true;
            }
            return false;
        }

        private static string GetDriveConflictBaseName(
            string fileName, DriveData.File driveFile)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName);
            DateTime timestamp = driveFile?.ModifiedTime ??
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string identity = SafTransactionJournal.GetRootNamespaceId(
                driveFile?.Id ?? fileName).Substring(0, 8);
            return $"{stem}.drive-conflict-{timestamp:yyyyMMdd-HHmmss}-{identity}";
        }

        private static bool IsConflictCopyName(
            string fileName, string baseName, string extension)
        {
            if (!string.Equals(
                    Path.GetExtension(fileName),
                    extension,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string stem = Path.GetFileNameWithoutExtension(fileName);
            if (string.Equals(stem, baseName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            string prefix = $"{baseName}-";
            return stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                stem.Substring(prefix.Length).All(character =>
                    character >= '0' && character <= '9');
        }

        private static void ReportDriveConflict(string logicalPath, bool copied)
        {
            string message = copied
                ? $"Google Drive conflict preserved as a separate file: {logicalPath}"
                : $"Google Drive conflict retained without overwriting device content: " +
                  $"{logicalPath}";
            Debug.LogWarning($"DRIVE_CONFLICT {message}");
            ControllerConsoleScript.m_Instance?.AddNewLine(message, bNotify: true);
        }

        private static bool IsSameStoragePath(
            SyncItem item,
            StorageArea area,
            string relativeDirectory,
            string displayName)
        {
            return item.Area == area &&
                string.Equals(
                    item.RelativeDirectory,
                    relativeDirectory,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    item.Name, displayName, StringComparison.OrdinalIgnoreCase);
        }

        private static string CombineLogicalPath(string directory, string name)
        {
            return string.IsNullOrEmpty(directory)
                ? name
                : $"{directory.TrimEnd('/', '\\')}/{name}";
        }

        private static string GetLogicalDirectory(string relativePath)
        {
            int separator = relativePath?.LastIndexOf('/') ?? -1;
            return separator < 0 ? "" : relativePath.Substring(0, separator);
        }

        // Update checks to see if any transfer tasks are ready to be performed and kicks them off
        // Currently, only one upload and one download happen at the same time.
        private async Task ManageTransfersAsync(CancellationToken token)
        {
            var updateWait = new WaitForUpdate();
            while (!token.IsCancellationRequested)
            {
                await updateWait;
                if (m_ToTransfer.Count == 0 && !m_Transfers.Any())
                {
                    continue;
                }

                // Clear out completed transfer tasks
                foreach (var task in m_Transfers.Keys.Where(x => x.Task.IsCompleted &&
                    x.Task.Exception != null))
                {
                    Debug.LogException(task.Task.Exception);
                }

                var toRemove = m_Transfers.Keys.Where(x => x.Task.IsCompleted).ToArray();
                foreach (var transfer in toRemove)
                {
                    m_BytesTransferred += transfer.BytesTransferred;
                    m_Transfers.TryRemove(transfer, out _);
                }

                // Kick off transfers in empty slots
                while (m_Transfers.Count < 4 && m_ToTransfer.Count > 0)
                {
                    var item = m_ToTransfer.Dequeue();
                    m_Transfers.TryAdd(new Transfer(this, item), null);
                }

                if (m_Transfers.IsEmpty && m_ToTransfer.Count == 0)
                {
                    m_TotalBytesToTransfer = 0;
                    m_PreviousTotalBytesToTransfer = 0;
                    m_BytesTransferred = 0;
                }
            }
        }

        private async Task UploadItemAsync(Transfer transfer, CancellationToken token)
        {
            var item = transfer.Item;
            IUserStorageBackend backend = UserStorage.Backend;
            EnsureTransferRootMatches(item, backend);
            ContentHashes sourceHashes = backend.Kind ==
                StorageBackendKind.StorageAccessFramework
                    ? await Task.Run(
                        () => ComputeStorageHashes(
                            backend, item.DocumentId, token),
                        token)
                    : null;
            var metadata = new DriveData.File
            {
                Name = item.Name,
                Parents = new[] { item.ParentId },
            };
            if (item.LastModified != DateTime.MinValue)
            {
                metadata.ModifiedTime = item.LastModified;
            }
            switch (Path.GetExtension(item.Name))
            {
                case ".tilt":
                    metadata.MimeType = "application/octet-stream";
                    metadata.ContentHints = await CreateTiltFileContentHintsAsync(
                        backend, item.DocumentId, item.Name);
                    break;
                case ".jpg":
                case ".jpeg":
                    metadata.MimeType = "image/jpeg";
                    break;
                case ".png":
                    metadata.MimeType = "image/png";
                    break;
                case ".obj":
                case ".fbx":
                case ".gltf":
                case ".glb":
                    metadata.MimeType = "application/octet-stream";
                    break;
                case ".mp4":
                case ".m4v":
                    metadata.MimeType = "video/mp4";
                    break;
                case ".avi":
                    metadata.MimeType = "video/x-msvideo";
                    break;
                case ".mov":
                    metadata.MimeType = "video/quicktime";
                    break;
                case ".3gp":
                    metadata.MimeType = "video/3gpp";
                    break;
                case ".webm":
                    metadata.MimeType = "video/webm";
                    break;
                case ".lua":
                    metadata.MimeType = "text/x-lua";
                    break;
                case ".html":
                    metadata.MimeType = "text/html";
                    break;
                case ".svg":
                    metadata.MimeType = "image/svg+xml";
                    break;
            }

            try
            {
                DriveData.File committedDriveFile;
                using (Stream stream = backend.OpenRead(
                    item.DocumentId, requireSeekable: true, token))
                {
                    if (item.FileId == null)
                    {
                        committedDriveFile = await m_DriveAccess.UploadFileAsync(
                            metadata, stream, token, transfer);
                    }
                    else
                    {
                        metadata.Id = item.FileId;
                        committedDriveFile = await m_DriveAccess.UpdateFileAsync(
                            metadata, stream, token, transfer);
                    }
                }
                if (committedDriveFile == null)
                {
                    throw new IOException(
                        "Google Drive upload completed without committed metadata.");
                }
                if (string.IsNullOrEmpty(committedDriveFile.Id))
                {
                    committedDriveFile.Id = item.FileId;
                }
                EnsureTransferRootMatches(item, backend);
                StorageDocument source = FindStorageDocument(
                    backend,
                    item.Area,
                    item.RelativeDirectory,
                    item.Name,
                    item.DocumentId,
                    token);
                if (source == null)
                {
                    throw new IOException(
                        "Google Drive upload source changed before confirmation.");
                }
                if (backend.Kind == StorageBackendKind.StorageAccessFramework)
                {
                    ContentHashes currentHashes = await Task.Run(
                        () => ComputeStorageHashes(
                            backend, source.DocumentId, token),
                        token);
                    if (!source.DocumentId.Equals(item.DocumentId) ||
                        !string.Equals(
                            sourceHashes.Sha256,
                            currentHashes.Sha256,
                            StringComparison.Ordinal))
                    {
                        throw new IOException(
                            "Google Drive upload source changed during transfer.");
                    }
                    ConfirmLedger(
                        item.Area,
                        item.LedgerRelativePath,
                        source,
                        committedDriveFile,
                        "Upload",
                        token,
                        currentHashes);
                }
                item.DriveFile = committedDriveFile;
            }
            catch (IOException ex)
            {
                // Something went wrong with accessing the local file. Ignore for now, hopefully it will work
                // next time around.
                throw new IOException(ex.Message);
            }
            if (DriveIsLowOnSpace && !token.IsCancellationRequested)
            {
                await ClearAllTransfers();
                Debug.Log(
                    $"Backup stopped. User has {m_DriveAccess.DriveFreeSpace} remaining. " +
                    $"User must have at least {kMinimumFreeSpace} free to backup.\n" +
                    $"At least {kMinimumFreeSpace - m_DriveAccess.DriveFreeSpace} more bytes required.");
                ControllerConsoleScript.m_Instance.AddNewLine(
                    "Google Drive low space warning! Drive backup stopped.", bNotify: true);
            }
            EnsureTransferRootMatches(item, backend);
            if (Path.GetExtension(item.Name) == ".tilt")
            {
                var driveSet = SketchCatalog.m_Instance.GetSet(SketchSetType.Drive);
                if (item.FileId == null)
                {
                    driveSet.NotifySketchCreated(null);
                }
                else
                {
                    driveSet.NotifySketchChanged(null);
                }
            }
        }

        private async Task<DriveData.File.ContentHintsData> CreateTiltFileContentHintsAsync(
            IUserStorageBackend backend,
            StorageDocumentId documentId,
            string displayName)
        {
            var hints = new DriveData.File.ContentHintsData();
            var tiltFile = new TiltFile(
                new BackendReadStreamSource(backend, documentId), displayName);
            using (Stream thumbStream = tiltFile.GetReadStream(TiltFile.FN_THUMBNAIL))
            using (var thumbnail = new MemoryStream())
            {
                await thumbStream.CopyToAsync(thumbnail);
                byte[] thumbBytes = thumbnail.ToArray();

                // The thumbnail has to be encoded as URL-safe Base64, which is not the Base64 that C# encodes
                // to. (RFC 4648 section 5). This section converts it to be url-safe.
                byte[] base64 = Encoding.ASCII.GetBytes(Convert.ToBase64String(thumbBytes));
                for (int i = 0; i < base64.Length; ++i)
                {
                    if (base64[i] == (byte)'+')
                    {
                        base64[i] = (byte)'-';
                    }
                    else if (base64[i] == (byte)'/')
                    {
                        base64[i] = (byte)'_';
                    }
                }

                hints.Thumbnail = new DriveData.File.ContentHintsData.ThumbnailData
                {
                    Image = Encoding.ASCII.GetString(base64),
                    MimeType = TiltFile.THUMBNAIL_MIME_TYPE,
                };
            }
            return hints;
        }

        private async Task DownloadItemAsync(Transfer transfer, CancellationToken token)
        {
            var item = transfer.Item;
            IUserStorageBackend backend = UserStorage.Backend;
            EnsureTransferRootMatches(item, backend);
            if (backend.Kind == StorageBackendKind.Local)
            {
                await DownloadLocalItemAsync(transfer, backend, token);
                return;
            }

            string relativePath = CombineLogicalPath(
                item.RelativeDirectory, item.Name);
            StorageMutationResult commit;
            using (IStorageWriteTransaction transaction = backend.BeginWrite(
                item.Area,
                relativePath,
                StorageMimeTypes.ForPath(item.Name),
                token,
                item.DocumentId))
            {
                using (Stream stream = transaction.OpenWrite())
                {
                    await m_DriveAccess.DownloadFileAsync(
                        item.FileId, stream, token, transfer);
                }
                EnsureTransferRootMatches(item, backend);
                commit = transaction.Commit();
            }
            if (!commit.Success)
            {
                throw new IOException(commit.Error);
            }
            EnsureTransferRootMatches(item, backend);
            StorageDocument committedDocument = FindStorageDocument(
                backend,
                item.Area,
                item.RelativeDirectory,
                item.Name,
                commit.DocumentId,
                token);
            if (committedDocument == null)
            {
                throw new IOException(
                    "Drive download committed but destination metadata is unavailable.");
            }
            Exception confirmationError = null;
            try
            {
                ConfirmDownloadedItem(item, backend, committedDocument, token);
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException ||
                e is InvalidOperationException)
            {
                confirmationError = e;
            }
            if (item.Area == StorageArea.Scripts ||
                item.Area == StorageArea.Plugins ||
                item.Area == StorageArea.Fonts)
            {
                RuntimeProjectionResult refresh =
                    await UserRuntimeContent.Instance.EnsureCurrentAsync(
                        item.Area, token);
                if (!refresh.Success)
                {
                    throw new IOException(
                        $"Drive download committed but runtime refresh failed: {refresh.Error}");
                }
            }
            if (confirmationError != null)
            {
                throw new IOException(
                    "Drive download committed but sync state could not be recorded.",
                    confirmationError);
            }
        }

        private async Task<StorageDocument> DownloadLocalItemAsync(
            Transfer transfer,
            IUserStorageBackend backend,
            CancellationToken token)
        {
            SyncItem item = transfer.Item;
            string path = GetLocalSyncPath(item);
            string tempPath = path + ".partial";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                // The following moves the file to the recycling bin, which is safer in case we do the wrong
                // thing in overwriting a file.
                FileUtils.DeleteWithRecycleBin(path);
            }
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await m_DriveAccess.DownloadFileAsync(item.FileId, stream, token, transfer);
                }
                File.Move(tempPath, path);
                File.SetLastWriteTime(path, item.LastModified);
            }
            catch (Exception)
            {
                // If there's an exception partway through - delete the partial file.
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                throw;
            }
            EnsureTransferRootMatches(item, backend);
            if (Path.GetExtension(path) == ".tilt")
            {
                if (item.Overwrite)
                {
                    SketchCatalog.m_Instance.NotifyUserFileChanged(path);
                }
                else
                {
                    SketchCatalog.m_Instance.NotifyUserFileCreated(path);
                }
            }
            StorageDocument document = FindStorageDocument(
                backend,
                item.Area,
                item.RelativeDirectory,
                item.Name,
                new StorageDocumentId(path),
                token);
            if (document == null)
            {
                throw new IOException(
                    "Drive download destination metadata is unavailable.");
            }
            return document;
        }

        private void ConfirmDownloadedItem(
            SyncItem item,
            IUserStorageBackend backend,
            StorageDocument downloadedDocument,
            CancellationToken token)
        {
            StorageDocument ledgerDocument = item.ConflictCopy
                ? FindStorageDocument(
                    backend,
                    item.Area,
                    GetLogicalDirectory(item.LedgerRelativePath),
                    Path.GetFileName(item.LedgerRelativePath),
                    item.LedgerDocumentId,
                    token)
                : downloadedDocument;
            if (ledgerDocument == null || item.DriveFile == null)
            {
                throw new IOException(
                    "Drive download cannot be recorded in the sync ledger.");
            }
            ConfirmLedger(
                item.Area,
                item.LedgerRelativePath,
                ledgerDocument,
                item.DriveFile,
                item.ConflictCopy ? "ConflictCopy" : "Download",
                token);
            if (item.ConflictCopy)
            {
                ReportDriveConflict(
                    CombineLogicalPath(item.RelativeDirectory, item.Name),
                    copied: true);
            }
        }

        private static StorageDocument FindStorageDocument(
            IUserStorageBackend backend,
            StorageArea area,
            string relativeDirectory,
            string displayName,
            StorageDocumentId preferredId,
            CancellationToken token)
        {
            StorageDirectoryResult listing = backend.List(
                area, relativeDirectory, token);
            if (!listing.Success)
            {
                return null;
            }
            if (preferredId.IsValid)
            {
                StorageDocument byId = listing.Documents.FirstOrDefault(
                    document => document.DocumentId.Equals(preferredId));
                if (byId != null)
                {
                    return byId;
                }
            }
            return listing.Documents.FirstOrDefault(document =>
                string.Equals(
                    document.DisplayName,
                    displayName,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureTransferRootMatches(
            SyncItem item, IUserStorageBackend backend)
        {
            if (!ReferenceEquals(backend, UserStorage.Backend) ||
                !backend.IsReady ||
                !string.Equals(
                    item.StorageRootIdentity,
                    backend.RootIdentity,
                    StringComparison.Ordinal))
            {
                throw new OperationCanceledException(
                    "The selected user-storage root changed during Google Drive transfer.");
            }
        }

        private static string GetLocalSyncPath(SyncItem item)
        {
            string root = Path.GetFullPath(
                LocalUserStorageBackend.GetAreaRoot(item.Area));
            string relativePath = CombineLogicalPath(
                item.RelativeDirectory, item.Name)
                .Replace('/', Path.DirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(root, relativePath));
            string prefix = root.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!path.StartsWith(prefix, comparison))
            {
                throw new IOException("Google Drive destination escapes its storage area.");
            }
            return path;
        }

        public async Task CancelTransferAsync(string filename)
        {
            if (!Initialized)
            {
                return;
            }
            string name = Path.GetFileName(filename);
            var transfer = m_Transfers.Keys.FirstOrDefault(x =>
                x.Item.Name == name &&
                x.Item.DocumentId.IsValid &&
                string.Equals(
                    x.Item.DocumentId.Value, filename, StringComparison.OrdinalIgnoreCase));
            if (transfer != null)
            {
                transfer.TaskAndCts.Cancel();
                try
                {
                    await transfer.Task;
                }
                catch (OperationCanceledException) { }
            }
        }
    }
} // namespace TiltBrush
