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
using System.Linq;
using System.Text;
using System.Threading;

namespace TiltBrush
{
    internal static class SharedUserConfig
    {
        // Read shared configuration in place. Never overwrite either the user's shared file
        // or the private fallback when permission, provider availability, or roots change.
        internal static string ReadText(
            IUserStorageBackend backend, string localPath, Action<Exception> onSharedReadError)
        {
            try
            {
                if (backend.Kind == StorageBackendKind.StorageAccessFramework && backend.IsReady)
                {
                    string rootIdentity = backend.RootIdentity;
                    StorageDirectoryResult listing = backend.List(
                        StorageArea.UserRoot, "", CancellationToken.None);
                    if (!listing.Success)
                    {
                        throw new IOException($"{listing.Code}: {listing.Error}");
                    }
                    StorageDocument config = listing.Documents.FirstOrDefault(document =>
                        !document.IsDirectory && document.DisplayName == Path.GetFileName(localPath));
                    if (config != null)
                    {
                        string text;
                        using (Stream stream = backend.OpenRead(
                            config.DocumentId, requireSeekable: false, CancellationToken.None))
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            text = reader.ReadToEnd();
                        }
                        if (!string.Equals(rootIdentity, backend.RootIdentity, StringComparison.Ordinal))
                        {
                            throw new IOException("The selected folder changed while reading configuration.");
                        }
                        return text;
                    }
                }
            }
            catch (Exception e)
            {
                onSharedReadError?.Invoke(e);
            }
            return File.Exists(localPath) ? File.ReadAllText(localPath, Encoding.UTF8) : null;
        }
    }
}
