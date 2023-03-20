using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;
namespace TiltBrush
{
    [Serializable]
    public class IcosaSketch
    {
        [Serializable]
        public class Format
        {
            public string id;
            public string url;
            public string format;
        }

        public string id;
        public string url;
        public Format[] formats;
        public string name;
        public string description;
        public string thumbnail;
        public string ownername;
        public string ownerurl;
    }

    public class IcosaCollectionFactory : MonoBehaviour, IResourceCollectionFactory
    {
        public string Scheme => "icosa";

        public IResourceCollection Create(Uri uri)
        {
            Assert.AreEqual(uri.Scheme, Scheme);
            return new FeedCollection(App.HttpClient, uri);
        }
    }

    public class IcosaCollection : IResourceCollection
    {
        private string m_User;
        private HttpClient m_httpClient;
        public static Uri AllAssetsUri => new Uri("https://api.icosa.gallery/assets");

        public static Uri CreateUserUri(string user)
        {
            return new Uri($"https://api.icosa.gallery/users/{user}/assets");
        }

        public IcosaCollection(HttpClient httpClient, Uri uri)
        {
            if (uri.Segments.Length > 1)
            {
                m_User = uri.Segments[uri.Segments.Length - 2];
                Name = $"Icosa : {m_User}";
            }
            else
            {
                Name = "Icosa";
                m_User = null;
            }

            m_httpClient = httpClient;
        }

        public string CollectionType => "Icosa";
        public string CollectionInstance => m_User ?? "";

        public string Name { get; private set; }
        public Uri Uri { get; }
        public Uri PreviewUri { get; }
        public string Description { get; }
        public Author[] Authors { get; }
        public ResourceLicense License { get; }
        public string Error { get; private set; }
#pragma warning disable 1998
        public async Task InitAsync()
        {
            return;
        }
        public async Task<Texture2D> LoadPreviewAsync()
        {
            throw new NotImplementedException();
            return null;
        }

        private async IAsyncEnumerable<IResource> ReadPage(Stream stream)
        {
            using var streamReader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);
            var settings = new JsonSerializerSettings
            {
                Error = delegate (object sender, ErrorEventArgs args)
                {
                    Debug.LogWarning(args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                }
            };
            var json = JsonSerializer.CreateDefault(settings);

            // TODO: can we deserialize these one at a time?

            var sketches = json.Deserialize<IcosaSketch[]>(jsonReader);
            foreach (var sketch in sketches)
            {
                var tiltFormat = sketch.formats.FirstOrDefault(x => x.format == "TILT");
                var authors = new Author[] { new Author { Name = sketch.name, Url = sketch.ownerurl } };
                if (tiltFormat != null)
                {
                    var remoteSketch = new RemoteSketchResource(
                        name: sketch.name,
                        uri: new Uri(tiltFormat.url),
                        previewUri: sketch.thumbnail == null ? null : new Uri(sketch.thumbnail),
                        description: sketch.description,
                        authors: authors
                    );
                    yield return remoteSketch;
                }
            }
        }

        public async Task<Stream> GetStreamAsync()
        {
            throw new NotImplementedException();
        }
#pragma warning restore 1998


        public int NumResources
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public async IAsyncEnumerable<IResource> ContentsAsync()
        {
            if (m_User == null)
            {
                m_httpClient.BaseAddress = new Uri("https://api.icosa.gallery/");
                m_httpClient.DefaultRequestHeaders.Accept.Clear();
                m_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                for (int page = 0; true; page++)
                {
                    var response = await m_httpClient.GetAsync($"assets?page={page}");
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        Error = $"IcosaSketchCatalog returned error code of {response.StatusCode} : {response.ReasonPhrase}";
                        Debug.LogWarning(Error);
                        yield break;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync();
                    await foreach (var resource in ReadPage(stream))
                    {
                        yield return resource;
                    }
                    stream.Close();
                }
            }
            else
            {
                for (int page = 0; true; page++)
                {
                    var url = $"https://api.icosa.gallery/assets&page={page}";
                    await foreach (var resource in ReadPage(await m_httpClient.GetStreamAsync(url)))
                    {
                        yield return resource;
                    }
                }
            }
        }
        public void Refresh()
        {
            throw new NotImplementedException();
        }
        public event Action OnChanged;
        public event Action OnRefreshingChanged;
    }
}
