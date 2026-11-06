// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using TiltBrush.DocumentImport;

namespace TiltBrush
{
    internal sealed class NativeAudioCache
    {
        private readonly string m_Directory = $"Native Imports/{Guid.NewGuid():N}";
        private readonly Dictionary<string, SoundClip> m_Clips = new Dictionary<string, SoundClip>(StringComparer.Ordinal);

        public SoundClip Get(ImportResource resource)
        {
            if (resource == null) { throw new FileNotFoundException("Missing native audio resource"); }
            if (m_Clips.TryGetValue(resource.Origin, out var existing)) { return existing; }
            string directory = Path.Combine(App.SoundClipLibraryPath(), m_Directory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Path.GetFileName(resource.Name));
            using (var input = resource.OpenRead())
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) { input.CopyTo(output); }
            var clip = new SoundClip(path);
            m_Clips.Add(resource.Origin, clip);
            SoundClipCatalog.Instance.ForceCatalogScan();
            return clip;
        }
    }
}
