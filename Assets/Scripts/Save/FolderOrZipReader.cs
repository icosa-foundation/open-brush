// Copyright 2020 The Open Brush Authors
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

using System.Collections.Generic;
using System.IO;
using System.Linq;

using ZipSubfileReader = TiltBrush.ZipSubfileReader_SharpZipLib;
using ZipLibrary = ICSharpCode.SharpZipLib.Zip;


namespace TiltBrush
{
    /// <summary>
    /// Class used to read from a zipped file or a folder.
    /// </summary>
    public class FolderOrZipReader
    {
        protected bool m_IsFile;
        protected bool m_Exists;
        protected string m_RootPath;
        protected Dictionary<string, string> m_ZipEntryMap = new Dictionary<string, string>();
        protected string m_subfolder;

        public bool IsZip => m_IsFile;

        public FolderOrZipReader(string path)
        {
            m_RootPath = path;
            m_subfolder = "";
            bool fileExists = File.Exists(path);
            bool dirExists = Directory.Exists(path);
            m_Exists = fileExists || dirExists;
            if (!m_Exists)
            {
                return;
            }
            m_IsFile = Path.HasExtension(path) && !dirExists;
            if (m_IsFile)
            {
                SetRootFolder("");
            }
        }

        public void SetRootFolder(string subfolder)
        {
            if (!m_Exists || !m_IsFile)
            {
                return;
            }
            m_subfolder = subfolder.Replace('\\', '/');
            if (!string.IsNullOrEmpty(m_subfolder) && !m_subfolder.EndsWith("/"))
            {
                m_subfolder += '/';
            };
            using (var zipFile = new ZipLibrary.ZipFile(m_RootPath))
            {
                foreach (ZipLibrary.ZipEntry entry in zipFile)
                {
                    string name = entry.Name;
                    if (string.IsNullOrEmpty(m_subfolder) || name.StartsWith(m_subfolder))
                    {
                        name = name.Substring(m_subfolder.Length);
                        m_ZipEntryMap[name.ToLowerInvariant()] = entry.Name;
                    }
                }
                zipFile.Close();
            }
        }

        public bool Exists(string filename)
        {
            if (m_IsFile)
            {
                string filenameLower = filename.ToLowerInvariant();
                return m_Exists && m_ZipEntryMap.ContainsKey(filenameLower);
            }
            string path = Path.Combine(m_RootPath, filename);
            return File.Exists(path);
        }

        public string Find(string filename)
        {
            if (!m_Exists)
            {
                return null;
            }
            string key = m_ZipEntryMap.Keys.FirstOrDefault(x => x.EndsWith(filename.ToLowerInvariant()));
            if (key != null)
            {
                return m_ZipEntryMap[key];
            }
            return null;
        }

        virtual public Stream GetReadStream(string filename)
        {
            if (!m_Exists)
            {
                return null;
            }
            if (m_IsFile)
            {
                string filenameLower = Path.Combine(m_subfolder, filename).ToLowerInvariant();
                if (!m_ZipEntryMap.ContainsKey(filenameLower))
                {
                    return null;
                }
                return new ZipSubfileReader(m_RootPath, m_ZipEntryMap[filenameLower]);
            }
            else
            {
                string path = Path.Combine(m_RootPath, filename);
                if (File.Exists(path))
                {
                    return new FileStream(path, FileMode.Open, FileAccess.Read);
                }
            }
            return null;
        }

        virtual public IEnumerable<string> GetContentsAt(string path)
        {
            if (m_IsFile)
            {
                var entries = new HashSet<string>();
                using (var zipFile = new ZipLibrary.ZipFile(m_RootPath))
                {
                    foreach (ZipLibrary.ZipEntry entry in zipFile)
                    {
                        if (entry == null)
                        {
                            continue;
                        }

                        if (entry.Name.StartsWith(path))
                        {
                            string subpart = Path.GetDirectoryName(entry.Name.Substring(path.Length));
                            if (subpart[0] == '/' || subpart[0] == '\\')
                            {
                                subpart = subpart.Substring(1);
                            }
                            entries.Add(subpart);
                        }
                    }
                }
                return entries;
            }
            else
            {
                string folderPath = Path.Combine(m_RootPath, path);
                return Directory.GetFiles(folderPath);
            }
        }
    }
}
