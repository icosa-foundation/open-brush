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

        public FilesystemSketchCollection(string path, string name)
        {
            m_Path = path;
            Name = name;
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
            // TODO: Perhaps to something clever with having a thumbnail in a .meta subdir?
            throw new NotImplementedException();
            return null;
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
                yield return new FilesystemSketchCollection(dirInfo.FullName, dirInfo.Name);
            }

            foreach (var fileInfo in m_Dir.EnumerateFiles("*.tilt"))
            {
                yield return new FilesystemSketch(fileInfo.FullName);
            }
        }

        public async Task<Stream> GetStreamAsync()
        {
            throw new NotImplementedException();
        }
#pragma warning restore 1998

    }
}
