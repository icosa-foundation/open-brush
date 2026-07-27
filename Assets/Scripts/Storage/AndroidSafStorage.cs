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
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("hasOpenBrushFolder", GetActivity());
#else
            return true;
#endif
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

        public static void ClearOpenBrushFolder()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            bridge.CallStatic("clearOpenBrushFolder", GetActivity());
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
                        "openbrush-fd-probe.bin",
                        "application/octet-stream",
                        out stream,
                        out documentUri,
                        out string error))
                {
                    report = error;
                    return false;
                }

                byte[] expected = { 0x4f, 0x42, 0x46, 0x44, 0x01, 0x23, 0x45, 0x67 };
                stream.Write(expected, 0, expected.Length);
                stream.Flush();
                long endPosition = stream.Seek(0, SeekOrigin.End);
                if (!stream.CanSeek || endPosition != expected.Length)
                {
                    report = $"Descriptor is not seekable or has unexpected length {endPosition}.";
                    return false;
                }

                stream.Seek(0, SeekOrigin.Begin);
                byte[] actual = new byte[expected.Length];
                int totalRead = 0;
                while (totalRead < actual.Length)
                {
                    int read = stream.Read(actual, totalRead, actual.Length - totalRead);
                    if (read == 0)
                    {
                        break;
                    }
                    totalRead += read;
                }

                if (totalRead != expected.Length)
                {
                    report = $"Descriptor returned {totalRead} of {expected.Length} probe bytes.";
                    return false;
                }
                for (int i = 0; i < expected.Length; ++i)
                {
                    if (actual[i] != expected[i])
                    {
                        report = $"Descriptor probe data mismatch at byte {i}.";
                        return false;
                    }
                }

                report = $"Seekable detached descriptor read/write passed ({expected.Length} bytes).";
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

        public static bool WriteFileFromPath(string relativePath, string sourcePath, string mimeType)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>(
                "writeFileFromPath", GetActivity(), relativePath, sourcePath, mimeType);
#else
            return true;
#endif
        }

        public static bool CopyDirectoryFromPath(string relativeDestinationPath, string sourceDirectoryPath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>(
                "copyDirectoryFromPath", GetActivity(), relativeDestinationPath, sourceDirectoryPath);
#else
            return true;
#endif
        }

        public static int StartWriteFileFromPath(string relativePath, string sourcePath, string mimeType)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<int>(
                "startWriteFileFromPath", GetActivity(), relativePath, sourcePath, mimeType);
#else
            return 0;
#endif
        }

        public static int StartCopyDirectoryFromPath(
            string relativeDestinationPath, string sourceDirectoryPath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<int>(
                "startCopyDirectoryFromPath",
                GetActivity(),
                relativeDestinationPath,
                sourceDirectoryPath);
#else
            return 0;
#endif
        }

        public static int StartCopyDirectoryToPath(
            string relativePath, string destinationDirectoryPath, string[] preservedPaths)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<int>(
                "startCopyDirectoryToPath",
                GetActivity(),
                relativePath,
                destinationDirectoryPath,
                preservedPaths);
#else
            return 0;
#endif
        }
        public static bool IsTransferJobDone(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("isTransferJobDone", jobId);
#else
            return true;
#endif
        }

        public static bool DidTransferJobSucceed(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("didTransferJobSucceed", jobId);
#else
            return true;
#endif
        }

        public static long GetTransferJobBytesDone(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<long>("getTransferJobBytesDone", jobId);
#else
            return 0;
#endif
        }

        public static long GetTransferJobBytesTotal(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<long>("getTransferJobBytesTotal", jobId);
#else
            return 0;
#endif
        }

        public static string GetTransferJobError(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<string>("getTransferJobError", jobId);
#else
            return null;
#endif
        }

        public static void ClearTransferJob(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            bridge.CallStatic("clearTransferJob", jobId);
#endif
        }

        public static bool DeleteTreeChild(string relativePath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("deleteTreeChild", GetActivity(), relativePath);
#else
            return true;
#endif
        }

        public static string[] ListFiles(string relativePath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<string[]>("listFiles", GetActivity(), relativePath);
#else
            return new string[0];
#endif
        }

        public static bool CopyFileToPath(string relativePath, string destinationPath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>(
                "copyFileToPath", GetActivity(), relativePath, destinationPath);
#else
            return true;
#endif
        }

        public static bool CopyDirectoryToPath(string relativePath, string destinationDirectoryPath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>(
                "copyDirectoryToPath", GetActivity(), relativePath, destinationDirectoryPath);
#else
            return true;
#endif
        }

#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
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
            catch
            {
                handle.Dispose();
                throw;
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
