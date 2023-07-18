using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
namespace TiltBrush
{
    public class RemoteFileInfo : SceneFileInfo
    {
        private string m_humanName;
        private Uri m_Uri;
        private TiltFile m_TiltFile;

        public RemoteFileInfo(string url)
        {
            m_Uri = new Uri(url);
            HumanName = m_Uri.Segments.LastOrDefault();

        }

        public RemoteFileInfo(Uri uri)
        {
            m_Uri = uri;
            HumanName = m_Uri.Segments.LastOrDefault();

        }
        public FileInfoType InfoType => FileInfoType.Cloud;
        public string HumanName { get; set; }
        public bool Valid => true;
        public bool Available => m_TiltFile != null;
        public string FullPath => m_Uri.ToString();
        public bool Exists => true;
        public bool ReadOnly => true;
        public string AssetId => null;
        public string SourceId => "";
        public int? TriangleCount => null;
        public void Delete()
        {
            throw new System.NotImplementedException();
        }
        public string Rename(string newName)
        {
            throw new NotImplementedException();
        }
        public bool IsHeaderValid()
        {
            return true;
        }
        public Task<Stream> GetReadStreamAsync(string subfileName)
        {
            return App.HttpClient.GetStreamAsync(m_Uri);
        }
    }
}
