using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace TiltBrush
{
    public class SketchCollection
    {
        private IResourceCollection m_Collection;
        private IAsyncEnumerator<IResource> m_ResourceEnumerator;
        private int m_RequestedIndex = 0;
        private Task m_RequestsTask = null;
        private List<IResource> m_Resources;

        public IResourceCollection Collection => m_Collection;

        public int NumSketches => m_Resources?.Count ?? 0;

        public SketchCollection(IResourceCollection collection, int firstRequest = 20)
        {
            m_Collection = collection;
            m_Resources = new List<IResource>();
            Refresh(firstRequest);
            m_Collection.OnChanged += OnChanged;
            m_Collection.OnRefreshingChanged += OnRefreshingChanged;
        }

        public void RequestToIndex(int index)
        {
            m_RequestedIndex = index;
            if (m_RequestsTask == null || m_RequestsTask.IsCompleted)
            {
                m_RequestsTask = ProcessRequests();
            }
        }

        public async Task ProcessRequests()
        {
            while (await m_ResourceEnumerator.MoveNextAsync() && m_RequestedIndex > NumSketches)
            {
                m_Resources.Add(m_ResourceEnumerator.Current);
            }
        }

        public bool IsRefreshing => !m_RequestsTask?.IsCompleted ?? false;

        public void Refresh(int toIndex)
        {
            m_ResourceEnumerator = m_Collection.ContentsAsync().GetAsyncEnumerator();
            RequestToIndex(toIndex);
        }

        public async Task<Texture2D> GetIconAsync(int index)
        {
            var resource = m_Resources[index];
            var preview = m_Resources[index] as IHasPreviewImage;
            if (preview == null)
            {
                return null;
            }
            Texture2D icon = await preview.LoadImageAsync();

            if (icon != null)
            {
                return icon;
            }

            // If we do not get an icon, get one from the file?

            var tilt = new DotTiltFile(resource);
            await using (var thumbStream = await tilt.GetSubFileAsync(DotTiltFile.FN_THUMBNAIL))
            {
                if (thumbStream == null)
                {
                    Debug.LogError($"Could not open {DotTiltFile.FN_THUMBNAIL} stream for {resource.Uri}.");
                    return null;
                }
                try
                {
                    icon = new Texture2D(2, 2);
                    var memStream = new MemoryStream();
                    await thumbStream.CopyToAsync(memStream);
                    icon.LoadImage(memStream.ToArray());
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            return icon;
        }

        public IResource GetResource(int index)
        {
            return m_Resources[index];
        }

        public void DeleteSketch(int index)
        {
            // TODO: Check writability of collection etc
            throw new NotImplementedException();
        }

        public event Action OnChanged;
        public event Action OnRefreshingChanged;
    }
}
