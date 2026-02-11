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
using UnityEngine;

using ZipSubfileReader = TiltBrush.ZipSubfileReader_SharpZipLib;
using ZipLibrary = ICSharpCode.SharpZipLib.Zip;

namespace TiltBrush
{

    /// Writes files and directories in as atomic a fashion as possible.
    /// Use in a using() block, and call Commit() or Rollback() when done.
    public class AtomicWriter : IDisposable
    {
        private string m_destination;
        private string m_temporaryPath;
        private bool m_finished = false;

        [Serializable] public enum SaveFormat { Directory, Inherit, Zip }

        private ZipLibrary.ZipOutputStream m_zipstream = null;

        protected virtual void WriteHeader(Stream s) { }

        public AtomicWriter(string path, SaveFormat format = SaveFormat.Zip)
        {
            m_destination = path;
            m_temporaryPath = path + "_part";
            Destroy(m_temporaryPath);

            bool useZip;
            switch (format)
            {
                case SaveFormat.Directory: useZip = false; break;
                case SaveFormat.Inherit: useZip = !Directory.Exists(path); break;
                default:
                case SaveFormat.Zip: useZip = true; break;
            }
            if (useZip)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(m_temporaryPath));
                FileStream tmpfs = new FileStream(m_temporaryPath, FileMode.Create, FileAccess.Write);
                // derived classes have the option of writing a header here
                WriteHeader(tmpfs);
                m_zipstream = new ZipLibrary.ZipOutputStream(tmpfs);
                m_zipstream.SetLevel(0); // no compression
                                         // Since we don't have size info up front, it conservatively assumes 64-bit.
                                         // We turn it off to maximize compatibility with wider ecosystem (eg, osx unzip).
                m_zipstream.UseZip64 = ZipLibrary.UseZip64.Off;
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
            if (m_zipstream != null)
            {
                var entry = new ZipLibrary.ZipEntry(subfileName);
                entry.DateTime = System.DateTime.Now;
                // There is such a thing as "Deflated, compression level 0".
                // Explicitly use "Stored".
                entry.CompressionMethod = (m_zipstream.GetLevel() == 0)
                  ? ZipLibrary.CompressionMethod.Stored
                  : ZipLibrary.CompressionMethod.Deflated;
                return new ZipOutputStreamWrapper_SharpZipLib(m_zipstream, entry);
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

            if (m_zipstream != null)
            {
                m_zipstream.Dispose();
                m_zipstream = null;
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

            if (m_zipstream != null)
            {
                m_zipstream.Dispose();
                m_zipstream = null;
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

} // namespace TiltBrush
