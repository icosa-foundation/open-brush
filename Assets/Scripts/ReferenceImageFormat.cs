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

namespace TiltBrush
{
    public static class ReferenceImageFormat
    {
        private static readonly HashSet<string> kSupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".svg", ".hdr", ".exr"
            };

        public static bool IsSupportedExtension(string extension)
        {
            return extension != null && kSupportedExtensions.Contains(extension);
        }

        public static bool IsSupportedFile(string path)
        {
            return !string.IsNullOrEmpty(path) && IsSupportedExtension(Path.GetExtension(path));
        }

        public static bool IsHighDynamicRangeFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".hdr", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".exr", StringComparison.OrdinalIgnoreCase);
        }
    }
}
