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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TiltBrush;
using UnityEngine;

using ZipSubfileReader = TiltBrush.ZipSubfileReader_SharpZipLib;
using ZipLibrary = ICSharpCode.SharpZipLibUnityPort.Zip;


namespace TiltBrush {
/// <summary>
/// Class used to read from a zipped file or a folder.
/// </summary>
public class FileOrZip {
  private bool m_IsFile;
  private bool m_Exists;
  private string m_RootPath;
  private Dictionary<string, string> m_ZipEntryMap = new Dictionary<string, string>();
  private string m_ZipName;

  public delegate void ReadHeaderDelegate(Stream s);
  
  public FileOrZip(string path, ReadHeaderDelegate header = null) {
    m_RootPath = path;
    bool fileExists = File.Exists(path);
    bool dirExists = Directory.Exists(path);
    m_Exists = fileExists || dirExists;
    m_IsFile = (fileExists || Path.HasExtension(path)) && !dirExists;
    if (!m_Exists) {
      if (m_IsFile) {
        
      } else {
        Directory.CreateDirectory(path);
      }
    } else {
      if (m_IsFile) {
        using (var zipFile = new ZipLibrary.ZipFile(path)) {
          m_ZipName = zipFile[0].Name;
          int initialPathLength = m_ZipName.Length;
          foreach (ZipLibrary.ZipEntry entry in zipFile) {
            if (entry.Name.Length > initialPathLength) {
              m_ZipEntryMap[entry.Name.Substring(initialPathLength).ToLowerInvariant()] = entry.Name;
            }
          }
          zipFile.Close();
        }
      }
    }
  }

  public bool Exists(string filename) {
    if (m_IsFile) {
      string filenameLower = filename.ToLowerInvariant();
      return m_Exists && m_ZipEntryMap.ContainsKey(filenameLower);
    } 
    string path = Path.Combine(m_RootPath, filename);
    return File.Exists(path);
  }

  public Stream GetReadStream(string filename) {
    if (!m_Exists) {
      return null;
    }
    if (m_IsFile) {
      string filenameLower = filename.ToLowerInvariant();
      if (!m_ZipEntryMap.ContainsKey(filenameLower)) {
        return null;
      }
      return new ZipSubfileReader(m_RootPath, m_ZipEntryMap[filenameLower]);
    } else {
      string path = Path.Combine(m_RootPath, filename);
      if (File.Exists(path)) {
        return new FileStream(path, FileMode.Open, FileAccess.Read);
      }
    }
    return null;
  }
}
}
