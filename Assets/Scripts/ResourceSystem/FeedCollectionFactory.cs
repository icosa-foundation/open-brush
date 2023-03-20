using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.ServiceModel.Syndication;
using UnityEngine;
using UnityEngine.Assertions;

namespace TiltBrush
{
    public class FeedCollectionFactory : MonoBehaviour, IResourceCollectionFactory
    {
        public string Scheme => "feed";

        public IResourceCollection Create(Uri uri)
        {
            Assert.AreEqual(uri.Scheme, Scheme);
            return new FeedCollection(App.HttpClient, uri);
        }
    }

    public class FeedCollection : IResourceCollection
    {
        private Uri m_Uri;
        private HttpClient m_HttpClient;
        private List<RemoteSketchResource> m_Items;
        private string m_Title;

        public FeedCollection(HttpClient httpClient, Uri uri)
        {
            m_Uri = uri;
            m_HttpClient = httpClient;
        }

        public string CollectionType => "Rss";
        public string CollectionInstance => m_Uri.OriginalString;

        public string Name => m_Title;
        public Uri Uri { get; }
        public Uri PreviewUri { get; }
        public string Description { get; }
        public Author[] Authors { get; }
        public ResourceLicense License { get; }

        public int NumResources => m_Items?.Count ?? 0;
        public async Task InitAsync()
        {
            // might as well do all the work when getting the page
            SyndicationFeed feed;
            try
            {
                var stream = await m_HttpClient.GetStreamAsync(m_Uri.AbsolutePath);
                using var xmlReader = XmlReader.Create(stream);
                feed = SyndicationFeed.Load(xmlReader);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }
            m_Title = feed.Title.Text;
            m_Items = feed.Items.Select(item => new RemoteSketchResource(
                name: item.Title.Text,
                uri: item.Links[0].Uri,
                previewUri: null,
                description: item.Summary.Text,
                authors: item.Authors.Select(x => new TiltBrush.Author { Name = x.Name, Url = x.Uri, Email = x.Email }).ToArray()
            )).ToList();
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
            foreach (var item in m_Items)
            {
                yield return item;
            }
        }
        public void Refresh()
        {
            InitAsync();
        }
        public event Action OnChanged;
        public event Action OnRefreshingChanged;
    }
}
