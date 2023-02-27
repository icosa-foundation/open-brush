using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
namespace TiltBrush
{
    public class FilesystemSketch : IResource
    {
        private string m_Path;
        public FilesystemSketch(string path)
        {
            m_Path = path;
            Name = Path.GetFileNameWithoutExtension(path);
            Uri = new Uri("file://" + m_Path);
            Assert.IsTrue(File.Exists(m_Path));
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
            return;
        }

        public async Task<Texture2D> LoadPreviewAsync()
        {
            throw new NotImplementedException();
            return null;
        }
        public async Task<Stream> GetStreamAsync()
        {
            return new FileStream(m_Path, FileMode.Open);
        }
#pragma warning restore 1998
    }
}
