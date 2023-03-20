using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
namespace TiltBrush
{
    public class LocalFileResource : IResource
    {
        protected string m_Path;
        public LocalFileResource(string path)
        {
            m_Path = path;
            Name = Path.GetFileNameWithoutExtension(path);
            Uri = new Uri("file://" + m_Path);
        }

        public string Name { get; protected set; }

        public Uri Uri { get; protected set; }

        public Uri PreviewUri { get; protected set; }

        public string Description { get; protected set; }

        public Author[] Authors { get; protected set; }

        public ResourceLicense License { get; protected set; }

#pragma warning disable 1998
        public async Task InitAsync()
        {
            return;
        }

        public async Task<Texture2D> LoadPreviewAsync()
        {
            //throw new NotImplementedException();
            return null;
        }
        public async Task<Stream> GetStreamAsync()
        {
            return new FileStream(m_Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
#pragma warning restore 1998
    }
}
