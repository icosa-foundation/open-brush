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
using System.Threading;
using UnityEngine;

namespace TiltBrush
{
    public static class OpenBrushStorage
    {
        public static bool IsGooglePlayStorageMode
        {
            get
            {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
                return Application.platform == RuntimePlatform.Android;
#else
                return false;
#endif
            }
        }

        public static string LocalUserPathRoot
        {
            get
            {
                return Path.Combine(Application.persistentDataPath, "OpenBrushWorkingCache");
            }
        }

        public static string LocalExportStagingPath
        {
            get
            {
                return Path.Combine(LocalStagingPath, "Exports");
            }
        }

        public static string LocalStagingPath =>
            Path.Combine(Application.temporaryCachePath, "OpenBrushSafStaging");

        public static string LocalSnapshotStagingPath =>
            Path.Combine(LocalStagingPath, "Snapshots");

        public static string LocalVideoStagingPath =>
            Path.Combine(LocalStagingPath, "Videos");

        public static string LocalVrVideoStagingPath =>
            Path.Combine(LocalStagingPath, "VRVideos");

        public static string LocalMaterializedMediaLibraryPath
        {
            get
            {
                string rootId = UserStorage.Backend.Kind ==
                    StorageBackendKind.StorageAccessFramework
                    ? UserStorage.Backend.RootIdentity
                    : "";
                return Path.Combine(
                    Application.persistentDataPath,
                    "OpenBrushSafMaterialized",
                    SafTransactionJournal.GetRootNamespaceId(rootId),
                    "Media Library");
            }
        }

        public static string SharedExportDisplayPath
        {
            get { return "Open Brush/Exports"; }
        }

        public static string GetSafRootScopedPreferenceKey(
            string baseKey, string rootIdentity = null)
        {
            string identity = rootIdentity ?? UserStorage.Backend.RootIdentity;
            return $"{baseKey}.{SafTransactionJournal.GetRootNamespaceId(identity)}";
        }

        public static bool TryGetSharedGeneratedFileRelativePath(
            string localPath, out string relativePath)
        {
            relativePath = null;

            if (string.IsNullOrEmpty(localPath))
            {
                return false;
            }

            if (TryGetRelativePath(App.SnapshotPath(), localPath, out string snapshotPath))
            {
                relativePath = Path.Combine("Snapshots", snapshotPath);
                return true;
            }

            if (TryGetRelativePath(App.VideosPath(), localPath, out string videoPath))
            {
                relativePath = Path.Combine("Videos", videoPath);
                return true;
            }

            if (TryGetRelativePath(App.VrVideosPath(), localPath, out string vrVideoPath))
            {
                relativePath = Path.Combine("VRVideos", vrVideoPath);
                return true;
            }

            return false;
        }

        public static bool TryGetSharedMediaLibraryRelativePath(
            string localPath, out string relativePath)
        {
            relativePath = null;

            if (string.IsNullOrEmpty(localPath))
            {
                return false;
            }

            if (TryGetRelativePath(App.MediaLibraryPath(), localPath, out string mediaPath))
            {
                relativePath = Path.Combine("Media Library", mediaPath);
                return true;
            }

            return false;
        }

        public static bool PublishGeneratedFileToSharedStorage(
            string localPath, out string error)
        {
            error = null;

            if (!IsGooglePlayStorageMode ||
                !TryGetSharedGeneratedFileRelativePath(localPath, out string relativePath))
            {
                return true;
            }

            return PublishPathToSharedStorage(
                relativePath, localPath, transactionOwnsPayload: true, out error);
        }

        public static void PublishGeneratedFileToSharedStorageAsync(
            string localPath, string label, Action<bool, string> onComplete)
        {
            if (!IsGooglePlayStorageMode ||
                !TryGetSharedGeneratedFileRelativePath(localPath, out string relativePath))
            {
                onComplete?.Invoke(true, null);
                return;
            }

            PublishPathToSharedStorageAsync(
                relativePath,
                localPath,
                label,
                transactionOwnsPayload: true,
                onComplete);
        }

        public static void PublishGeneratedFilesToSharedStorageAsync(
            IReadOnlyList<string> localPaths,
            string label,
            Action<bool, string> onComplete)
        {
            if (!IsGooglePlayStorageMode || localPaths == null || localPaths.Count == 0)
            {
                onComplete?.Invoke(true, null);
                return;
            }
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                StorageArea? bundleArea = null;
                var stagedPaths = new List<SafStagedPath>();
                foreach (string localPath in localPaths)
                {
                    if (!TryGetSharedGeneratedFileRelativePath(
                            localPath, out string sharedRelativePath) ||
                        !TryResolveStorageDestination(
                            sharedRelativePath, out StorageArea area, out string areaRelativePath))
                    {
                        onComplete?.Invoke(
                            false, $"Unsupported generated output path: {localPath}");
                        return;
                    }
                    if (bundleArea.HasValue && bundleArea.Value != area)
                    {
                        onComplete?.Invoke(
                            false, "Generated output bundle spans multiple storage areas.");
                        return;
                    }
                    bundleArea = area;
                    stagedPaths.Add(new SafStagedPath(localPath, areaRelativePath));
                }
                AndroidStorageManager.StartStorageOperation(
                    label,
                    () => SafStagedOutputPublisher.PublishBundle(
                        UserStorage.Backend,
                        bundleArea.Value,
                        stagedPaths,
                        transactionOwnsPayload: true,
                        CancellationToken.None),
                    onComplete);
                return;
            }

            int index = 0;
            void PublishNext()
            {
                if (index >= localPaths.Count)
                {
                    onComplete?.Invoke(true, null);
                    return;
                }
                PublishGeneratedFileToSharedStorageAsync(
                    localPaths[index++],
                    label,
                    (success, error) =>
                    {
                        if (success)
                        {
                            PublishNext();
                        }
                        else
                        {
                            onComplete?.Invoke(false, error);
                        }
                    });
            }
            PublishNext();
        }

        public static bool PublishMediaLibraryPathToSharedStorage(
            string localPath, out string error)
        {
            error = null;

            if (!IsGooglePlayStorageMode ||
                !TryGetSharedMediaLibraryRelativePath(localPath, out string relativePath))
            {
                return true;
            }

            return PublishPathToSharedStorage(
                relativePath, localPath, transactionOwnsPayload: false, out error);
        }

        public static void PublishMediaLibraryPathToSharedStorageAsync(
            string localPath, string label, Action<bool, string> onComplete)
        {
            if (!IsGooglePlayStorageMode ||
                !TryGetSharedMediaLibraryRelativePath(localPath, out string relativePath))
            {
                onComplete?.Invoke(true, null);
                return;
            }

            PublishPathToSharedStorageAsync(
                relativePath,
                localPath,
                label,
                transactionOwnsPayload: false,
                onComplete);
        }

        public static bool PublishVideoCaptureToSharedStorage(
            string localVideoPath, out string error)
        {
            error = null;

            if (!IsGooglePlayStorageMode ||
                !TryGetSharedGeneratedFileRelativePath(localVideoPath, out _))
            {
                return true;
            }

            if (File.Exists(localVideoPath))
            {
                return PublishGeneratedFileToSharedStorage(localVideoPath, out error);
            }

            string directory = Path.GetDirectoryName(localVideoPath);
            string basename = Path.GetFileNameWithoutExtension(localVideoPath);
            string frameDirectory = Path.Combine(directory, basename + "_frames");
            string metadataPath = Path.Combine(directory, basename + "_sequence.txt");

            if (!Directory.Exists(frameDirectory))
            {
                error = "Local video capture output does not exist: " + localVideoPath;
                return false;
            }

            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                var stagedPaths = new List<SafStagedPath>
                {
                    new SafStagedPath(frameDirectory, Path.GetFileName(frameDirectory)),
                };
                if (File.Exists(metadataPath))
                {
                    stagedPaths.Add(new SafStagedPath(
                        metadataPath, Path.GetFileName(metadataPath)));
                }
                StorageArea area = localVideoPath.StartsWith(
                    App.VrVideosPath(), StringComparison.OrdinalIgnoreCase)
                    ? StorageArea.VrVideos
                    : StorageArea.Videos;
                SafPublicationResult result = SafStagedOutputPublisher.PublishBundle(
                    UserStorage.Backend,
                    area,
                    stagedPaths,
                    transactionOwnsPayload: true,
                    CancellationToken.None);
                error = result.Error;
                return result.Success;
            }

            if (!PublishGeneratedFileToSharedStorage(frameDirectory, out error))
            {
                return false;
            }

            if (File.Exists(metadataPath) &&
                !PublishGeneratedFileToSharedStorage(metadataPath, out error))
            {
                return false;
            }

            return true;
        }

        public static void PublishVideoCaptureToSharedStorageAsync(
            string localVideoPath, string label, Action<bool, string> onComplete)
        {
            if (!IsGooglePlayStorageMode ||
                !TryGetSharedGeneratedFileRelativePath(localVideoPath, out _))
            {
                onComplete?.Invoke(true, null);
                return;
            }

            if (File.Exists(localVideoPath))
            {
                PublishGeneratedFileToSharedStorageAsync(localVideoPath, label, onComplete);
                return;
            }

            string directory = Path.GetDirectoryName(localVideoPath);
            string basename = Path.GetFileNameWithoutExtension(localVideoPath);
            string frameDirectory = Path.Combine(directory, basename + "_frames");
            string metadataPath = Path.Combine(directory, basename + "_sequence.txt");

            if (!Directory.Exists(frameDirectory))
            {
                onComplete?.Invoke(false, "Local video capture output does not exist: " + localVideoPath);
                return;
            }

            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                var stagedPaths = new List<SafStagedPath>
                {
                    new SafStagedPath(frameDirectory, Path.GetFileName(frameDirectory)),
                };
                if (File.Exists(metadataPath))
                {
                    stagedPaths.Add(new SafStagedPath(
                        metadataPath, Path.GetFileName(metadataPath)));
                }
                StorageArea area = localVideoPath.StartsWith(
                    App.VrVideosPath(), StringComparison.OrdinalIgnoreCase)
                    ? StorageArea.VrVideos
                    : StorageArea.Videos;
                AndroidStorageManager.StartStorageOperation(
                    label,
                    () => SafStagedOutputPublisher.PublishBundle(
                        UserStorage.Backend,
                        area,
                        stagedPaths,
                        transactionOwnsPayload: true,
                        CancellationToken.None),
                    onComplete);
                return;
            }

            PublishGeneratedFileToSharedStorageAsync(frameDirectory, label, (framesCopied, frameError) =>
            {
                if (!framesCopied)
                {
                    onComplete?.Invoke(false, frameError);
                    return;
                }

                if (!File.Exists(metadataPath))
                {
                    onComplete?.Invoke(true, null);
                    return;
                }

                PublishGeneratedFileToSharedStorageAsync(
                    metadataPath, label + " metadata", onComplete);
            });
        }

        public static void PublishExportToSharedStorageAsync(
            string localExportDirectory,
            string localReadmePath,
            Action<bool, string> onComplete)
        {
            if (!IsGooglePlayStorageMode)
            {
                onComplete?.Invoke(true, null);
                return;
            }

            string exportName = Path.GetFileName(localExportDirectory);
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                var stagedPaths = new List<SafStagedPath>
                {
                    new SafStagedPath(localExportDirectory, exportName),
                    new SafStagedPath(localReadmePath, "README.txt"),
                };
                AndroidStorageManager.StartStorageOperation(
                    $"export {exportName}",
                    () => SafStagedOutputPublisher.PublishBundle(
                        UserStorage.Backend,
                        StorageArea.Exports,
                        stagedPaths,
                        transactionOwnsPayload: true,
                        CancellationToken.None),
                    onComplete);
                return;
            }

            string relativeExportPath = Path.Combine("Exports", exportName);
            PublishPathToSharedStorageAsync(
                relativeExportPath,
                localExportDirectory,
                "export " + exportName,
                transactionOwnsPayload: true,
                (exportCopied, exportError) =>
                {
                    if (!exportCopied)
                    {
                        onComplete?.Invoke(false, exportError);
                        return;
                    }

                    PublishPathToSharedStorageAsync(
                        Path.Combine("Exports", "README.txt"),
                        localReadmePath,
                        "export README",
                        transactionOwnsPayload: true,
                        onComplete);
                });
        }

        private static void PublishPathToSharedStorageAsync(
            string relativePath,
            string localPath,
            string label,
            bool transactionOwnsPayload,
            Action<bool, string> onComplete)
        {
            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                onComplete?.Invoke(false, "SAF storage backend is unavailable.");
                return;
            }
            if (!TryResolveStorageDestination(
                    relativePath, out StorageArea area, out string areaRelativePath))
            {
                onComplete?.Invoke(
                    false, $"Unsupported shared-storage destination: {relativePath}");
                return;
            }
            AndroidStorageManager.StartStorageOperation(
                label,
                () => SafStagedOutputPublisher.Publish(
                    UserStorage.Backend,
                    area,
                    areaRelativePath,
                    localPath,
                    transactionOwnsPayload,
                    CancellationToken.None),
                onComplete);
        }

        private static bool PublishPathToSharedStorage(
            string relativePath,
            string localPath,
            bool transactionOwnsPayload,
            out string error)
        {
            error = null;
            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                error = "SAF storage backend is unavailable.";
                return false;
            }
            if (!TryResolveStorageDestination(
                    relativePath, out StorageArea area, out string areaRelativePath))
            {
                error = $"Unsupported shared-storage destination: {relativePath}";
                return false;
            }
            SafPublicationResult result = SafStagedOutputPublisher.Publish(
                UserStorage.Backend,
                area,
                areaRelativePath,
                localPath,
                transactionOwnsPayload,
                CancellationToken.None);
            error = result.Error;
            return result.Success;
        }

        private static bool TryResolveStorageDestination(
            string sharedRelativePath,
            out StorageArea area,
            out string areaRelativePath)
        {
            string normalized = (sharedRelativePath ?? "").Replace('\\', '/').Trim('/');
            (string prefix, StorageArea area)[] mappings =
            {
                ("Media Library/BackgroundImages", StorageArea.MediaLibraryBackgroundImages),
                ("Media Library/Images", StorageArea.MediaLibraryImages),
                ("Media Library/Models", StorageArea.MediaLibraryModels),
                ("Media Library/Videos", StorageArea.MediaLibraryVideos),
                ("Saved Strokes", StorageArea.SavedStrokes),
                ("Sketches", StorageArea.Sketches),
                ("Snapshots", StorageArea.Snapshots),
                ("VRVideos", StorageArea.VrVideos),
                ("Videos", StorageArea.Videos),
                ("Exports", StorageArea.Exports),
            };
            foreach ((string prefix, StorageArea mappedArea) in mappings)
            {
                if (normalized == prefix)
                {
                    area = mappedArea;
                    areaRelativePath = "";
                    return true;
                }
                string prefixWithSeparator = $"{prefix}/";
                if (normalized.StartsWith(
                        prefixWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    area = mappedArea;
                    areaRelativePath = normalized.Substring(prefixWithSeparator.Length);
                    return true;
                }
            }
            area = default;
            areaRelativePath = null;
            return false;
        }

        private static bool TryGetRelativePath(string root, string path, out string relativePath)
        {
            relativePath = null;

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(path);
            if (fullPath == fullRoot)
            {
                relativePath = "";
                return true;
            }

            string rootWithSeparator = fullRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator))
            {
                return false;
            }

            relativePath = fullPath.Substring(rootWithSeparator.Length);
            return true;
        }

    }
}
