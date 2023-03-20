using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
namespace TiltBrush
{
    public class LocalFolderCollectionFactory : MonoBehaviour, IResourceCollectionFactory
    {
        [SerializeField] private Texture2D m_FolderLogo;

        public string Scheme => "file";

        public IResourceCollection Create(Uri uri)
        {
            // TODO: Should I have special handling in here for special paths?
            Assert.AreEqual(uri.Scheme, Scheme);
            string path = uri.LocalPath;
            string name = uri.Segments.Last();
            return new LocalFolderCollection(path, name, m_FolderLogo);
        }

    }

    public class LocalFolderCollection : IResourceCollection
    {
        private string m_Path;
        private DirectoryInfo m_Dir;
        private Texture2D m_Icon;
        private List<IResource> m_Resources;

        public LocalFolderCollection(string path, string name, Texture2D icon = null)
        {
            m_Path = path;
            Name = name;
            m_Icon = icon;
            Uri = new Uri($"file:///{m_Path}");
        }

        public string CollectionType => "LocalFolderCollection";
        public string CollectionInstance => m_Path;

        public string Name { get; private set; }

        public Uri Uri { get; private set; }

        public Uri PreviewUri { get; }

        public string Description { get; }

        public Author[] Authors { get; set; }

        public ResourceLicense License { get; }

        public int NumResources => m_Resources?.Count ?? 0;

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

            Refresh();
        }

        public async IAsyncEnumerable<IResource> ContentsAsync()
        {
            foreach (var resource in m_Resources)
            {
                yield return resource;
            }
        }
        public void Refresh()
        {
            if (!m_Dir.Exists)
            {
                Debug.LogWarning($"Cannot read local folder {m_Dir.FullName}.");
                return;
            }
            m_Resources = new List<IResource>();
            m_Resources.AddRange(m_Dir.EnumerateDirectories().Where(dirInfo => !dirInfo.Name.StartsWith("."))
                .Select(dirInfo => new LocalFolderCollection(dirInfo.FullName, dirInfo.Name, m_Icon)));
            m_Resources.AddRange(m_Dir.EnumerateFiles("*.tilt").Select(fileInfo => new LocalFileResource(fileInfo.FullName)));
        }
        public event Action OnChanged;
        public event Action OnRefreshingChanged;

        public async Task<Stream> GetStreamAsync()
        {
            return null;
            throw new NotImplementedException();
        }
#pragma warning restore 1998

    }
}
