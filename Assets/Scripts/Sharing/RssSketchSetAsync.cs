using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.ServiceModel.Syndication;
using UnityEngine;

namespace TiltBrush
{
    public class RssSketchSetAsync : ISketchSetAsync
    {
        private Uri m_Uri;
        public RssSketchSetAsync(Uri uri)
        {
            m_Uri = uri;
        }

        public string Name => "RSS Feed";
        public async Task InitAsync()
        {
            // might as well do all the work when getting the page
            return;
        }
        public async Task<RemoteSketch[]> FetchSketchPageAsync(int page)
        {
            Debug.Log($"Fetching rss stream {m_Uri.AbsoluteUri}");
            var stream = await App.HttpClient.GetStreamAsync(m_Uri).ConfigureAwait(false);
            Debug.Log("Got stream");
            using var xmlReader = XmlReader.Create(stream);
            Debug.Log("Loading feed");
            var feed = SyndicationFeed.Load(xmlReader);
            var sketches = new List<RemoteSketch>();
            foreach (var item in feed.Items)
            {
                var sketch = new RemoteSketch();
                var tiltUri = item.Links[0].Uri;
                sketch.SceneFileInfo = new RemoteFileInfo(tiltUri);
                sketch.Authors = item.Authors.Select(x => x.Name).ToArray();
                sketches.Add(sketch);
            }
            return sketches.ToArray();
        }
    }
}
