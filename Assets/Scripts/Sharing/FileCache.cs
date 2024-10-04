using System;
using System.IO;
using System.Linq;
namespace TiltBrush
{
    /// <summary>
    /// The FileCache is a simple cache that can store files in multiple 'filesets'.
    /// The idea is that a fileset is effectively a uniquely named folder that can store files.
    /// </summary>
    public class FileCache
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">The root path of the cache</param>
        /// <param name="maxMegabytes">The maximum size of the cache in megabytes</param>
        public FileCache(string path, long maxMegabytes)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            m_Root = new DirectoryInfo(path);
            if (!m_Root.Exists)
            {
                m_Root.Create();
            }

            m_MaxBytes = maxMegabytes * 1024 * 1024;
            ReadCacheSize();
            TrimCacheSize();
        }

        /// <summary>
        /// Trims folders within the cache directory if the maximum cache size is breached.
        /// Works on a last-accessed basis.
        /// </summary>
        public void TrimCacheSize()
        {
            foreach (var subdir in m_Root.EnumerateDirectories().OrderBy(x => x.LastWriteTimeUtc))
            {
                if (m_CurrentBytes < m_MaxBytes)
                {
                    m_Root.Refresh();
                    return;
                }
                long subdirSize = subdir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(x => x.Length);
                subdir.Delete(recursive: true);
                m_CurrentBytes -= subdirSize;
            }
        }

        /// <summary>
        /// Determines if a given fileset exists within the cache
        /// </summary>
        /// <param name="fileset">The name of the fileset</param>
        /// <returns>Whether it exists</returns>
        public bool FilesetExists(string fileset)
        {
            return Directory.Exists(Path.Combine(m_Root.FullName, fileset));
        }

        /// <summary>
        /// Checks whether a given file exists within a fileset.
        /// </summary>
        /// <param name="fileset"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool FileExists(string fileset, string filename)
        {
            string folder = Path.Combine(m_Root.FullName, fileset);
            string path = Path.Combine(folder, filename);
            return File.Exists(path);
        }

        /// <summary>
        /// Writes a file to a fileset
        /// </summary>
        /// <param name="fileset">The name of the fileset</param>
        /// <param name="filename">The file within the fileset</param>
        /// <param name="data">File bytes</param>
        public void Write(string fileset, string filename, byte[] data)
        {
            string folder = Path.Combine(m_Root.FullName, fileset);
            string path = Path.Combine(folder, filename);
            DirectoryInfo subdir = new DirectoryInfo(folder);
            bool createDir = !subdir.Exists;
            if (createDir)
            {
                Directory.CreateDirectory(folder);
            }
            else
            {
                subdir.LastWriteTimeUtc = DateTime.UtcNow;
            }
            File.WriteAllBytes(path, data);
            m_CurrentBytes += data.LongLength;
            if (createDir)
            {
                m_Root.Refresh();
            }
            TrimCacheSize();
        }

        /// <summary>
        /// Read all the bytes from a file in a fileset
        /// </summary>
        /// <param name="fileset">The fileset</param>
        /// <param name="filename">The file</param>
        /// <returns>All the bytes from the file</returns>
        public byte[] Read(string fileset, string filename)
        {
            string folder = Path.Combine(m_Root.FullName, fileset);
            string path = Path.Combine(folder, filename);
            return File.ReadAllBytes(path);
        }

        /// <summary>
        /// Read the bytes from a file in a fileset as a stream
        /// </summary>
        /// <param name="fileset">fileset</param>
        /// <param name="filename">filename</param>
        /// <returns>A stream of the bytes in the file</returns>
        public Stream ReadStream(string fileset, string filename)
        {
            string folder = Path.Combine(m_Root.FullName, fileset);
            string path = Path.Combine(folder, filename);
            return File.OpenRead(path);
        }

        /// <summary>
        /// Delete a file in a fileset
        /// </summary>
        /// <param name="fileset">Fileset</param>
        /// <param name="filename">File</param>
        public void DeleteFile(string fileset, string filename)
        {
            string folder = Path.Combine(m_Root.FullName, fileset);
            string path = Path.Combine(folder, filename);
            FileInfo file = new FileInfo(path);
            m_CurrentBytes -= file.Length;
            file.Delete();
        }

        /// <summary>
        /// Delete a fileset
        /// </summary>
        /// <param name="fileset">Fileset</param>
        public void DeleteFileset(string fileset)
        {
            string folder = Path.Combine(m_Root.FullName, fileset);
            DirectoryInfo subdir = new DirectoryInfo(folder);
            m_CurrentBytes -= subdir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(x => x.Length);
            Directory.Delete(fileset, recursive: true);
            m_Root.Refresh();
        }

        /// <summary>
        /// Deletes the entire cache.
        /// </summary>
        public void Clear()
        {
            m_Root.Delete(recursive: true);
        }

        public long CacheSize => m_CurrentBytes;

        private DirectoryInfo m_Root;
        private long m_MaxBytes;
        private long m_CurrentBytes;

        private void ReadCacheSize()
        {
            m_CurrentBytes = m_Root.EnumerateFiles("*", SearchOption.AllDirectories).Sum(x => x.Length);
        }
    }
}
