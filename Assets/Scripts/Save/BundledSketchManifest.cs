// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;

namespace TiltBrush
{
    // The small index can be read without opening any of the packaged sketch binaries.
    [Serializable]
    public sealed class BundledSketchManifest
    {
        public const string AssetDirectory = "BundledSketches";
        public const string FileName = "catalog.json";
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public BundledSketchEntry[] Sketches;
    }

    [Serializable]
    public sealed class BundledSketchEntry
    {
        public string FileName;
        public string Metadata;
        public byte[] Thumbnail;
    }
}
