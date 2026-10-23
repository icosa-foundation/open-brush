// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TiltBrush;
using UnityEditor.Build;

// Prepare generated data outside Assets. The player build supplies these files directly
// to StreamingAssets; the source sketch assets never need to move or be reimported.
internal static class BundledSketchBuildData
{
    internal const string SourceDirectory = "Assets/DefaultBundledSketches";
    private const string PayloadSuffix = ".gz";

    internal static void AddToBuild(BuildPlayerContext context)
    {
        string manifest = Prepare(SourceDirectory, "Library/BundledSketchBuild");
        context.AddAdditionalPathToStreamingAssets(Path.GetFullPath(manifest),
            $"{BundledSketchManifest.AssetDirectory}/{BundledSketchManifest.FileName}");
        foreach (string path in Directory.GetFiles(SourceDirectory, "*.tilt.bytes"))
        {
            string payload = PreparePayload(path, "Library/BundledSketchBuild/Payloads");
            context.AddAdditionalPathToStreamingAssets(Path.GetFullPath(payload),
                $"{BundledSketchManifest.AssetDirectory}/{Path.GetFileName(payload)}");
        }
    }

    // Android imports .gz inputs as compressed APK entries under their original names.
    // AssetManager returns the original bytes; compression stays entirely at build time.
    internal static string PreparePayload(string sourcePath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string output = Path.Combine(outputDirectory,
            $"{Path.GetFileNameWithoutExtension(sourcePath)}{PayloadSuffix}");
        using (Stream input = File.OpenRead(sourcePath))
        using (Stream destination = File.Create(output))
        using (var compressed = new GZipStream(destination, CompressionLevel.Optimal))
            input.CopyTo(compressed);
        return output;
    }

    internal static string Prepare(string sourceDirectory, string outputDirectory)
    {
        var entries = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.GetFiles(sourceDirectory, "*.tilt.bytes")
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!names.Add(name))
                throw new BuildFailedException($"Duplicate bundled sketch name: {name}");

            if (!new TiltFile(path).IsHeaderValid())
                throw new BuildFailedException($"Invalid bundled sketch: {path}");
            entries.Add(name);
        }
        if (entries.Count == 0)
            throw new BuildFailedException($"No bundled sketches found in {sourceDirectory}");

        Directory.CreateDirectory(outputDirectory);
        string manifestPath = Path.Combine(outputDirectory, BundledSketchManifest.FileName);
        File.WriteAllText(manifestPath,
            JsonConvert.SerializeObject(new BundledSketchManifest { Sketches = entries.ToArray() }),
            new UTF8Encoding(false));
        return manifestPath;
    }
}
