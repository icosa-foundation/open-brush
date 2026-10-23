// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;

namespace TiltBrush
{
    // Filenames only: this packaging stage preserves the existing sketchbook seeding behavior.
    [Serializable]
    public sealed class BundledSketchManifest
    {
        public const string AssetDirectory = "BundledSketches";
        public const string FileName = "catalog.json";
        public const string PayloadSuffix = ".gz";
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string[] Sketches;
    }
}
