using System.IO;
using System.Threading.Tasks;
namespace TiltBrush
{
    public class WritableLocalFileResource : LocalFileResource, IWritableResource
    {
        public WritableLocalFileResource(string path) : base(path)
        {

        }

        public string Name
        {
            get => base.Name;
            set
            {
                throw new System.NotImplementedException();
            }
        }
        public string Description
        {
            get => base.Description;
            set
            {
                throw new System.NotImplementedException();
            }
        }
        public async Task<Stream> GetWriteStreamAsync()
        {
            throw new System.NotImplementedException();
        }

        public bool Delete()
        {
            File.Delete(m_Path);
            return !File.Exists(m_Path);
        }
    }
}
