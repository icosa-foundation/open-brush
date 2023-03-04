using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace TiltBrush
{
    public class FilesystemSketchCollection : IResourceCollection
    {
        private string m_Path;
        private DirectoryInfo m_Dir;
        private Texture2D m_Icon;

        public FilesystemSketchCollection(string path, string name, Texture2D icon = null)
        {
            m_Path = path;
            Name = name;
            m_Icon = icon;
        }

        public string CollectionType => "LocalFolderCollection";
        public string CollectionInstance => m_Path;

        public string Name { get; private set; }

        public Uri Uri { get; private set; }

        public Uri PreviewUri { get; }

        public string Description { get; }

        public Author[] Authors { get; set; }

        public ResourceLicense License { get; }

#pragma warning disable 1998
        public async Task<Texture2D> LoadPreviewAsync()
        {
            return m_Icon;
        }

        public async Task InitAsync()
        {
            m_Dir = new DirectoryInfo(m_Path);
            if (Name == null)
            {
                Name = m_Dir.Name;
            }
            Uri = new Uri($"file://{m_Dir.FullName}");
        }

        public async IAsyncEnumerable<IResource> ContentsAsync()
        {
            foreach (var dirInfo in m_Dir.EnumerateDirectories())
            {
                if (dirInfo.Name.StartsWith("."))
                {
                    continue;
                }
                yield return new FilesystemSketchCollection(dirInfo.FullName, dirInfo.Name, m_Icon);
            }

            foreach (var fileInfo in m_Dir.EnumerateFiles("*.tilt"))
            {
                yield return new FilesystemSketch(fileInfo.FullName);
            }
        }

        public async Task<Stream> GetStreamAsync()
        {
            return null;
            throw new NotImplementedException();
        }
#pragma warning restore 1998

    }
}
