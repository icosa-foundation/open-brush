// Copyright 2026 The Open Brush Authors. Licensed under Apache-2.0.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

namespace TiltBrush
{
    // Only the explicit scoped build calls this gate. Preparation grants access;
    // it does not establish ownership or replace the need to handle failed opens.
    public sealed class AndroidDirectStorage : MonoBehaviour
    {
        private const string Bridge = "foundation.icosa.openbrush.DirectStorage";
        private static AndroidDirectStorage s_Instance;
        private string m_FolderName;
        private static int s_RefreshRequested;
        private static string[] s_Files = Array.Empty<string>();
        private static string[] s_Directories = Array.Empty<string>();
        public static bool IsActive { get; private set; }
        public static bool IsRefreshing { get; private set; }
        public static event Action Refreshed;
        public static bool StartupReady { get; private set; }
        public static bool StartupCanceled { get; private set; }
        public static string RootPath { get; private set; }
        public static int DocumentCount { get; private set; }

        [Serializable]
        private sealed class Scan
        {
            public string root;
            public int documents;
            public int failed;
            public string error;
            public string[] files;
            public string[] directories;
        }

        public static IEnumerator Initialize(string folderName)
        {
            if (Application.platform != RuntimePlatform.Android || StartupReady) yield break;
            if (s_Instance == null)
            {
                var owner = new GameObject("OBDS_Storage");
                DontDestroyOnLoad(owner);
                s_Instance = owner.AddComponent<AndroidDirectStorage>();
                s_Instance.m_FolderName = folderName;
                IsActive = true;
                IsRefreshing = true;
                try
                {
                    using (var bridge = new AndroidJavaClass(Bridge))
                    {
                        RootPath = bridge.CallStatic<string>("documentsRoot", folderName);
                        bridge.CallStatic("begin", folderName, owner.name);
                    }
                }
                catch (Exception error) { s_Instance.OnStorageError(error.Message); }
            }
            while (!StartupReady && !StartupCanceled) yield return null;
        }

        public static bool ContainsPath(string path)
        {
            return IsActive && !string.IsNullOrEmpty(path) &&
                Path.GetFullPath(path).StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        // MediaStore translation permits path reads, but Android's ordinary directory
        // listings can still omit externally supplied files. Only names are merged;
        // consumers keep their existing streams, identities and save implementation.
        public static string[] GetFiles(string directory, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            string[] files = Directory.GetFiles(directory, "*", searchOption);
            if (!ContainsPath(directory) && directory != RootPath) return files;
            string fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            return files.Concat(s_Files.Where(path =>
                (searchOption == SearchOption.AllDirectories
                    ? path.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    : Path.GetDirectoryName(path) == fullPath) && File.Exists(path)))
                .Distinct(StringComparer.Ordinal).ToArray();
        }

        public static string[] GetDirectories(string directory)
        {
            string[] directories = Directory.GetDirectories(directory);
            if (!ContainsPath(directory) && directory != RootPath) return directories;
            string fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            return directories.Concat(s_Directories.Where(path => Path.GetDirectoryName(path) == fullPath && Directory.Exists(path)))
                .Distinct(StringComparer.Ordinal).ToArray();
        }

        // Safe from file-watcher/background readers; JNI and dialogs stay on the main thread.
        public static void RequestRefresh()
        {
            if (IsActive) Interlocked.Exchange(ref s_RefreshRequested, 1);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && StartupReady) RequestRefresh();
        }

        private void Update()
        {
            if (IsRefreshing || Interlocked.Exchange(ref s_RefreshRequested, 0) == 0) return;
            IsRefreshing = true;
            try
            {
                using (var bridge = new AndroidJavaClass(Bridge))
                    bridge.CallStatic("begin", m_FolderName, gameObject.name);
            }
            catch (Exception error) { OnStorageError(error.Message); }
        }

        [Preserve]
        public void OnStoragePrepared(string json)
        {
            try
            {
                Scan scan = JsonUtility.FromJson<Scan>(json);
                if (scan == null || scan.files == null || scan.directories == null || scan.root != RootPath)
                    throw new IOException("Invalid storage preparation result.");
                if (scan.failed != 0)
                    throw new IOException($"Access could not be prepared for {scan.failed} documents: {scan.error}");
                string capability = Path.Combine(RootPath, $".obds-capability-{Guid.NewGuid():N}");
                bool created = false;
                try
                {
                    using (var stream = new FileStream(capability, FileMode.CreateNew, FileAccess.Write))
                    {
                        created = true;
                        stream.WriteByte(0x5a);
                        stream.Flush(true);
                    }
                    using (var stream = File.OpenRead(capability))
                        if (stream.ReadByte() != 0x5a) throw new IOException("Root capability read failed.");
                }
                finally { if (created) File.Delete(capability); }
                DocumentCount = scan.documents;
                s_Files = scan.files;
                s_Directories = scan.directories;
                Debug.Log($"OBDS_APP prepared documents={scan.documents} inaccessible={scan.failed}");
                StartupReady = true;
                IsRefreshing = false;
                Refreshed?.Invoke();
            }
            catch (Exception error) { OnStorageError(error.Message); }
        }

        [Preserve]
        public void OnStorageError(string error)
        {
            Debug.LogError($"OBDS_APP folder connection failed: {error}");
            using (var bridge = new AndroidJavaClass(Bridge))
                bridge.CallStatic("showRetry", m_FolderName, gameObject.name);
        }

        [Preserve]
        public void OnStorageCanceled(string unused)
        {
            Debug.Log("OBDS_APP folder connection deferred; exiting before shared configuration loads");
            StartupCanceled = true;
            Application.Quit();
        }
    }
}
