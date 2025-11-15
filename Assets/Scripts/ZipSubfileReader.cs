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

namespace TiltBrush
{
    /// <summary>
    /// Wrappers to maintain backward compatibility with existing code.
    /// The actual implementation is in OpenBrush.TiltFile package.
    /// </summary>
    public class ZipSubfileReader_SharpZipLib : OpenBrush.TiltFile.ZipSubfileReader_SharpZipLib
    {
        public ZipSubfileReader_SharpZipLib(string zipPath, string subPath) : base(zipPath, subPath)
        {
        }
    }

    public class ZipSubfileReader_DotNetZip : OpenBrush.TiltFile.ZipSubfileReader_DotNetZip
    {
        public ZipSubfileReader_DotNetZip(string zipPath, string subPath) : base(zipPath, subPath)
        {
        }
    }
}
