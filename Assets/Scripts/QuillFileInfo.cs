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
using System.IO;

namespace TiltBrush
{
    public enum QuillSourceType
    {
        Imm,
        Quill
    }

    public class QuillFileInfo
    {
        public string FullPath { get; }
        public string DisplayName { get; }
        public long FileSizeBytes { get; }
        public DateTime LastWriteTimeUtc { get; }
        public QuillSourceType SourceType { get; }

        public QuillFileInfo(string fullPath, string displayName, long fileSizeBytes,
            DateTime lastWriteTimeUtc, QuillSourceType sourceType)
        {
            FullPath = fullPath;
            DisplayName = displayName;
            FileSizeBytes = Math.Max(0, fileSizeBytes);
            LastWriteTimeUtc = lastWriteTimeUtc;
            SourceType = sourceType;
        }

        public string SourceLabel => SourceType == QuillSourceType.Imm ? "IMM" : "Quill";

        public string DescriptionLabel => $"{SourceLabel}  {FormatFileSize(FileSizeBytes)}";

        public string DetailLabel => $"Modified {LastWriteTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            string[] units = { "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = -1;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.#} {units[unitIndex]}";
        }

        public static QuillFileInfo FromImmFile(FileInfo file)
        {
            return new QuillFileInfo(file.FullName, Path.GetFileNameWithoutExtension(file.Name),
                file.Length, file.LastWriteTimeUtc, QuillSourceType.Imm);
        }

        public static QuillFileInfo FromQuillDirectory(DirectoryInfo directory)
        {
            long estimatedBytes = 0;
            try
            {
                foreach (var file in directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                {
                    estimatedBytes += file.Length;
                }
            }
            catch
            {
                estimatedBytes = 0;
            }

            return new QuillFileInfo(directory.FullName, directory.Name, estimatedBytes,
                directory.LastWriteTimeUtc, QuillSourceType.Quill);
        }
    }
}
