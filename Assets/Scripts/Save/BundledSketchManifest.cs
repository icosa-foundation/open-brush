// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using UnityEngine.Scripting;

namespace TiltBrush
{
    // Browsing uses this small index; sketch binaries are opened only when selected.
    [Serializable]
    [Preserve]
    public sealed class BundledSketchManifest
    {
        public const string AssetDirectory = "BundledSketches";
        public const string FileName = "catalog.json";
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public BundledSketchEntry[] Sketches;
    }

    [Serializable]
    [Preserve]
    public sealed class BundledSketchEntry
    {
        public string FileName;
        public string Metadata;
        public byte[] Thumbnail;
    }
}
