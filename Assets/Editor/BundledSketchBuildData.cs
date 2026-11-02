// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using TiltBrush;
using UnityEditor;
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
        bool android = context.BuildPlayerOptions.target == BuildTarget.Android;
        string catalog = android ? Compress(manifest, $"{manifest}{PayloadSuffix}") : manifest;
        context.AddAdditionalPathToStreamingAssets(Path.GetFullPath(catalog),
            $"{BundledSketchManifest.AssetDirectory}/{Path.GetFileName(catalog)}");
        foreach (string path in Directory.GetFiles(SourceDirectory, "*.tilt.bytes"))
        {
            string payload = android ? PreparePayload(path, "Library/BundledSketchBuild/Payloads") : path;
            string name = android ? Path.GetFileName(payload) : Path.GetFileNameWithoutExtension(path);
            context.AddAdditionalPathToStreamingAssets(Path.GetFullPath(payload),
                $"{BundledSketchManifest.AssetDirectory}/{name}");
        }
    }

    // Android imports .gz inputs as compressed APK entries under their original names.
    // AssetManager returns the original bytes; compression stays entirely at build time.
    internal static string PreparePayload(string sourcePath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string output = Path.Combine(outputDirectory,
            $"{Path.GetFileNameWithoutExtension(sourcePath)}{PayloadSuffix}");
        return Compress(sourcePath, output);
    }

    private static string Compress(string source, string output)
    {
        using (Stream input = File.OpenRead(source))
        using (Stream destination = File.Create(output))
        using (var compressed = new GZipStream(destination, CompressionLevel.Optimal))
            input.CopyTo(compressed);
        return output;
    }

    internal static string Prepare(string sourceDirectory, string outputDirectory)
    {
        var manifest = BundledSketchAssets.CreateManifest(sourceDirectory);
        Directory.CreateDirectory(outputDirectory);
        string manifestPath = Path.Combine(outputDirectory, BundledSketchManifest.FileName);
        File.WriteAllText(manifestPath,
            JsonConvert.SerializeObject(manifest),
            new UTF8Encoding(false));
        return manifestPath;
    }
}
