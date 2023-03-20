
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class DotTiltFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct TiltZipHeader
        {
            public uint sentinel;
            public ushort headerSize;
            public ushort headerVersion;
            public uint unused1;
            public uint unused2;
        }
        public const uint TILT_SENTINEL = 0x546c6974; // 'tilT'
        public const string FN_METADATA = "metadata.json";
        public const string FN_METADATA_LEGACY = "main.json"; // used pre-release only
        public const string FN_SKETCH = "data.sketch";
        public const string FN_THUMBNAIL = "thumbnail.png";
        public static ushort HEADER_VERSION = 1;
        public static ushort HEADER_SIZE = (ushort)Marshal.SizeOf<TiltZipHeader>();

        private IResource m_Resource;
        private FileInfo m_FileCache;

        public IResource Resource => m_Resource;

        public DotTiltFile(IResource resource)
        {
            m_Resource = resource;
        }

        ~DotTiltFile()
        {
            if (m_FileCache != null)
            {
                m_FileCache.Delete();
            }
        }

        public async Task<Stream> GetStreamAsync()
        {
            if (m_FileCache == null)
            {
                var original = await m_Resource.GetStreamAsync();
                if (original is FileStream)
                {
                    return original;
                }
                string tempFilename = Path.GetTempFileName();
                using (var fileStream = File.Create(tempFilename))
                {
                    await original.CopyToAsync(fileStream);
                    fileStream.Close();
                }
                original.Close();
                m_FileCache = new FileInfo(tempFilename);
            }
            return m_FileCache.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public async Task<bool> VerifyTiltHeaderAsync()
        {
            return ReadAndVerifyTiltHeader(await GetStreamAsync());
        }

        public bool ReadAndVerifyTiltHeader(Stream stream)
        {
            var reader = new BinaryReader(stream);
            var headerBytes = reader.ReadBytes(HEADER_SIZE);
            if (headerBytes.Length != 16)
            {
                Debug.Log($"Could not read Tilt file header - expected {HEADER_SIZE} bytes - got {headerBytes.Length}.");
                return false;
            }

            GCHandle gcHandle = GCHandle.Alloc(headerBytes, GCHandleType.Pinned);
            var header = Marshal.PtrToStructure<TiltZipHeader>(gcHandle.AddrOfPinnedObject());
            if (header.sentinel != TILT_SENTINEL)
            {
                Debug.Log($"Tilt File sentinel incorrect - expected 0x{TILT_SENTINEL:X}, got 0x{header.sentinel:X}.");
                return false;
            }
            if (header.headerVersion != HEADER_VERSION)
            {
                Debug.Log($"Unsupported Tilt File header version - expected {HEADER_VERSION}, got {header.headerVersion}.");
                return false;
            }
            if (header.headerSize < HEADER_SIZE)
            {
                Debug.Log($"Tilt File header error - header size too small - expected {HEADER_SIZE}, got {header.headerSize}.");
                return false;
            }
            if (header.headerSize > HEADER_SIZE)
            {
                stream.Seek(header.headerSize - HEADER_SIZE, SeekOrigin.Current);
            }
            gcHandle.Free();
            return true;
        }

        public async Task<Stream> GetSubFileAsync(string filename)
        {
            var stream = await GetStreamAsync();

            if (!ReadAndVerifyTiltHeader(stream))
            {
                return null;
            }

            var subStream = new SubStream(stream);
            var archive = new ZipArchive(subStream, ZipArchiveMode.Read);
            var entry = archive.GetEntry(filename);
            if (entry == null)
            {
                return null;
            }

            return entry.Open();
        }

        public async Task<Stream> GetMetaDataStreamAsync()
        {
            var stream = await GetSubFileAsync(FN_METADATA);
            if (stream == null)
            {
                return await GetSubFileAsync(FN_METADATA_LEGACY);
            }
            return stream;
        }

        public async Task<Stream> GetSketchStreamAsync()
        {
            return await GetSubFileAsync(FN_SKETCH);
        }

        public async Task<Stream> GetThumbnailStream()
        {
            return await GetSubFileAsync(FN_THUMBNAIL);
        }
    }
}