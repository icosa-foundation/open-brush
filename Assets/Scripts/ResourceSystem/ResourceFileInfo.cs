using System.IO;
using System.Threading.Tasks;
using USD.NET;
namespace TiltBrush
{
    public class ResourceFileInfo : SceneFileInfo
    {
        private IResource m_Resource;
        private DotTiltFile m_TiltFile;

        public ResourceFileInfo(IResource resource)
        {
            m_Resource = resource;
            m_TiltFile = new DotTiltFile(m_Resource);
        }
        public FileInfoType InfoType => FileInfoType.Cloud; // TODO: this should probably do something sensible here
        public string HumanName => m_Resource.Name;
        public bool Valid => true; // TODO: Not sure if this should always be true
        public bool Available => true; // Maybe?
        public string FullPath => m_Resource.Uri.AbsoluteUri; // Unsure if this is correct
        public bool Exists => true; // ?
        public bool ReadOnly => true; // TODO: For now only read-only
        public string AssetId => "";
        public string SourceId => "";
        public int? TriangleCount => null;
        public void Delete()
        {
            throw new System.NotImplementedException();
        }
        public bool IsHeaderValid()
        {
            return true; // TODO : FAAAEEEEKE
        }
        public async Task<Stream> GetReadStreamAsync(string subfileName)
        {
            return await m_TiltFile.GetSubFileAsync(subfileName);
        }
    }
}
