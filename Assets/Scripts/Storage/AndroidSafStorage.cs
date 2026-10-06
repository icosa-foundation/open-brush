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
        [ThreadStatic] private static bool sm_ThreadAttachedToJvm;

        public static bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
                return Application.platform == RuntimePlatform.Android;
#else
                return false;
#endif
            }
        }

        public static bool RequestOpenBrushFolder()
        {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            AttachToJvmIfNeeded();
            AndroidJavaClass bridge = Bridge;
            // The only call that still needs an activity from this side: starting the picker
            // requires a real Activity, not the application context. Guarded because a null one
            // would otherwise fail inside Unity's argument marshalling as a bare
            // NullReferenceException with nothing naming the cause.
            using AndroidJavaObject activity = GetActivity();
            if (activity == null)
            {
                return false;
            }
            bridge.CallStatic("requestOpenBrushFolder", activity);
            return true;
#else
            return false;
#endif
        }

        public static bool HasOpenBrushFolder()
        {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
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
                AttachToJvmIfNeeded();
                AndroidJavaClass bridge = Bridge;
                sm_CachedReadiness =
                    bridge.CallStatic<bool>("hasOpenBrushFolder");
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


        public static string GetSelectedRootIdentity()
        {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            AttachToJvmIfNeeded();
            AndroidJavaClass bridge = Bridge;
            return bridge.CallStatic<string>("getSelectedRootIdentity");
#else
            return "";
#endif
        }


        public static bool EnsureDirectory(string relativePath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            AttachToJvmIfNeeded();
            AndroidJavaClass bridge = Bridge;
            return bridge.CallStatic<bool>("ensureDirectory", relativePath);
#else
            return true;
#endif
        }

        public static StorageDirectoryResult QueryDirectory(string relativePath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            try
            {
                AttachToJvmIfNeeded();
                AndroidJavaClass bridge = Bridge;
                using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                    "queryDirectory", relativePath);
                if (result == null)
                {
                    // Not the provider's doing. Every exit from the Java method builds a result,
                    // including its own catch blocks, so it cannot hand one of these back as
                    // null. A null means no Java frame ran to completion at all - in practice a
                    // worker thread that got here without being attached to the JVM.
                    return StorageDirectoryResult.Failed(
                        StorageResultCode.ProviderUnavailable,
                        "The directory query never reached the provider.");
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

        /// Reads can be issued from worker threads - image decoding, glTF buffer loads, Lua module
        /// resolution - and every one of them reaches the provider through JNI, which is only legal
        /// on a thread attached to the JVM. Unity attaches only its own, so attach here rather than
        /// relying on each caller to remember.
        internal static void AttachToJvmIfNeeded()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Attaching an already-attached thread is legal but not free, and reads arrive one
            // buffer at a time, so remember the answer per thread rather than per read.
            if (sm_ThreadAttachedToJvm)
            {
                return;
            }
            AndroidJNI.AttachCurrentThread();
            sm_ThreadAttachedToJvm = true;
#endif
        }

        public static bool TryOpenSeekableReadStream(
            string relativePath, out Stream stream, out string error)
        {
            stream = null;
            error = null;
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            AttachToJvmIfNeeded();
            AndroidJavaClass bridge = Bridge;
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "openChannelForPath", relativePath, "r");
            return TryCreateChannelStream(
                result, canWrite: false, out stream, out _, out error);
#else
            error = "SAF documents are unavailable on this platform.";
            return false;
#endif
        }

        public static bool TryOpenSeekableReadStream(
            StorageDocumentId documentId, out Stream stream, out string error)
        {
            stream = null;
            error = null;
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            AttachToJvmIfNeeded();
            AndroidJavaClass bridge = Bridge;
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "openChannelForDocument", documentId.Value, "r");
            return TryCreateChannelStream(
                result, canWrite: false, out stream, out _, out error);
#else
            error = "SAF documents are unavailable on this platform.";
            return false;
#endif
        }

        public static bool TryCreateNamedFileStream(
            string relativeDirectory,
            string displayName,
            string mimeType,
            out Stream stream,
            out StorageDocumentId documentId,
            out StorageDocumentId parentDocumentId,
            out string error)
        {
            stream = null;
            documentId = default;
            parentDocumentId = default;
            error = null;
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            AttachToJvmIfNeeded();
            AndroidJavaClass bridge = Bridge;
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "createNamedChannel",
                relativeDirectory,
                displayName,
                mimeType);
            bool success = TryCreateChannelStream(
                result, canWrite: true, out stream, out string documentUri, out error);
            string parentDocumentUri = result == null
                ? null
                : result.Get<string>("parentDocumentUri");
            if (!success && !string.IsNullOrEmpty(documentUri))
            {
                StorageMutationResult cleanup = DeleteDocument(
                    new StorageDocumentId(documentUri),
                    new StorageDocumentId(parentDocumentUri));
                if (!cleanup.Success)
                {
                    error = $"{error} Temporary document cleanup also failed.";
                }
                documentUri = null;
            }
            documentId = success
                ? new StorageDocumentId(documentUri)
                : default;
            parentDocumentId = success
                ? new StorageDocumentId(parentDocumentUri)
                : default;
            return success;
#else
            error = "SAF documents are unavailable on this platform.";
            return false;
#endif
        }

        /// Bytes free on the volume holding the Open Brush folder, or -1 when the provider
        /// cannot report it - a cloud-backed root, for instance. Callers treat -1 as "unknown"
        /// and allow the write, matching what FileUtils does when a platform cannot answer.
        public static long GetSharedFreeSpaceBytes()
        {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            try
            {
                AttachToJvmIfNeeded();
                AndroidJavaClass bridge = Bridge;
                return bridge.CallStatic<long>("getAvailableBytes");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SAF_STORAGE Free space query failed: {e.Message}");
                return -1;
            }
#else
            return -1;
#endif
        }

        public static StorageMutationResult RenameDocument(
            StorageDocumentId documentId, string newDisplayName)
        {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            AttachToJvmIfNeeded();
            AndroidJavaClass bridge = Bridge;
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "renameDocumentUri", documentId.Value, newDisplayName);
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
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            AttachToJvmIfNeeded();
            AndroidJavaClass bridge = Bridge;
            using AndroidJavaObject result = bridge.CallStatic<AndroidJavaObject>(
                "deleteDocumentByUri",
                documentId.Value,
                parentDocumentId.Value ?? "");
            return ReadMutationResult(result, documentId);
#else
            return new StorageMutationResult(
                StorageResultCode.NotReady, documentId,
                "SAF mutations are unavailable on this platform.");
#endif
        }

#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
        private static StorageMutationResult ReadMutationResult(
            AndroidJavaObject result, StorageDocumentId fallbackDocumentId)
        {
            if (result == null)
            {
                // The bridge cannot return a null result, so this is a call that never arrived
                // rather than a provider failure. See the note in QueryDirectory.
                return new StorageMutationResult(
                    StorageResultCode.ProviderUnavailable,
                    fallbackDocumentId,
                    "The mutation never reached the provider.");
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

        private static bool TryCreateChannelStream(
            AndroidJavaObject result,
            bool canWrite,
            out Stream stream,
            out string documentUri,
            out string error)
        {
            stream = null;
            documentUri = null;
            error = null;
            if (result == null)
            {
                // The bridge cannot return a null result, so this is a call that never arrived
                // rather than a provider failure. See the note in QueryDirectory.
                error = "The channel request never reached the provider.";
                return false;
            }

            int handle = result.Get<int>("handle");
            documentUri = result.Get<string>("documentUri");
            error = result.Get<string>("error");
            if (handle < 0)
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Provider returned an invalid document channel.";
                }
                return false;
            }

            stream = new SafDocumentStream(handle, result.Get<long>("length"), canWrite);
            return true;
        }

        /// Used by exactly one caller, which needs a real Activity to start the folder picker.
        /// Everything else lets the Java side resolve its own context: passing one from here
        /// meant that before UnityPlayer.currentActivity was populated the argument was null,
        /// and Unity throws NullReferenceException building the JNI argument array rather than
        /// reporting anything useful. Java reads the field directly, with no timing window and
        /// no signature inference, and falls back to currentContext.
        private static readonly object sm_BridgeGate = new object();
        private static AndroidJavaClass sm_Bridge;

        /// One class reference for the life of the process, never disposed. Constructing it per
        /// call was not merely wasteful: a thread attached to the JVM from native code resolves
        /// classes through the system class loader, which cannot see application classes, so
        /// every catalog query issued from a worker thread failed. The channel already cached its
        /// reference, which is exactly why reads worked from worker threads while directory
        /// queries did not.
        private static AndroidJavaClass Bridge
        {
            get
            {
                if (sm_Bridge != null)
                {
                    return sm_Bridge;
                }
                lock (sm_BridgeGate)
                {
                    sm_Bridge ??= new AndroidJavaClass(kBridgeClass);
                    return sm_Bridge;
                }
            }
        }

        private static AndroidJavaObject GetActivity()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
#endif
    }
}
