// Copyright 2026 The Open Brush Authors
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
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using DriveData = Google.Apis.Drive.v3.Data;

namespace TiltBrush
{
    public sealed class DriveSyncLedger
    {
        private const int kVersion = 1;

        [Serializable]
        private sealed class LedgerFile
        {
            public int Version;
            public string AccountNamespace;
            public string DriveRootNamespace;
            public string StorageRootNamespace;
            public List<Entry> Entries = new List<Entry>();
        }

        [Serializable]
        public sealed class Entry
        {
            public string Area;
            public string RelativePath;
            public string DriveFileId;
            public long? DriveVersion;
            public string DriveMd5;
            public long? DriveSize;
            public long? DriveModifiedUtcTicks;
            public string StorageDocumentId;
            public long? StorageSize;
            public long? StorageModifiedUtcTicks;
            public string StorageSha256;
            public string StorageMd5;
            public string LastDirection;
            public long ConfirmedUtcTicks;
        }

        private readonly object m_Gate = new object();
        private readonly string m_Path;
        private readonly string m_AccountNamespace;
        private readonly string m_DriveRootNamespace;
        private readonly string m_StorageRootNamespace;
        private LedgerFile m_File;

        public DriveSyncLedger(
            string accountIdentity,
            string driveRootIdentity,
            string storageRootIdentity,
            string basePath = null)
        {
            m_AccountNamespace = Namespace(accountIdentity);
            m_DriveRootNamespace = Namespace(driveRootIdentity);
            m_StorageRootNamespace = Namespace(storageRootIdentity);
            string root = basePath ?? Path.Combine(
                Application.persistentDataPath, "OpenBrushDriveSyncLedger");
            m_Path = Path.Combine(
                root,
                m_AccountNamespace,
                m_DriveRootNamespace,
                $"{m_StorageRootNamespace}.json");
        }

        public Entry Get(StorageArea area, string relativePath)
        {
            lock (m_Gate)
            {
                EnsureLoaded();
                return m_File.Entries.FirstOrDefault(entry =>
                    entry.Area == area.ToString() &&
                    string.Equals(
                        entry.RelativePath,
                        Normalize(relativePath),
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool StorageMatches(
            Entry entry, StorageDocument document, Func<string> getSha256)
        {
            if (entry == null || document == null)
            {
                return false;
            }
            if (entry.StorageDocumentId == document.DocumentId.Value &&
                entry.StorageSize == document.Size &&
                TimesEqual(
                    entry.StorageModifiedUtcTicks,
                    document.LastModified?.ToUniversalTime().Ticks))
            {
                return true;
            }
            return !string.IsNullOrEmpty(entry.StorageSha256) &&
                getSha256 != null &&
                string.Equals(
                    entry.StorageSha256, getSha256(), StringComparison.Ordinal);
        }

        public bool DriveMatches(Entry entry, DriveData.File driveFile)
        {
            if (entry == null || driveFile == null ||
                entry.DriveFileId != driveFile.Id)
            {
                return false;
            }
            if (entry.DriveVersion.HasValue && driveFile.Version.HasValue)
            {
                return entry.DriveVersion == driveFile.Version;
            }
            if (!string.IsNullOrEmpty(entry.DriveMd5) &&
                !string.IsNullOrEmpty(driveFile.Md5Checksum))
            {
                return string.Equals(
                    entry.DriveMd5,
                    driveFile.Md5Checksum,
                    StringComparison.OrdinalIgnoreCase);
            }
            return entry.DriveSize == driveFile.Size &&
                TimesEqual(
                    entry.DriveModifiedUtcTicks,
                    driveFile.ModifiedTime?.ToUniversalTime().Ticks);
        }

        public void Confirm(
            StorageArea area,
            string relativePath,
            StorageDocument storageDocument,
            string storageSha256,
            string storageMd5,
            DriveData.File driveFile,
            string direction)
        {
            if (storageDocument == null)
            {
                throw new ArgumentNullException(nameof(storageDocument));
            }
            if (driveFile == null || string.IsNullOrEmpty(driveFile.Id))
            {
                throw new ArgumentException(
                    "Drive metadata is missing its file identity.", nameof(driveFile));
            }
            lock (m_Gate)
            {
                EnsureLoaded();
                string normalized = Normalize(relativePath);
                Entry entry = m_File.Entries.FirstOrDefault(candidate =>
                    candidate.Area == area.ToString() &&
                    string.Equals(
                        candidate.RelativePath,
                        normalized,
                        StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    entry = new Entry
                    {
                        Area = area.ToString(),
                        RelativePath = normalized,
                    };
                    m_File.Entries.Add(entry);
                }
                entry.DriveFileId = driveFile.Id;
                entry.DriveVersion = driveFile.Version;
                entry.DriveMd5 = driveFile.Md5Checksum;
                entry.DriveSize = driveFile.Size;
                entry.DriveModifiedUtcTicks =
                    driveFile.ModifiedTime?.ToUniversalTime().Ticks;
                entry.StorageDocumentId = storageDocument.DocumentId.Value;
                entry.StorageSize = storageDocument.Size;
                entry.StorageModifiedUtcTicks =
                    storageDocument.LastModified?.ToUniversalTime().Ticks;
                entry.StorageSha256 = storageSha256;
                entry.StorageMd5 = storageMd5;
                entry.LastDirection = direction;
                entry.ConfirmedUtcTicks = DateTime.UtcNow.Ticks;
                m_File.Entries.Sort((left, right) =>
                {
                    int areaOrder = StringComparer.Ordinal.Compare(left.Area, right.Area);
                    return areaOrder != 0
                        ? areaOrder
                        : StringComparer.OrdinalIgnoreCase.Compare(
                            left.RelativePath, right.RelativePath);
                });
                WriteAtomically();
            }
        }

        private void EnsureLoaded()
        {
            if (m_File != null)
            {
                return;
            }
            if (!File.Exists(m_Path))
            {
                m_File = NewFile();
                return;
            }
            LedgerFile loaded;
            try
            {
                loaded = JsonConvert.DeserializeObject<LedgerFile>(
                    File.ReadAllText(m_Path));
            }
            catch (Exception e) when (e is IOException || e is JsonException)
            {
                throw new IOException(
                    "Google Drive sync ledger could not be read and was retained.", e);
            }
            if (loaded == null ||
                loaded.Version != kVersion ||
                loaded.AccountNamespace != m_AccountNamespace ||
                loaded.DriveRootNamespace != m_DriveRootNamespace ||
                loaded.StorageRootNamespace != m_StorageRootNamespace ||
                loaded.Entries == null)
            {
                throw new IOException(
                    "Google Drive sync ledger has an unknown or mismatched format and was retained.");
            }
            m_File = loaded;
        }

        private LedgerFile NewFile()
        {
            return new LedgerFile
            {
                Version = kVersion,
                AccountNamespace = m_AccountNamespace,
                DriveRootNamespace = m_DriveRootNamespace,
                StorageRootNamespace = m_StorageRootNamespace,
            };
        }

        private void WriteAtomically()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(m_Path));
            string temporary = $"{m_Path}.obtmp-{Guid.NewGuid():N}";
            try
            {
                File.WriteAllText(
                    temporary,
                    JsonConvert.SerializeObject(m_File, Formatting.Indented));
                if (File.Exists(m_Path))
                {
                    File.Replace(temporary, m_Path, null);
                }
                else
                {
                    File.Move(temporary, m_Path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static bool TimesEqual(long? left, long? right)
        {
            if (!left.HasValue || !right.HasValue)
            {
                return !left.HasValue && !right.HasValue;
            }
            return Math.Abs(left.Value - right.Value) <= TimeSpan.TicksPerSecond * 3;
        }

        private static string Normalize(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Drive sync path is empty.", nameof(relativePath));
            }
            string normalized = relativePath.Replace('\\', '/').Trim('/');
            if (normalized.Split('/').Any(
                    segment => string.IsNullOrEmpty(segment) ||
                        segment == "." ||
                        segment == ".."))
            {
                throw new ArgumentException(
                    "Drive sync path escapes its storage area.", nameof(relativePath));
            }
            return normalized;
        }

        private static string Namespace(string identity)
        {
            return SafTransactionJournal.GetRootNamespaceId(identity ?? "");
        }
    }
}
