using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.ServiceModel.Syndication;
using UnityEngine;

namespace TiltBrush
{
    public class RssSketchCollection : IResourceCollection
    {
        private Uri m_Uri;
        private HttpClient m_HttpClient;

        public RssSketchCollection(HttpClient httpClient, Uri uri)
        {
            m_Uri = uri;
            m_HttpClient = httpClient;
        }

        public string CollectionType => "Rss";
        public string CollectionInstance => m_Uri.OriginalString;

        public string Name => "RSS Feed";
        public Uri Uri { get; }
        public Uri PreviewUri { get; }
        public string Description { get; }
        public Author[] Authors { get; }
        public ResourceLicense License { get; }
        public async Task InitAsync()
        {
            // might as well do all the work when getting the page
            return;
        }
        public async Task<Texture2D> LoadPreviewAsync()
        {
            throw new NotImplementedException();
        }
        public async Task<Stream> GetStreamAsync()
        {
            throw new NotImplementedException();
        }
        public async IAsyncEnumerable<IResource> ContentsAsync()
        {
            SyndicationFeed feed;
            try
            {
                Debug.Log($"Fetching rss stream {m_Uri.AbsoluteUri}");
                var stream = await m_HttpClient.GetStreamAsync(m_Uri);
                Debug.Log("Got stream");
                using var xmlReader = XmlReader.Create(stream);
                Debug.Log("Loading feed");
                feed = SyndicationFeed.Load(xmlReader);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                yield break;
            }
            foreach (var item in feed.Items)
            {
                var remoteSketch = new RemoteSketchResource(
                    name: item.Title.Text,
                    uri: item.Links[0].Uri,
                    previewUri: null,
                    description: item.Summary.Text,
                    authors: item.Authors.Select(x => new TiltBrush.Author { Name = x.Name, Url = x.Uri, Email = x.Email }).ToArray()
                );
                yield return remoteSketch;
            }
        }
    }
}
