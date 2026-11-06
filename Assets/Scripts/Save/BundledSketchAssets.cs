// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
#if USE_DOTNETZIP
using ZipSubfileReader = TiltBrush.ZipSubfileReader_DotNetZip;
#else
using ZipSubfileReader = TiltBrush.ZipSubfileReader_SharpZipLib;
#endif

namespace TiltBrush
{
    public static class BundledSketchAssets
    {
        public static BundledSketchManifest ReadManifest()
        {
#if UNITY_EDITOR
            return CreateManifest(Path.Combine(Application.dataPath, "DefaultBundledSketches"));
#else
            return ParseManifest(Read(BundledSketchManifest.FileName));
#endif
        }

        internal static BundledSketchManifest ParseManifest(byte[] data)
        {
            var manifest = JsonConvert.DeserializeObject<BundledSketchManifest>(
                Encoding.UTF8.GetString(data));
            if (manifest == null || manifest.Version != BundledSketchManifest.CurrentVersion ||
                manifest.Sketches == null || manifest.Sketches.Length == 0)
                throw new InvalidDataException("Missing or unsupported bundled sketch catalog.");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.Sketches)
            {
                if (entry == null) throw new InvalidDataException("Null bundled sketch entry.");
                string name = entry.FileName;
                ValidateName(name);
                if (!name.EndsWith(".tilt", StringComparison.Ordinal) || !names.Add(name) ||
                    string.IsNullOrWhiteSpace(entry.Metadata) || entry.Thumbnail == null || entry.Thumbnail.Length == 0)
                    throw new InvalidDataException($"Invalid or duplicate bundled sketch: {name}");
            }
            return manifest;
        }

        public static byte[] Read(string name)
        {
            ValidateName(name);
            string path = $"{BundledSketchManifest.AssetDirectory}/{name}";
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridge = new AndroidJavaClass("foundation.icosa.openbrush.DirectStorage"))
                    return bridge.CallStatic<byte[]>("readAsset", path);
            }
            catch (AndroidJavaException e)
            {
                throw new IOException($"Could not read packaged sketch asset {path}.", e);
            }
#else
#if UNITY_EDITOR
            return File.ReadAllBytes(Path.Combine(Application.dataPath, "DefaultBundledSketches", $"{name}.bytes"));
#else
            return File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, path));
#endif
#endif
        }

        internal static IEnumerable<string> GetContentsAt(BundledSketchEntry entry, string path)
        {
            using (var input = new MemoryStream(Read(entry.FileName), false))
                return GetContentsAt(input, path);
        }

        internal static IEnumerable<string> GetContentsAt(Stream input, string path)
        {
            string prefix = path.Replace('\\', '/').Trim('/');
            if (prefix.Length > 0) prefix += "/";
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#if USE_DOTNETZIP
            using (var archive = Ionic.Zip.ZipFile.Read(input))
#else
            using (var archive = new Unity.SharpZipLib.Zip.ZipFile(input))
#endif
            {
                foreach (var entry in archive)
                {
#if USE_DOTNETZIP
                    string name = entry.FileName.Replace('\\', '/');
#else
                    string name = ((Unity.SharpZipLib.Zip.ZipEntry)entry).Name.Replace('\\', '/');
#endif
                    if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string remainder = name.Substring(prefix.Length);
                    int separator = remainder.IndexOf('/');
                    if (separator > 0) directories.Add(remainder.Substring(0, separator));
                }
            }
            // Materialize before disposing the archive and its input.
            return directories;
        }

        internal static Stream OpenSubfile(BundledSketchEntry entry, string name)
        {
            if (name == TiltFile.FN_METADATA)
                return new MemoryStream(Encoding.UTF8.GetBytes(entry.Metadata), false);
            if (name == TiltFile.FN_THUMBNAIL)
                return new MemoryStream(entry.Thumbnail, false);

            // Only loading sketch data needs the binary. The stream owns its temporary
            // buffer; no on-disk or persistent in-memory copy is kept by the catalog.
            var input = new MemoryStream(Read(entry.FileName), false);
            try { return new ZipSubfileReader(input, name); }
            catch (FileNotFoundException) { input.Dispose(); return null; }
            catch { input.Dispose(); throw; }
        }

#if UNITY_EDITOR
        // Shared by the build hook and Viewer Play mode; no generated Assets are needed.
        public static BundledSketchManifest CreateManifest(string directory)
        {
            var entries = new List<BundledSketchEntry>();
            foreach (string path in Directory.GetFiles(directory, "*.tilt.bytes").OrderBy(path => path, StringComparer.Ordinal))
            {
                var tilt = new TiltFile(path);
                using (Stream metadata = tilt.GetReadStream(TiltFile.FN_METADATA))
                using (Stream thumbnail = tilt.GetReadStream(TiltFile.FN_THUMBNAIL))
                {
                    if (metadata == null || thumbnail == null)
                        throw new InvalidDataException($"Missing metadata or thumbnail: {path}");
                    using (var reader = new StreamReader(metadata, Encoding.UTF8))
                    using (var image = new MemoryStream())
                    {
                        thumbnail.CopyTo(image);
                        entries.Add(new BundledSketchEntry
                        {
                            FileName = Path.GetFileNameWithoutExtension(path),
                            Metadata = reader.ReadToEnd(),
                            Thumbnail = image.ToArray()
                        });
                    }
                }
            }
            return ParseManifest(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
                new BundledSketchManifest { Sketches = entries.ToArray() })));
        }
#endif

        private static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0 ||
                name == "." || name == ".." || Path.IsPathRooted(name))
                throw new InvalidDataException($"Invalid bundled sketch filename: {name}");
        }
    }
}
