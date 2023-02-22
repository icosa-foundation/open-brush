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
    public class AsyncWrapperSketchSet : SketchSet
    {
        private ISketchSetAsync m_Async;
        private Task m_Init;
        private Task m_LoadingIcons;
        private Task m_Refreshing;
        private List<RemoteSketch[]> m_SketchPages;
        private Dictionary<int, Texture2D> m_CachedIcons;
        private int m_LookAhead = 40;
        private bool m_FeedExhausted;
        private ConcurrentQueue<int> m_IconsToLoad;

        public AsyncWrapperSketchSet(ISketchSetAsync async)
        {
            m_SketchPages = new List<RemoteSketch[]>();
            m_IconsToLoad = new ConcurrentQueue<int>();
            m_Async = async;
        }
        public SketchSetType Type => SketchSetType.Async;

        public bool IsReadyForAccess => m_Init is { IsCompleted: true };

        public bool IsActivelyRefreshingSketches => m_Refreshing is { IsCompleted: false };

        public bool RequestedIconsAreLoaded => m_LoadingIcons is { IsCompleted: true };

        public int NumSketches => m_SketchPages.Sum(x => x.Length);

        public void Init()
        {
            m_Init = m_Async.InitAsync();
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
            return index < NumSketches;
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
                var sketch = GetSketchAtIndex(index);
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

        private async Task LoadSketchIcon(RemoteSketch remoteSketch)
        {
            var url = remoteSketch.SceneFileInfo.FullPath;
            var tilt = new TiltFile(url);
            using (var thumbStream = await tilt.GetReadStreamAsync(TiltFile.FN_THUMBNAIL))
            {
                Texture2D icon = new Texture2D(2, 2);
                var memStream = new MemoryStream();
                await thumbStream.CopyToAsync(memStream);
                icon.LoadImage(memStream.ToArray());
                remoteSketch.Icon = icon;
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
            while (!m_FeedExhausted && totalIndex > NumSketches)
            {
                var newPage = await m_Async.FetchSketchPageAsync(m_SketchPages.Count);
                if (newPage.Length == 0)
                {
                    m_FeedExhausted = true;
                    return;
                }
                m_SketchPages.Add(newPage);
            }
        }

        public bool GetSketchIcon(int index, out Texture2D icon, out string[] authors, out string description)
        {
            FetchSketchesToAtLeast(index);
            var sketch = GetSketchAtIndex(index);
            icon = sketch.Icon;
            authors = sketch.Authors;
            description = "DESCRIPTION NOT IMPLEMENTED";
            return true;
        }

        private RemoteSketch GetSketchAtIndex(int index)
        {
            for (int page = 0; page < m_SketchPages.Count; page++)
            {
                int pageLength = m_SketchPages[page].Length;
                if (index < pageLength)
                {
                    return m_SketchPages[page][index];
                }
                index -= pageLength;
            }
            return null;
        }

        public SceneFileInfo GetSketchSceneFileInfo(int index)
        {
            return GetSketchAtIndex(index).SceneFileInfo;
        }

        public string GetSketchName(int index)
        {
            return GetSketchAtIndex(index).SceneFileInfo.HumanName;
        }

        public void DeleteSketch(int index)
        {
            // do nothing
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
            // Not sure we have to do anything here
        }

        public void Update()
        {
            // Not sure we have to do anything here
        }

        public event Action OnChanged;

        public event Action OnSketchRefreshingChanged;

    }
}
