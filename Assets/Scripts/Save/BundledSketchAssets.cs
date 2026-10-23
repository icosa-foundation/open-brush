// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    public static class BundledSketchAssets
    {
        public static BundledSketchManifest ReadManifest()
        {
            return ParseManifest(Read(BundledSketchManifest.FileName));
        }

        internal static BundledSketchManifest ParseManifest(byte[] data)
        {
            var manifest = JsonConvert.DeserializeObject<BundledSketchManifest>(
                Encoding.UTF8.GetString(data));
            if (manifest == null || manifest.Version != BundledSketchManifest.CurrentVersion ||
                manifest.Sketches == null || manifest.Sketches.Length == 0)
                throw new InvalidDataException("Missing or unsupported bundled sketch catalog.");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in manifest.Sketches)
            {
                ValidateName(name);
                if (!name.EndsWith(".tilt", StringComparison.Ordinal) || !names.Add(name))
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
            return File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, path));
#endif
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0 ||
                name == "." || name == ".." || Path.IsPathRooted(name))
                throw new InvalidDataException($"Invalid bundled sketch filename: {name}");
        }
    }
}
