// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
            bool compressed = name.EndsWith(".tilt", StringComparison.Ordinal);
            string path = $"{BundledSketchManifest.AssetDirectory}/{name}";
            if (compressed) path = $"{path}{BundledSketchManifest.PayloadSuffix}";
            byte[] bytes;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridge = new AndroidJavaClass("foundation.icosa.openbrush.DirectStorage"))
                    bytes = bridge.CallStatic<byte[]>("readAsset", path);
            }
            catch (AndroidJavaException e)
            {
                throw new IOException($"Could not read packaged sketch asset {path}.", e);
            }
#else
            bytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, path));
#endif
            return compressed ? DecodePayload(bytes) : bytes;
        }

        internal static byte[] DecodePayload(byte[] bytes)
        {
            using (var input = new MemoryStream(bytes))
            using (var decoded = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                decoded.CopyTo(output);
                return output.ToArray();
            }
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0 ||
                name == "." || name == ".." || Path.IsPathRooted(name))
                throw new InvalidDataException($"Invalid bundled sketch filename: {name}");
        }
    }
}
