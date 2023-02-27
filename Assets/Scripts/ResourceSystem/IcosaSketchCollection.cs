using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor.PackageManager;
using UnityEngine;
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

    public class IcosaSketchCollection : IResourceCollection
    {
        private string m_User;
        private HttpClient m_httpClient;

        public IcosaSketchCollection(HttpClient httpClient, string user = null)
        {
            m_User = user;
            Name = "Icosa";
            if (m_User != null)
            {
                Name += $" for {m_User}";
                Uri = new Uri($"https://api.icosa.gallery/users/{m_User}/assets");
            }
            else
            {
                Uri = new Uri($"https://api.icosa.gallery/assets");
            }
            m_httpClient = httpClient;
        }

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
    }
}
