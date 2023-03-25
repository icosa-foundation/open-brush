using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluxJpeg.Core;
using UnityEngine;
namespace TiltBrush
{
    public class ResourceCollectionSketchSet : ISketchSet
    {
        private IResourceCollection m_Collection;
        private Task m_Init;
        private Task m_LoadingIcons;
        private Task m_Refreshing;
        private List<ResourceSketch> m_Sketches;
        private Dictionary<int, Texture2D> m_CachedIcons;
        private int m_LookAhead = 40;
        private bool m_FeedExhausted;
        private ConcurrentQueue<int> m_IconsToLoad;
        private IAsyncEnumerator<IResource> m_ResourceEnumerator;

        public ResourceCollectionSketchSet(IResourceCollection collection)
        {
            m_Sketches = new List<ResourceSketch>();
            m_IconsToLoad = new ConcurrentQueue<int>();
            m_Collection = collection;
            m_ResourceEnumerator = m_Collection.ContentsAsync().GetAsyncEnumerator();
            m_Collection.OnChanged += RequestRefresh;
        }

        public string SketchSetType => m_Collection?.Uri?.Scheme ?? "";
        public string SketchSetInstance => m_Collection?.Uri?.OriginalString ?? "";

        public string Title => m_Collection.Name;

        public bool IsReadyForAccess => m_Init is { IsCompleted: true };

        public bool IsActivelyRefreshingSketches => m_Refreshing is { IsCompleted: false };

        public bool RequestedIconsAreLoaded => m_LoadingIcons is { IsCompleted: true };

        public int NumSketches => m_Sketches.Count;

        public void Init()
        {
            m_Init = m_Collection.InitAsync();
            InitAsync();
        }

        private async Task InitAsync()
        {
            await m_Init;
            m_Refreshing = FetchSketchesToAtLeastAsync(0);
            await m_Refreshing;
        }

        public bool IsSketchIndexValid(int index)
        {
            return index < NumSketches && index >= 0;
        }

        public void RequestOnlyLoadedMetadata(List<int> requests)
        {
            foreach (var item in requests)
            {
                m_IconsToLoad.Enqueue(item);
            }
            if (m_LoadingIcons == null || m_LoadingIcons.IsCompleted)
            {
                m_LoadingIcons = LoadIcons();
            }
        }

        private async Task LoadIcons()
        {
            var loadingTasks = new List<Task>();
            while (m_IconsToLoad.TryDequeue(out int index))
            {
                var sketch = m_Sketches[index];
                if (sketch.Icon == null)
                {
                    loadingTasks.Add(LoadSketchIcon(sketch));
                }
            }
            foreach (var task in loadingTasks)
            {
                await task;
            }
        }

        private async Task LoadSketchIcon(ResourceSketch resourceSketch)
        {
            var thumbnail = await resourceSketch.ResourceFileInfo.Resource.LoadPreviewAsync();
            if (thumbnail != null)
            {
                resourceSketch.Icon = thumbnail;
                return;
            }

            var tilt = new DotTiltFile(resourceSketch.ResourceFileInfo.Resource);
            await using (var thumbStream = await tilt.GetSubFileAsync(TiltFile.FN_THUMBNAIL))
            {
                if (thumbStream == null)
                {
                    Debug.LogError($"Could not open {TiltFile.FN_THUMBNAIL} stream for {resourceSketch.ResourceFileInfo.Resource.Uri}.");
                    return;
                }
                try
                {
                    thumbnail = new Texture2D(2, 2);
                    var memStream = new MemoryStream();
                    await thumbStream.CopyToAsync(memStream);
                    thumbnail.LoadImage(memStream.ToArray());
                    resourceSketch.Icon = thumbnail;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private void FetchSketchesToAtLeast(int index)
        {
            if (m_Refreshing?.IsCompleted == false)
            {
                return;
            }
            m_Refreshing = FetchSketchesToAtLeastAsync(index);
        }

        private async Task FetchSketchesToAtLeastAsync(int index)
        {
            int totalIndex = index + m_LookAhead;
            bool updated = false;
            while (await m_ResourceEnumerator.MoveNextAsync() && totalIndex > NumSketches)
            {
                if (!updated)
                {
                    OnSketchRefreshingChanged?.Invoke();
                }
                var resource = m_ResourceEnumerator.Current;
                var fileInfo = new ResourceFileInfo(resource);
                var sketch = new ResourceSketch(fileInfo);
                m_Sketches.Add(sketch);
                updated = true;
            }
            if (updated)
            {
                OnSketchRefreshingChanged?.Invoke();
                OnChanged?.Invoke();
            }
        }

        public bool GetSketchIcon(int index, out Texture2D icon, out string[] authors, out string description)
        {
            FetchSketchesToAtLeast(index);
            var sketch = m_Sketches[index];
            icon = sketch.Icon;
            authors = sketch.Authors;
            description = sketch.ResourceFileInfo.Resource.Description ?? "";
            return icon != null;
        }

        public SceneFileInfo GetSketchSceneFileInfo(int index)
        {
            return m_Sketches[index].SceneFileInfo;
        }

        public string GetSketchName(int index)
        {
            return m_Sketches[index].SceneFileInfo.HumanName;
        }

        public IResource GetResource(int index)
        {
            return m_Sketches[index].ResourceFileInfo.Resource;
        }

        public void DeleteSketch(int index)
        {
            if (m_Collection.Delete(m_Sketches[index].ResourceFileInfo.Resource))
            {
                RequestRefresh();
            }
        }

        public void PrecacheSketchModels(int index)
        {
            // TODO: Later, my friend - later.
            // I would like to make sketches contain all their models rather
            // than support all that guff.
        }

        public void NotifySketchCreated(string fullpath)
        {
            // Do nothing?
        }

        public void NotifySketchChanged(string fullpath)
        {
            // Do nothing?
        }

        public void RequestRefresh()
        {
            if (!IsActivelyRefreshingSketches)
            {
                m_Refreshing = FetchSketchesToAtLeastAsync(m_Sketches.Count - m_LookAhead);
            }
        }

        public void Update()
        {
            // Not sure we have to do anything here
        }

        public event Action OnChanged;

        public event Action OnSketchRefreshingChanged;

    }
}
