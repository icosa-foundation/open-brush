// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
#if USE_DOTNETZIP
using ZipSubfileReader = ZipSubfileReader_DotNetZip;
using ZipLibrary = Ionic.Zip;
#else
using ZipSubfileReader = TiltBrush.ZipSubfileReader_SharpZipLib;
using ZipLibrary = ICSharpCode.SharpZipLib.Zip;
#endif

namespace TiltBrush
{
    public interface IReopenableReadStream
    {
        Stream Open();
    }

    public class TiltFile
    {

        private const uint TILT_SENTINEL = 0x546c6974; // 'tilT'
        private const uint PKZIP_SENTINEL = 0x04034b50;

        // These are the only valid subfile names for GetStream()
        public const string FN_METADATA = "metadata.json";
        public const string FN_METADATA_LEGACY = "main.json"; // used pre-release only
        public const string FN_SKETCH = "data.sketch";
        public const string FN_THUMBNAIL = "thumbnail.png";
        public const string FN_HI_RES = "hires.png";

        public const string THUMBNAIL_MIME_TYPE = "image/png";
        public const string TILT_MIME_TYPE = "application/vnd.google-tiltbrush.tilt";

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
        private struct TiltZipHeader
        {
            public uint sentinel;
            public ushort headerSize;
            public ushort headerVersion;
            public uint unused1;
            public uint unused2;
        }
        public unsafe static ushort HEADER_SIZE = (ushort)sizeof(TiltZipHeader);
        public static ushort HEADER_VERSION = 1;

        private static void WriteTiltZipHeader(Stream stream, TiltZipHeader header)
        {
            unsafe
            {
                Debug.Assert(
                    HEADER_SIZE == Marshal.SizeOf(header),
                    "Reference types detected in TiltZipHeader");

                byte[] buffer = new byte[HEADER_SIZE];
                fixed (byte* bufferPointer = buffer)
                {
                    Marshal.StructureToPtr(header, (IntPtr)bufferPointer, false);
                    stream.Write(buffer, 0, buffer.Length);
                }
            }
        }

        /// Writes a zip-format .tilt archive to a caller-supplied stream.
        /// Destination replacement and transaction semantics belong to the caller.
        sealed public class ArchiveWriter : IDisposable
        {
            private ZipLibrary.ZipOutputStream m_zipstream;
            private bool m_finished;

            public ArchiveWriter(Stream outputStream, bool ownsOutputStream = true)
            {
                if (outputStream == null)
                {
                    throw new ArgumentNullException(nameof(outputStream));
                }
                if (!outputStream.CanWrite)
                {
                    throw new ArgumentException("Tilt archive output stream is not writable.",
                        nameof(outputStream));
                }

                var header = new TiltZipHeader
                {
                    sentinel = TILT_SENTINEL,
                    headerSize = HEADER_SIZE,
                    headerVersion = HEADER_VERSION,
                };
                WriteTiltZipHeader(outputStream, header);
                m_zipstream = new ZipLibrary.ZipOutputStream(outputStream);
#if USE_DOTNETZIP
                // Ionic.Zip documentation says compression level None can produce archives that
                // the default macOS reader cannot open. Compression method None is compatible.
                m_zipstream.CompressionMethod = ZipLibrary.CompressionMethod.None;
                m_zipstream.EnableZip64 = ZipLibrary.Zip64Option.Never;
#else
                m_zipstream.IsStreamOwner = ownsOutputStream;
                m_zipstream.SetLevel(0);
                m_zipstream.UseZip64 = ZipLibrary.UseZip64.Off;
#endif
            }

            public Stream GetWriteStream(string subfileName)
            {
                if (m_finished)
                {
                    throw new InvalidOperationException("Tilt archive is already complete.");
                }
#if USE_DOTNETZIP
                var entry = m_zipstream.PutNextEntry(subfileName);
                entry.LastModified = DateTime.Now;
                return new ZipOutputStreamWrapper_DotNetZip(m_zipstream);
#else
                var entry = new ZipLibrary.ZipEntry(subfileName)
                {
                    DateTime = DateTime.Now,
                    CompressionMethod = m_zipstream.GetLevel() == 0
                        ? ZipLibrary.CompressionMethod.Stored
                        : ZipLibrary.CompressionMethod.Deflated
                };
                return new ZipOutputStreamWrapper_SharpZipLib(m_zipstream, entry);
#endif
            }

            public void Complete()
            {
                if (m_finished)
                {
                    return;
                }

                m_finished = true;
                m_zipstream.Dispose();
                m_zipstream = null;
            }

            public void Dispose()
            {
                Complete();
            }
        }

        /// Writes .tilt files and directories in as atomic a fashion as possible.
        /// Use in a using() block, and call Commit() or Rollback() when done.
        sealed public class AtomicWriter : IDisposable
        {
            private string m_destination;
            private string m_temporaryPath;
            private bool m_finished = false;

            private ArchiveWriter m_archiveWriter;

            public AtomicWriter(string path)
            {
                m_destination = path;
                m_temporaryPath = path + "_part";
                Destroy(m_temporaryPath);

                bool useZip;
                switch (DevOptions.I.PreferredTiltFormat)
                {
                    case TiltFormat.Directory:
                        useZip = false;
                        break;
                    case TiltFormat.Inherit:
                        useZip = !Directory.Exists(path);
                        break;
                    default:
                    case TiltFormat.Zip:
                        useZip = true;
                        break;
                }
                if (useZip)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(m_temporaryPath));
                    FileStream tmpfs = new FileStream(m_temporaryPath, FileMode.Create, FileAccess.Write);
                    m_archiveWriter = new ArchiveWriter(tmpfs);
                }
                else
                {
                    Directory.CreateDirectory(m_temporaryPath);
                }
            }

            /// Returns a writable stream to an empty subfile.
            public Stream GetWriteStream(string subfileName)
            {
                Debug.Assert(!m_finished);
                if (m_archiveWriter != null)
                {
                    return m_archiveWriter.GetWriteStream(subfileName);
                }
                else
                {
                    Directory.CreateDirectory(m_temporaryPath);
                    string fullPath = Path.Combine(m_temporaryPath, subfileName);
                    return new FileStream(fullPath, FileMode.Create, FileAccess.Write);
                }
            }

            /// Raises exception on failure.
            /// On failure, existing file is untouched.
            public void Commit()
            {
                if (m_finished) { return; }
                m_finished = true;

                if (m_archiveWriter != null)
                {
                    m_archiveWriter.Complete();
                    m_archiveWriter = null;
                }

                string previous = m_destination + "_previous";
                Destroy(previous);
                // Don't destroy previous version until we know the new version is in place.
                try { Rename(m_destination, previous); }
                // The *NotFound exceptions are benign; they happen when writing a new file.
                // Let the other IOExceptions bubble up; they probably indicate some problem
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                Rename(m_temporaryPath, m_destination);
                Destroy(previous);
            }

            public void Rollback()
            {
                if (m_finished) { return; }
                m_finished = true;

                if (m_archiveWriter != null)
                {
                    m_archiveWriter.Dispose();
                    m_archiveWriter = null;
                }

                Destroy(m_temporaryPath);
            }

            // IDisposable support

            ~AtomicWriter() { Dispose(); }
            public void Dispose()
            {
                if (!m_finished) { Rollback(); }
                GC.SuppressFinalize(this);
            }

            // Static API

            // newpath must not already exist
            private static void Rename(string oldpath, string newpath)
            {
                Directory.Move(oldpath, newpath);
            }

            // Handles directories, files, and read-only flags.
            private static void Destroy(string path)
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    RecursiveUnsetReadOnly(path);
                    Directory.Delete(path, true);
                }
            }

            private static void RecursiveUnsetReadOnly(string directory)
            {
                foreach (string sub in Directory.GetFiles(directory))
                {
                    File.SetAttributes(Path.Combine(directory, sub), FileAttributes.Normal);
                }
                foreach (string sub in Directory.GetDirectories(directory))
                {
                    RecursiveUnsetReadOnly(Path.Combine(directory, sub));
                }
            }
        }

        private readonly string m_Fullpath;
        private readonly IReopenableReadStream m_StreamSource;
        private readonly string m_DisplayName;

        public TiltFile(string fullpath)
        {
            m_Fullpath = fullpath;
            m_StreamSource = null;
            m_DisplayName = fullpath;
        }

        public TiltFile(IReopenableReadStream streamSource, string displayName)
        {
            m_Fullpath = null;
            m_StreamSource = streamSource ?? throw new ArgumentNullException(nameof(streamSource));
            m_DisplayName = string.IsNullOrEmpty(displayName) ? "<stream>" : displayName;
        }

        private static TiltZipHeader ReadTiltZipHeader(Stream s)
        {
            byte[] buf = new byte[HEADER_SIZE];
            int totalRead = 0;
            while (totalRead < buf.Length)
            {
                int read = s.Read(buf, totalRead, buf.Length - totalRead);
                if (read == 0)
                {
                    throw new EndOfStreamException("Incomplete .tilt header.");
                }
                totalRead += read;
            }
            unsafe
            {
                fixed (byte* bufp = buf)
                {
                    return *(TiltZipHeader*)bufp;
                }
            }
        }

        /// Returns a readable stream to a pre-existing subfile,
        /// or null if the subfile does not exist,
        /// or null if the file format is invalid.
        public Stream GetReadStream(string subfileName)
        {
            if (m_StreamSource != null)
            {
                if (!IsHeaderValid())
                {
                    return null;
                }

                Stream archiveStream = null;
                try
                {
                    archiveStream = m_StreamSource.Open();
                    Stream result = new ZipSubfileReader(archiveStream, subfileName);
                    archiveStream = null;
                    return result;
                }
                catch (ZipLibrary.ZipException e)
                {
                    Debug.LogFormat("{0}", e);
                    return null;
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                finally
                {
                    archiveStream?.Dispose();
                }
            }

            if (File.Exists(m_Fullpath))
            {
                // It takes a long time to figure out a file isn't a .zip, so it's worth the
                // price of a quick check up-front
                if (!IsHeaderValid())
                {
                    return null;
                }
                try
                {
                    return new ZipSubfileReader(m_Fullpath, subfileName);
                }
                catch (ZipLibrary.ZipException e)
                {
                    Debug.LogFormat("{0}", e);
                    return null;
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
            }

            string fullPath = Path.Combine(m_Fullpath, subfileName);
            try
            {
                return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        public bool IsHeaderValid()
        {
            if (m_StreamSource != null)
            {
                try
                {
                    using (Stream stream = m_StreamSource.Open())
                    {
                        return IsHeaderValid(stream, m_DisplayName);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.LogFormat("Document does not have read permissions: {0}", m_DisplayName);
                    return false;
                }
                catch (IOException)
                {
                    return false;
                }
            }

            if (File.Exists(m_Fullpath))
            {
                try
                {
                    using (var stream = new FileStream(m_Fullpath, FileMode.Open, FileAccess.Read))
                    {
                        return IsHeaderValid(stream, m_Fullpath);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.LogFormat("File does not have read permissions: {0}", m_Fullpath);
                    return false;
                }
                catch (IOException)
                {
                    // Might be a temporary thing (eg sharing violation); being conservative for now
                    return false;
                }
            }

            if (Directory.Exists(m_Fullpath))
            {
                // Directories don't have a header but we can do some roughly-equivalent
                // sanity-checking
                return (File.Exists(Path.Combine(m_Fullpath, FN_METADATA)) &&
                    File.Exists(Path.Combine(m_Fullpath, FN_SKETCH)) &&
                    File.Exists(Path.Combine(m_Fullpath, FN_THUMBNAIL)));
            }
            return false;
        }

        public static bool IsHeaderValid(Stream stream, string displayName = "<stream>")
        {
            if (stream == null || !stream.CanRead)
            {
                return false;
            }
            if (!stream.CanSeek)
            {
                Debug.LogFormat("Tilt archive is not seekable: {0}", displayName);
                return false;
            }

            long originalPosition = stream.Position;
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                var header = ReadTiltZipHeader(stream);
                if (header.sentinel != TILT_SENTINEL || header.headerVersion != HEADER_VERSION)
                {
                    Debug.LogFormat("Bad .tilt sentinel or header: {0}", displayName);
                    return false;
                }
                if (header.headerSize < HEADER_SIZE)
                {
                    Debug.LogFormat("Unexpected header length: {0}", displayName);
                    return false;
                }
                stream.Seek(header.headerSize, SeekOrigin.Begin);
                if ((new BinaryReader(stream)).ReadUInt32() != PKZIP_SENTINEL)
                {
                    Debug.LogFormat("Zip sentinel not found: {0}", displayName);
                    return false;
                }
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            finally
            {
                try
                {
                    stream.Seek(originalPosition, SeekOrigin.Begin);
                }
                catch (IOException)
                {
                    // Header validation has already completed; a failed position restore should
                    // not obscure its result. Callers open a fresh stream to read the archive.
                }
            }
        }

        public static bool IsArchiveValid(
            Stream stream,
            string displayName = "<stream>",
            bool testData = false)
        {
            if (!IsHeaderValid(stream, displayName))
            {
                return false;
            }

            long originalPosition = stream.Position;
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
#if USE_DOTNETZIP
                using (var archive = ZipLibrary.ZipFile.Read(stream))
                {
                    if (archive[FN_SKETCH] == null ||
                        archive[FN_METADATA] == null &&
                        archive[FN_METADATA_LEGACY] == null)
                    {
                        return false;
                    }
                    if (testData)
                    {
                        foreach (ZipLibrary.ZipEntry entry in archive)
                        {
                            using (Stream input = entry.OpenReader())
                            {
                                input.CopyTo(Stream.Null);
                            }
                        }
                    }
                    return true;
                }
#else
                using (var archive = new ZipLibrary.ZipFile(stream)
                {
                    IsStreamOwner = false,
                })
                {
                    if (archive.GetEntry(FN_SKETCH) == null ||
                        archive.GetEntry(FN_METADATA) == null &&
                        archive.GetEntry(FN_METADATA_LEGACY) == null)
                    {
                        return false;
                    }
                    return archive.TestArchive(testData);
                }
#endif
            }
            catch (Exception e) when (
                e is IOException ||
                e is ZipLibrary.ZipException ||
                e is InvalidOperationException)
            {
                Debug.LogFormat("Invalid .tilt archive {0}: {1}", displayName, e.Message);
                return false;
            }
            finally
            {
                try
                {
                    stream.Seek(originalPosition, SeekOrigin.Begin);
                }
                catch (IOException)
                {
                    // The caller will observe the provider failure on its next operation.
                }
            }
        }

        public bool IsLoadable()
        {
            if (!IsHeaderValid())
            {
                return false;
            }

            using (Stream sketch = GetReadStream(FN_SKETCH))
            {
                if (sketch == null)
                {
                    return false;
                }
            }

            using (Stream metadata = GetReadStream(FN_METADATA) ?? GetReadStream(FN_METADATA_LEGACY))
            {
                return metadata != null;
            }
        }

    }

} // namespace TiltBrush
