using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace TiltBrush
{
    public class RemoteSketchResource : IResource
    {

        public RemoteSketchResource(string name, Uri uri, Uri previewUri = null, string description = null, Author[] authors = null, ResourceLicense license = null)
        {
            Name = name;
            Uri = uri;
            PreviewUri = previewUri;
            Description = description;
            Authors = authors;
            License = license;
        }

        public string Name { get; }
        public Uri Uri { get; }
        public Uri PreviewUri { get; }
        public string Description { get; }
        public Author[] Authors { get; }
        public ResourceLicense License { get; }
#pragma warning disable 1998
        public async Task InitAsync()
        {
            //throw new NotImplementedException();
            return;
        }
#pragma warning restore 1998

        public async Task<Texture2D> LoadPreviewAsync()
        {
            if (PreviewUri == null)
            {
                return null;
            }
            var httpStream = await App.HttpClient.GetStreamAsync(PreviewUri);
            var memoryStream = new MemoryStream();
            await httpStream.CopyToAsync(memoryStream);
            httpStream.Close();
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(memoryStream.ToArray()))
            {
                return null;
            }
            return texture;
        }

        public async Task<Stream> GetStreamAsync()
        {
            return await App.HttpClient.GetStreamAsync(Uri);
        }
    }
}
