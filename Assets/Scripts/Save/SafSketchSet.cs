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
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    public sealed class SafSceneFileInfo : SceneFileInfo
    {
        private sealed class StorageReadStreamSource : IReopenableReadStream
        {
            private readonly IUserStorageBackend m_Backend;
            private readonly StorageDocumentId m_DocumentId;

            public StorageReadStreamSource(
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

        private readonly IUserStorageBackend m_Backend;
        private StorageDocument m_Document;
        private readonly TiltFile m_TiltFile;
        private string m_AssetId;
        private string m_SourceId;

        public FileInfoType InfoType => FileInfoType.Disk;
        public string HumanName =>
            Path.GetFileNameWithoutExtension(SaveLoadScript.RemoveMd5Suffix(
                m_Document.DisplayName));
        public bool Valid => m_Document.DocumentId.IsValid;
        public bool Available => Valid && m_Backend.IsReady;
        public string FullPath => null;
        public string StorageId => m_Document.DocumentId.Value;
        public bool Exists => Available;
        public bool ReadOnly => !m_Document.SupportsRename;
        public string AssetId => m_AssetId;
        public string SourceId => m_SourceId;
        public int? TriangleCount => null;
        public DateTime CreationTime => m_Document.LastModified ?? DateTime.MinValue;
        public StorageDocument Document => m_Document;

        public SafSceneFileInfo(IUserStorageBackend backend, StorageDocument document)
        {
            m_Backend = backend ?? throw new ArgumentNullException(nameof(backend));
            m_Document = document ?? throw new ArgumentNullException(nameof(document));
            m_TiltFile = new TiltFile(
                new StorageReadStreamSource(backend, document.DocumentId),
                document.RelativeDisplayPath);
        }

        public void Delete()
        {
            StorageMutationResult result = m_Backend.Delete(
                m_Document.DocumentId, CancellationToken.None);
            if (!result.Success)
            {
                Debug.LogWarning(
                    $"SAF_TRANSACTION Delete failed for {HumanName}: {result.Error}");
            }
        }

        public string Rename(string newName)
        {
            string displayName = newName.EndsWith(
                SaveLoadScript.TILT_SUFFIX, StringComparison.OrdinalIgnoreCase)
                ? newName
                : $"{newName}{SaveLoadScript.TILT_SUFFIX}";
            StorageMutationResult result = m_Backend.Rename(
                m_Document.DocumentId, displayName, CancellationToken.None);
            if (!result.Success)
            {
                Debug.LogWarning(
                    $"SAF_TRANSACTION Rename failed for {HumanName}: {result.Error}");
                return StorageId;
            }
            return result.DocumentId.Value;
        }

        public bool IsHeaderValid()
        {
            return m_TiltFile.IsHeaderValid();
        }

        public Stream GetReadStream(string subfileName)
        {
            return m_TiltFile.GetReadStream(subfileName);
        }

        public SketchMetadata ReadMetadata()
        {
            using (Stream stream = SaveLoadScript.GetMetadataReadStream(this))
            {
                if (stream == null)
                {
                    return null;
                }
                using (var jsonReader = new JsonTextReader(new StreamReader(stream)))
                {
                    SketchMetadata metadata =
                        SaveLoadScript.m_Instance.DeserializeMetadata(jsonReader);
                    if (metadata != null)
                    {
                        m_SourceId = metadata.SourceId;
                        m_AssetId = metadata.AssetId;
                    }
                    return metadata;
                }
            }
        }
    }

    /// Sketch catalog backed directly by one SAF directory snapshot.
    public sealed class SafSketchSet : SketchSet
    {
        private const int kIconLoadsPerFrame = 3;

        private sealed class LoadedIconMetadata
        {
            public byte[] Thumbnail;
            public string[] Authors;
        }

        private sealed class SafSketch : Sketch
        {
            private readonly SafSceneFileInfo m_FileInfo;
            private Texture2D m_Icon;
            private string[] m_Authors;
            private bool m_Loaded;
            private Future<LoadedIconMetadata> m_LoadFuture;

            public SceneFileInfo SceneFileInfo => m_FileInfo;
            public string[] Authors => m_Authors;
            public Texture2D Icon => m_Icon;
            public bool IconAndMetadataValid => m_Loaded;
            public DateTime CreationTime => m_FileInfo.CreationTime;

            public SafSketch(SafSceneFileInfo fileInfo)
            {
                m_FileInfo = fileInfo;
                if (fileInfo.HumanName.Contains(" by "))
                {
                    string[] sections = fileInfo.HumanName.Split(
                        new[] { " by " }, StringSplitOptions.None);
                    m_Authors = new[] { sections.LastOrDefault() };
                }
            }

            public bool RequestLoadIconAndMetadata()
            {
                if (m_Loaded)
                {
                    return true;
                }
                if (m_LoadFuture == null)
                {
                    m_LoadFuture = new Future<LoadedIconMetadata>(LoadIconAndMetadata);
                }

                if (!m_LoadFuture.TryGetResult(out LoadedIconMetadata loaded))
                {
                    return false;
                }
                m_LoadFuture.Close();
                m_LoadFuture = null;
                if (loaded.Thumbnail != null && loaded.Thumbnail.Length > 0)
                {
                    m_Icon = new Texture2D(128, 128, TextureFormat.RGB24, true);
                    m_Icon.LoadImage(loaded.Thumbnail);
                    m_Icon.Apply();
                }
                if (loaded.Authors != null)
                {
                    m_Authors = loaded.Authors;
                }
                m_Loaded = true;
                return true;
            }

            public void Unload()
            {
                m_LoadFuture?.Close();
                m_LoadFuture = null;
                UnityEngine.Object.Destroy(m_Icon);
                m_Icon = null;
                m_Loaded = false;
            }

            private LoadedIconMetadata LoadIconAndMetadata()
            {
                try
                {
                    SketchMetadata metadata = m_FileInfo.ReadMetadata();
                    return new LoadedIconMetadata
                    {
                        Thumbnail = FileSketchSet.ReadThumbnail(m_FileInfo),
                        Authors = metadata?.Authors,
                    };
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"SAF_STREAM Failed to read {m_FileInfo.HumanName}: {e.Message}");
                    return new LoadedIconMetadata();
                }
            }
        }

        private readonly SketchSetType m_Type;
        private readonly StorageArea m_Area;
        private readonly IUserStorageBackend m_Backend;
        private readonly List<SafSketch> m_Sketches = new List<SafSketch>();
        private readonly Stack<int> m_RequestedLoads = new Stack<int>();
        private Future<StorageDirectoryResult> m_RefreshFuture;
        private string m_RefreshRootIdentity;
        private string m_AppliedRootIdentity;
        private bool m_Ready;
        private bool m_RefreshRequested;

        public SketchSetType Type => m_Type;
        public bool IsReadyForAccess => m_Ready;
        public bool IsActivelyRefreshingSketches => m_RefreshFuture != null;
        public bool RequestedIconsAreLoaded => m_RequestedLoads.Count == 0;
        public int NumSketches => m_Sketches.Count;

        public SafSketchSet(
            SketchSetType type, StorageArea area, IUserStorageBackend backend)
        {
            m_Type = type;
            m_Area = area;
            m_Backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public void Init()
        {
            RequestRefresh();
        }

        public bool IsSketchIndexValid(int index)
        {
            return index >= 0 && index < m_Sketches.Count;
        }

        public void RequestOnlyLoadedMetadata(List<int> requests)
        {
            foreach (SafSketch sketch in m_Sketches)
            {
                sketch.Unload();
            }
            m_RequestedLoads.Clear();
            requests.Reverse();
            foreach (int index in requests)
            {
                if (IsSketchIndexValid(index))
                {
                    m_RequestedLoads.Push(index);
                }
            }
        }

        public bool GetSketchIcon(
            int index, out Texture2D icon, out string[] authors, out string description)
        {
            description = null;
            if (!IsSketchIndexValid(index) || !m_Sketches[index].IconAndMetadataValid)
            {
                icon = null;
                authors = null;
                return false;
            }
            icon = m_Sketches[index].Icon;
            authors = m_Sketches[index].Authors;
            return true;
        }

        public SceneFileInfo GetSketchSceneFileInfo(int index)
        {
            return IsSketchIndexValid(index) ? m_Sketches[index].SceneFileInfo : null;
        }

        public string GetSketchName(int index)
        {
            return IsSketchIndexValid(index)
                ? m_Sketches[index].SceneFileInfo.HumanName
                : null;
        }

        public void DeleteSketch(int index)
        {
            if (!IsSketchIndexValid(index))
            {
                return;
            }
            SafSceneFileInfo fileInfo =
                (SafSceneFileInfo)m_Sketches[index].SceneFileInfo;
            if (!fileInfo.Document.SupportsDelete &&
                !fileInfo.Document.SupportsRemove)
            {
                OutputWindowScript.Error(
                    "Failed to delete sketch",
                    "The selected storage provider does not allow this document to be deleted.");
                return;
            }
            StorageMutationResult result = m_Backend.Delete(
                fileInfo.Document.DocumentId, CancellationToken.None);
            if (result.Success)
            {
                RequestRefresh();
            }
            else
            {
                OutputWindowScript.Error("Failed to delete sketch", result.Error);
            }
        }

        public void RenameSketch(int index, string newName)
        {
            if (!IsSketchIndexValid(index))
            {
                return;
            }
            SafSceneFileInfo fileInfo =
                (SafSceneFileInfo)m_Sketches[index].SceneFileInfo;
            if (!fileInfo.Document.SupportsRename)
            {
                OutputWindowScript.Error(
                    "Failed to rename sketch",
                    "The selected storage provider does not allow this document to be renamed.");
                return;
            }
            string displayName = newName.EndsWith(
                SaveLoadScript.TILT_SUFFIX, StringComparison.OrdinalIgnoreCase)
                ? newName
                : $"{newName}{SaveLoadScript.TILT_SUFFIX}";
            StorageMutationResult result = m_Backend.Rename(
                fileInfo.Document.DocumentId, displayName, CancellationToken.None);
            if (result.Success)
            {
                RequestRefresh();
            }
            else
            {
                OutputWindowScript.Error("Failed to rename sketch", result.Error);
            }
        }

        public void PrecacheSketchModels(int index)
        {
            if (IsSketchIndexValid(index))
            {
                App.IcosaAssetCatalog.PrecacheModels(
                    m_Sketches[index].SceneFileInfo, $"SafSketchSet {index}");
            }
        }

        public void NotifySketchCreated(string unused)
        {
            RequestRefresh();
        }

        public void NotifySketchChanged(string unused)
        {
            RequestRefresh();
        }

        public void NotifySketchDeleted(string unused)
        {
            RequestRefresh();
        }

        public void RequestRefresh()
        {
            m_RefreshRequested = true;
        }

        public void Update()
        {
            UpdateRefresh();

            int work = kIconLoadsPerFrame;
            while (work-- > 0 && m_RequestedLoads.Count > 0)
            {
                int index = m_RequestedLoads.Pop();
                if (IsSketchIndexValid(index) &&
                    !m_Sketches[index].RequestLoadIconAndMetadata())
                {
                    m_RequestedLoads.Push(index);
                }
            }
        }

        private void UpdateRefresh()
        {
            if (m_RefreshFuture == null && m_RefreshRequested)
            {
                m_RefreshRequested = false;
                m_RefreshRootIdentity = m_Backend.RootIdentity;
                if (m_AppliedRootIdentity != null &&
                    !string.Equals(
                        m_AppliedRootIdentity,
                        m_RefreshRootIdentity,
                        StringComparison.Ordinal))
                {
                    ClearCatalog();
                    m_AppliedRootIdentity = m_RefreshRootIdentity;
                    OnChanged();
                }
                m_RefreshFuture = new Future<StorageDirectoryResult>(
                    () => m_Backend.List(m_Area, "", CancellationToken.None),
                    longRunning: true);
                OnSketchRefreshingChanged();
                return;
            }
            if (m_RefreshFuture == null)
            {
                return;
            }

            StorageDirectoryResult result;
            try
            {
                if (!m_RefreshFuture.TryGetResult(out result))
                {
                    return;
                }
            }
            catch (FutureFailed e)
            {
                Debug.LogWarning(
                    $"SAF_STORAGE Catalog refresh failed for {m_Area}; retaining the " +
                    $"previous catalog: {e.InnerException?.Message ?? e.Message}");
                m_RefreshFuture.Close();
                m_RefreshFuture = null;
                m_RefreshRootIdentity = null;
                m_Ready = true;
                OnSketchRefreshingChanged();
                return;
            }

            m_RefreshFuture.Close();
            m_RefreshFuture = null;
            OnSketchRefreshingChanged();
            m_Ready = true;
            if (!string.Equals(
                    m_RefreshRootIdentity,
                    m_Backend.RootIdentity,
                    StringComparison.Ordinal))
            {
                m_RefreshRootIdentity = null;
                m_RefreshRequested = true;
                return;
            }
            m_RefreshRootIdentity = null;
            if (!result.Success)
            {
                Debug.LogWarning(
                    $"SAF_STORAGE Catalog refresh failed for {m_Area}: {result.Error}");
                return;
            }

            ClearCatalog();
            foreach (StorageDocument document in result.Documents)
            {
                if (!document.IsDirectory &&
                    document.DisplayName.EndsWith(
                        SaveLoadScript.TILT_SUFFIX, StringComparison.OrdinalIgnoreCase))
                {
                    m_Sketches.Add(new SafSketch(
                        new SafSceneFileInfo(m_Backend, document)));
                }
            }
            m_Sketches.Sort((left, right) =>
                right.CreationTime.CompareTo(left.CreationTime));
            m_AppliedRootIdentity = m_Backend.RootIdentity;
            OnChanged();
        }

        private void ClearCatalog()
        {
            foreach (SafSketch sketch in m_Sketches)
            {
                sketch.Unload();
            }
            m_Sketches.Clear();
            m_RequestedLoads.Clear();
        }

        public event Action OnChanged = delegate { };
        public event Action OnSketchRefreshingChanged = delegate { };
    }
}
