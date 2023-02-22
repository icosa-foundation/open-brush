using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace TiltBrush
{
    public class RemoteSketchResource : IResource
    {
        public RemoteSketchResource(string name, Uri uri, Texture2D previewImage = null, string description = null, Author[] authors = null, ResourceLicense license = null)
        {
            Name = name;
            Uri = uri;
            PreviewImage = previewImage;
            Description = description;
            Authors = authors;
            License = license;
        }

        public string Name { get; }
        public Uri Uri { get; }
        public Texture2D PreviewImage { get; }
        public string Description { get; }
        public Author[] Authors { get; }
        public ResourceLicense License { get; }
#pragma warning disable 1998
        public async Task InitAsync()
        {
            throw new NotImplementedException();
        }
        public async Task<bool> LoadPreviewAsync()
        {
            throw new NotImplementedException();
        }
        public async Task<Stream> GetStreamAsync()
        {
            throw new NotImplementedException();
        }
#pragma warning restore 1998
    }
}
