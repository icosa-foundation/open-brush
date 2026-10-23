// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.IO;
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

    // Activated with the runtime cutover, so intermediate commits keep the old reader working.
    internal static void AddToBuild(BuildPlayerContext context)
    {
        string manifest = Prepare(SourceDirectory, "Library/BundledSketchBuild");
        context.AddAdditionalPathToStreamingAssets(Path.GetFullPath(manifest),
            $"{BundledSketchManifest.AssetDirectory}/{BundledSketchManifest.FileName}");
        foreach (string path in Directory.GetFiles(SourceDirectory, "*.tilt.bytes"))
        {
            context.AddAdditionalPathToStreamingAssets(Path.GetFullPath(path),
                $"{BundledSketchManifest.AssetDirectory}/{Path.GetFileNameWithoutExtension(path)}");
        }
    }

    internal static string Prepare(string sourceDirectory, string outputDirectory)
    {
        var entries = new List<BundledSketchEntry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.GetFiles(sourceDirectory, "*.tilt.bytes")
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!names.Add(name))
                throw new BuildFailedException($"Duplicate bundled sketch name: {name}");

            var tilt = new TiltFile(path);
            using (Stream metadata = tilt.GetReadStream(TiltFile.FN_METADATA))
            using (Stream thumbnail = tilt.GetReadStream(TiltFile.FN_THUMBNAIL))
            {
                if (metadata == null || thumbnail == null)
                    throw new BuildFailedException($"Bundled sketch is missing metadata or thumbnail: {path}");

                using (var reader = new StreamReader(metadata, Encoding.UTF8))
                using (var image = new MemoryStream())
                {
                    thumbnail.CopyTo(image);
                    entries.Add(new BundledSketchEntry
                    {
                        FileName = name,
                        Metadata = reader.ReadToEnd(),
                        Thumbnail = image.ToArray()
                    });
                }
            }
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
