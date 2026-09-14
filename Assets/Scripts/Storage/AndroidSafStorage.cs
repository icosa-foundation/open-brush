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
using Microsoft.Win32.SafeHandles;
using UnityEngine;

namespace TiltBrush
{
    public static class AndroidSafStorage
    {
        private const string kBridgeClass = "foundation.icosa.openbrush.storage.OpenBrushStorageBridge";
        private static readonly object sm_ReadinessGate = new object();
        private static bool sm_HasCachedReadiness;
        private static bool sm_CachedReadiness;
        private static long sm_ReadinessCheckedTimestamp;

        public static bool IsAvailable
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

        public static void RequestOpenBrushFolder()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            bridge.CallStatic("requestOpenBrushFolder", GetActivity());
#endif
        }

        public static bool HasOpenBrushFolder()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            lock (sm_ReadinessGate)
            {
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                long cacheDuration =
                    System.Diagnostics.Stopwatch.Frequency;
                if (sm_HasCachedReadiness &&
                    now - sm_ReadinessCheckedTimestamp < cacheDuration)
                {
                    return sm_CachedReadiness;
                }
                using var bridge = new AndroidJavaClass(kBridgeClass);
                sm_CachedReadiness =
                    bridge.CallStatic<bool>("hasOpenBrushFolder", GetActivity());
                sm_ReadinessCheckedTimestamp = now;
                sm_HasCachedReadiness = true;
                return sm_CachedReadiness;
            }
#else
            return true;
#endif
        }

        public static void InvalidateReadiness()
        {
            lock (sm_ReadinessGate)
            {
                sm_HasCachedReadiness = false;
            }
        }

        public static string GetOpenBrushFolderDisplayName()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<string>("getOpenBrushFolderDisplayName", GetActivity());
#else
            return App.kAppFolderName;
#endif
        }

        public static string GetSelectedRootIdentity()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<string>("getSelectedRootIdentity", GetActivity());
#else
            return "";
#endif
        }

        public static void ClearOpenBrushFolder()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            bridge.CallStatic("clearOpenBrushFolder", GetActivity());
            InvalidateReadiness();
#endif
        }

        public static bool EnsureDirectory(string relativePath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("ensureDirectory", GetActivity(), relativePath);
#else
            return true;
#endif
        }

        public static StorageDirectoryResult QueryDirectory(string relativePath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            try
            {
                using var bridge = new AndroidJavaClass(kBridgeClass);
                using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                    "queryDirectory", GetActivity(), relativePath);
                if (result == null)
                {
                    return StorageDirectoryResult.Failed(
                        StorageResultCode.ProviderUnavailable,
                        "Provider returned no directory-query result.");
                }

                var code = (StorageResultCode)result.Get<int>("code");
                string error = result.Get<string>("error");
                if (code != StorageResultCode.Success)
                {
                    return StorageDirectoryResult.Failed(code, error);
                }

                string[] documentUris = result.Get<string[]>("documentUris");
                string[] parentDocumentUris = result.Get<string[]>("parentDocumentUris");
                string[] displayNames = result.Get<string[]>("displayNames");
                string[] mimeTypes = result.Get<string[]>("mimeTypes");
                bool[] directories = result.Get<bool[]>("directories");
                long[] sizes = result.Get<long[]>("sizes");
                bool[] hasSizes = result.Get<bool[]>("hasSizes");
                long[] lastModified = result.Get<long[]>("lastModified");
                bool[] hasLastModified = result.Get<bool[]>("hasLastModified");
                long[] flags = result.Get<long[]>("flags");
                string[] relativeDisplayPaths = result.Get<string[]>("relativeDisplayPaths");

                int count = documentUris?.Length ?? 0;
                if (!HaveLength(
                        count,
                        parentDocumentUris,
                        displayNames,
                        mimeTypes,
                        directories,
                        sizes,
                        hasSizes,
                        lastModified,
                        hasLastModified,
                        flags,
                        relativeDisplayPaths))
                {
                    return StorageDirectoryResult.Failed(
                        StorageResultCode.ProviderUnavailable,
                        "Provider returned inconsistent directory-query columns.");
                }

                var documents = new List<StorageDocument>(count);
                for (int i = 0; i < count; ++i)
                {
                    DateTime? modified = hasLastModified[i]
                        ? UnixMillisecondsToLocalDateTime(lastModified[i])
                        : (DateTime?)null;
                    documents.Add(new StorageDocument(
                        new StorageDocumentId(documentUris[i]),
                        new StorageDocumentId(parentDocumentUris[i]),
                        displayNames[i],
                        mimeTypes[i],
                        directories[i],
                        hasSizes[i] ? sizes[i] : (long?)null,
                        modified,
                        flags[i],
                        relativeDisplayPaths[i]));
                }
                return StorageDirectoryResult.Succeeded(documents);
            }
            catch (Exception e)
            {
                return StorageDirectoryResult.Failed(
                    StorageResultCode.ProviderUnavailable,
                    $"Directory query failed: {e.Message}");
            }
#else
            return StorageDirectoryResult.Failed(
                StorageResultCode.NotReady,
                "SAF directory queries are unavailable on this platform.");
#endif
        }

        public static bool TryOpenSeekableReadStream(
            string relativePath, out FileStream stream, out string error)
        {
            stream = null;
            error = null;
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "openFileDescriptor", GetActivity(), relativePath, "rw");
            return TryCreateFileStream(
                result, FileAccess.Read, out stream, out _, out error);
#else
            error = "SAF file descriptors are unavailable on this platform.";
            return false;
#endif
        }

        public static bool TryOpenSeekableReadStream(
            StorageDocumentId documentId, out FileStream stream, out string error)
        {
            stream = null;
            error = null;
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "openDocumentFileDescriptor", GetActivity(), documentId.Value, "r");
            return TryCreateFileStream(
                result, FileAccess.Read, out stream, out _, out error);
#else
            error = "SAF file descriptors are unavailable on this platform.";
            return false;
#endif
        }

        public static bool TryCreateTemporaryFileStream(
            string relativeDirectory,
            string targetFileName,
            string mimeType,
            out FileStream stream,
            out string documentUri,
            out string error)
        {
            stream = null;
            documentUri = null;
            error = null;
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "createTemporaryFileDescriptor",
                GetActivity(),
                relativeDirectory,
                targetFileName,
                mimeType);
            return TryCreateFileStream(
                result, FileAccess.ReadWrite, out stream, out documentUri, out error);
#else
            error = "SAF file descriptors are unavailable on this platform.";
            return false;
#endif
        }

        public static bool TryCreateNamedFileStream(
            string relativeDirectory,
            string displayName,
            string mimeType,
            out FileStream stream,
            out StorageDocumentId documentId,
            out string error)
        {
            stream = null;
            documentId = default;
            error = null;
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "createNamedFileDescriptor",
                GetActivity(),
                relativeDirectory,
                displayName,
                mimeType);
            bool success = TryCreateFileStream(
                result, FileAccess.ReadWrite, out stream, out string documentUri, out error);
            if (!success && !string.IsNullOrEmpty(documentUri))
            {
                if (!DeleteDocumentUri(documentUri))
                {
                    error = $"{error} Temporary document cleanup also failed.";
                }
                documentUri = null;
            }
            documentId = success
                ? new StorageDocumentId(documentUri)
                : default;
            return success;
#else
            error = "SAF file descriptors are unavailable on this platform.";
            return false;
#endif
        }

        public static StorageMutationResult RenameDocument(
            StorageDocumentId documentId, string newDisplayName)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "renameDocumentUri", GetActivity(), documentId.Value, newDisplayName);
            return ReadMutationResult(result, documentId);
#else
            return new StorageMutationResult(
                StorageResultCode.NotReady, documentId,
                "SAF mutations are unavailable on this platform.");
#endif
        }

        public static StorageMutationResult DeleteDocument(
            StorageDocumentId documentId, StorageDocumentId parentDocumentId = default)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "deleteDocumentByUri",
                GetActivity(),
                documentId.Value,
                parentDocumentId.Value ?? "");
            return ReadMutationResult(result, documentId);
#else
            return new StorageMutationResult(
                StorageResultCode.NotReady, documentId,
                "SAF mutations are unavailable on this platform.");
#endif
        }

        public static bool DeleteDocumentUri(string documentUri)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("deleteDocumentUri", GetActivity(), documentUri);
#else
            return false;
#endif
        }

        public static bool RunFileDescriptorProbe(out string report)
        {
            report = null;
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            FileStream stream = null;
            string documentUri = null;
            try
            {
                if (!TryCreateTemporaryFileStream(
                        "",
                        "openbrush-fd-probe.tilt",
                        TiltFile.TILT_MIME_TYPE,
                        out stream,
                        out documentUri,
                        out string error))
                {
                    report = error;
                    return false;
                }

                byte[] sketch = { 0x4f, 0x42, 0x46, 0x44, 0x01, 0x23, 0x45, 0x67 };
                byte[] metadata = System.Text.Encoding.UTF8.GetBytes("{}");
                byte[] thumbnail = { 0x89, 0x50, 0x4e, 0x47 };
                using (var writer = new TiltFile.ArchiveWriter(
                    stream, ownsOutputStream: false))
                {
                    WriteProbeEntry(writer, TiltFile.FN_SKETCH, sketch);
                    WriteProbeEntry(writer, TiltFile.FN_METADATA, metadata);
                    WriteProbeEntry(writer, TiltFile.FN_THUMBNAIL, thumbnail);
                    writer.Complete();
                }
                stream.Flush();
                long endPosition = stream.Seek(0, SeekOrigin.End);
                if (!stream.CanSeek || endPosition <= TiltFile.HEADER_SIZE)
                {
                    report = $"Descriptor is not seekable or has unexpected length {endPosition}.";
                    return false;
                }
                if (!TiltFile.IsArchiveValid(
                        stream, "SAF descriptor probe", testData: true))
                {
                    report = "Tilt archive validation failed on the original descriptor.";
                    return false;
                }

                stream.Dispose();
                stream = null;
                var probeDocumentId = new StorageDocumentId(documentUri);
                if (!ProbeArchiveEntry(
                        probeDocumentId, TiltFile.FN_SKETCH, sketch, out error) ||
                    !ProbeArchiveEntry(
                        probeDocumentId, TiltFile.FN_METADATA, metadata, out error) ||
                    !ProbeArchiveEntry(
                        probeDocumentId, TiltFile.FN_THUMBNAIL, thumbnail, out error))
                {
                    report = error;
                    return false;
                }
                report =
                    $"Seekable detached descriptor Tilt archive passed ({endPosition} bytes).";
                return true;
            }
            catch (Exception e)
            {
                report = $"{e.GetType().Name}: {e.Message}";
                return false;
            }
            finally
            {
                stream?.Dispose();
                if (!string.IsNullOrEmpty(documentUri) && !DeleteDocumentUri(documentUri))
                {
                    Debug.LogWarning("SAF_FD Failed to delete descriptor probe document.");
                }
            }
#else
            report = "SAF file descriptors are unavailable on this platform.";
            return false;
#endif
        }

#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
        private static void WriteProbeEntry(
            TiltFile.ArchiveWriter writer, string entryName, byte[] bytes)
        {
            using (Stream entry = writer.GetWriteStream(entryName))
            {
                entry.Write(bytes, 0, bytes.Length);
            }
        }

        private static bool ProbeArchiveEntry(
            StorageDocumentId documentId,
            string entryName,
            byte[] expected,
            out string error)
        {
            error = null;
            if (!TryOpenSeekableReadStream(
                    documentId, out FileStream archiveStream, out error))
            {
                return false;
            }
            try
            {
                using (var entry = new ZipSubfileReader_SharpZipLib(
                    archiveStream, entryName))
                using (var copy = new MemoryStream())
                {
                    archiveStream = null;
                    entry.CopyTo(copy);
                    byte[] actual = copy.ToArray();
                    if (actual.Length != expected.Length)
                    {
                        error =
                            $"Descriptor probe entry {entryName} had length {actual.Length}.";
                        return false;
                    }
                    for (int i = 0; i < expected.Length; ++i)
                    {
                        if (actual[i] != expected[i])
                        {
                            error =
                                $"Descriptor probe entry {entryName} differed at byte {i}.";
                            return false;
                        }
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                error = $"Failed to read descriptor probe entry {entryName}: {e.Message}";
                return false;
            }
            finally
            {
                archiveStream?.Dispose();
            }
        }

        private static StorageMutationResult ReadMutationResult(
            AndroidJavaObject result, StorageDocumentId fallbackDocumentId)
        {
            if (result == null)
            {
                return new StorageMutationResult(
                    StorageResultCode.ProviderUnavailable,
                    fallbackDocumentId,
                    "Provider returned no mutation result.");
            }
            var code = (StorageResultCode)result.Get<int>("code");
            string documentUri = result.Get<string>("documentUri");
            string error = result.Get<string>("error");
            return new StorageMutationResult(
                code,
                string.IsNullOrEmpty(documentUri)
                    ? fallbackDocumentId
                    : new StorageDocumentId(documentUri),
                error);
        }

        private static bool HaveLength(int expected, params Array[] arrays)
        {
            foreach (Array array in arrays)
            {
                if (array == null || array.Length != expected)
                {
                    return false;
                }
            }
            return true;
        }

        private static DateTime UnixMillisecondsToLocalDateTime(long milliseconds)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMilliseconds(milliseconds)
                .ToLocalTime();
        }

        private static bool TryCreateFileStream(
            AndroidJavaObject result,
            FileAccess access,
            out FileStream stream,
            out string documentUri,
            out string error)
        {
            stream = null;
            documentUri = null;
            error = null;
            if (result == null)
            {
                error = "Provider returned no descriptor result.";
                return false;
            }

            int fd = result.Get<int>("fd");
            documentUri = result.Get<string>("documentUri");
            error = result.Get<string>("error");
            if (fd < 0)
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Provider returned an invalid file descriptor.";
                }
                return false;
            }

            var handle = new SafeFileHandle(new IntPtr(fd), ownsHandle: true);
            try
            {
                stream = new FileStream(handle, access);
            }
            catch (Exception e)
            {
                handle.Dispose();
                error = $"Failed to wrap the detached file descriptor: {e.Message}";
                return false;
            }

            if (!stream.CanSeek)
            {
                stream.Dispose();
                stream = null;
                error = "Provider returned a non-seekable file descriptor.";
                return false;
            }
            return true;
        }

        private static AndroidJavaObject GetActivity()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
#endif
    }
}
