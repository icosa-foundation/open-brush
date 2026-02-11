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
using System.Collections.Generic;
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

    public class TiltFile : FolderOrZipReader
    {

        private const uint TILT_SENTINEL = 0x546c6974;   // 'tilT'
        private const uint PKZIP_SENTINEL = 0x04034b50;

        // These are the only valid subfile names for GetStream()
        public const string FN_METADATA = "metadata.json";
        public const string FN_METADATA_LEGACY = "main.json";  // used pre-release only
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

        /// Writes .tilt files and directories in as atomic a fashion as possible.
        /// Use in a using() block, and call Commit() or Rollback() when done.
        sealed public class TiltAtomicWriter : AtomicWriter
        {
            private string m_destination;
            private string m_temporaryPath;

            public TiltAtomicWriter(string path) : base(path, DevOptions.I.PreferredTiltFormat)
            {

            }

            protected override void WriteHeader(Stream s)
            {
                var header = new TiltZipHeader
                {
                    sentinel = TILT_SENTINEL,
                    headerSize = HEADER_SIZE,
                    headerVersion = HEADER_VERSION,
                };
                unsafe
                {
                    // This doesn't work because we need a byte[] to pass to Stream
                    // byte* bufp = stackalloc byte[sizeof(TiltZipHeader)];

                    // This doesn't work because the type is also byte*, not byte[]
                    // unsafe struct Foo { fixed byte buf[N]; }

                    Debug.Assert(
                        HEADER_SIZE == Marshal.SizeOf(header),
                        "Reference types detected in TiltZipHeader");

                    byte[] buf = new byte[HEADER_SIZE];
                    fixed (byte* bufp = buf)
                    {
                        IntPtr bufip = (IntPtr)bufp;
                        // Copy from undefined CLR layout to explicitly-defined layout
                        Marshal.StructureToPtr(header, bufip, false);
                        s.Write(buf, 0, buf.Length);
                        // Need this if there are reference types, but in that case
                        // there are other complications (like demarshaling)
                        // Marshal.DestroyStructure(bufip, typeof(TiltZipHeader));
                    }
                }
            }
        }

        public TiltFile(string fullpath) : base(fullpath)
        {
        }

        private static TiltZipHeader ReadTiltZipHeader(Stream s)
        {
            byte[] buf = new byte[HEADER_SIZE];
            s.Read(buf, 0, buf.Length);
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
        override public Stream GetReadStream(string subfileName)
        {
            if (m_Exists && m_IsFile)
            {
                // It takes a long time to figure out a file isn't a .zip, so it's worth the
                // price of a quick check up-front
                if (!IsHeaderValid())
                {
                    return null;
                }
            }
            return base.GetReadStream(subfileName);
        }

        override public IEnumerable<string> GetContentsAt(string path)
        {
            if (m_Exists && m_IsFile)
            {
                if (!IsHeaderValid())
                {
                    return null;
                }
            }
            return base.GetContentsAt(path);
        }

        public bool IsHeaderValid()
        {
            if (m_Exists && m_IsFile)
            {
                try
                {
                    using (var stream = new FileStream(m_RootPath, FileMode.Open, FileAccess.Read))
                    {
                        var header = ReadTiltZipHeader(stream);
                        if (header.sentinel != TILT_SENTINEL || header.headerVersion != HEADER_VERSION)
                        {
                            Debug.LogFormat("Bad .tilt sentinel or header: {0}", m_RootPath);
                            return false;
                        }
                        if (header.headerSize < HEADER_SIZE)
                        {
                            Debug.LogFormat("Unexpected header length: {0}", m_RootPath);
                            return false;
                        }
                        stream.Seek(header.headerSize - HEADER_SIZE, SeekOrigin.Current);
                        if ((new BinaryReader(stream)).ReadUInt32() != PKZIP_SENTINEL)
                        {
                            Debug.LogFormat("Zip sentinel not found: {0}", m_RootPath);
                            return false;
                        }
                        return true;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.LogFormat("File does not have read permissions: {0}", m_RootPath);
                    return false;
                }
                catch (IOException)
                {
                    // Might be a temporary thing (eg sharing violation); being conservative for now
                    return false;
                }
            }

            if (Directory.Exists(m_RootPath))
            {
                // Directories don't have a header but we can do some roughly-equivalent
                // sanity-checking
                return (File.Exists(Path.Combine(m_RootPath, FN_METADATA)) &&
                        File.Exists(Path.Combine(m_RootPath, FN_SKETCH)) &&
                        File.Exists(Path.Combine(m_RootPath, FN_THUMBNAIL)));
            }
            return false;
        }

    }

}  // namespace TiltBrush
