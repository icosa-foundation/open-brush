using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TiltBrush
{
    /// <summary>
    /// Cache where remote or local files can be requested, and remote files will be cached.
    /// Local files will just be read directly.
    /// </summary>
    public class DownloadingCache
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cache">File cache</param>
        public DownloadingCache(FileCache cache)
        {
            m_Cache = cache;
        }

        // TODO: I think this needs to store metadata about where the file came from.
        // and do something about files that have changed etc

        /// <summary>
        /// Read a file from a location. If the file does not exist, it will be cached
        /// at the given fileset and filename.
        /// </summary>
        /// <param name="fileset">Fileset to store/retrieve</param>
        /// <param name="filename">Filename</param>
        /// <param name="url">Location to load - should be http(s):// or file://</param>
        /// <returns>Task that returns the bytes for a file.</returns>
        public async Task<byte[]> Read(string fileset, string filename, string url)
        {
            const string fileStart = "file://";
            const string httpStart = "http";
            if (m_Cache.FileExists(fileset, filename))
            {
                return m_Cache.Read(fileset, filename);
            }
            if (url.StartsWith(fileStart))
            {
                return File.ReadAllBytes(url.Skip(fileStart.Length).ToString());
            }
            else if (url.StartsWith(httpStart))
            {
                byte[] bytes = await App.HttpClient.GetByteArrayAsync(url);
                m_Cache.Write(fileset, filename, bytes);
                return bytes;
            }
            return null;
        }

        private FileCache m_Cache;
    }
}
