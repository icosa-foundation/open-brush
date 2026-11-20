// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using TiltBrush.DocumentImport;

namespace TiltBrush
{
    internal sealed class NativeImageCache
    {
        private readonly Dictionary<string, ReferenceImage> m_Images = new Dictionary<string, ReferenceImage>(StringComparer.Ordinal);
        private readonly string m_Directory = $"Native Imports/{Guid.NewGuid():N}";

        public ReferenceImage Get(MaquettePrimitiveData primitive, MaquetteAssetResolver assets)
        {
            if (primitive.Type == "ImageFromWeb" && ApiMethods.IsHttpLocation(primitive.Path))
            {
                if (m_Images.TryGetValue(primitive.Path, out var existing)) { return existing; }
                string relative = ApiMethods.CacheNativeImageUrl(primitive.Path);
                var downloaded = Load(relative);
                m_Images.Add(primitive.Path, downloaded);
                return downloaded;
            }
            var resource = assets.Resolve(primitive.Path) ?? throw new FileNotFoundException($"Missing Maquette image: {primitive.Path}");
            return Get(resource);
        }

        public ReferenceImage Get(ImportResource resource)
        {
            if (resource == null) { throw new FileNotFoundException("Missing native image resource"); }
            if (m_Images.TryGetValue(resource.Origin, out var image)) { return image; }
            string filename = $"{Guid.NewGuid():N}{Path.GetExtension(resource.Name)}";
            string relativePath = $"{m_Directory}/{filename}";
            string path = Path.Combine(App.ReferenceImagePath(), relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var input = resource.OpenRead())
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) { input.CopyTo(output); }
            image = Load(relativePath);
            m_Images.Add(resource.Origin, image);
            return image;
        }

        private static ReferenceImage Load(string relative)
        {
            var image = new ReferenceImage(Path.Combine(App.ReferenceImagePath(), relative));
            image.SynchronousLoad();
            if (!image.Valid) { throw new InvalidDataException($"Image could not be loaded: {relative}"); }
            return image;
        }
    }
}
