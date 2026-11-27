// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TiltBrush.DocumentImport;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{
    internal sealed class NativeShapesXrAssets : IDisposable
    {
        private readonly string m_Parent;
        private readonly string m_Root;
        private readonly NativeProjectSource m_Source;
        private readonly Dictionary<Guid, (ShapesXrResourceData Data, ImportResource Resource)> m_Resources =
            new Dictionary<Guid, (ShapesXrResourceData, ImportResource)>();
        public NativeAssetWorkspace Workspace { get; }

        public NativeShapesXrAssets()
        {
            m_Parent = Path.GetFullPath(Path.Combine(Application.temporaryCachePath, "Native Imports", "ShapesXR"));
            m_Root = Path.Combine(m_Parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Root);
            m_Source = new NativeProjectSource(m_Root);
            Workspace = new NativeAssetWorkspace(m_Source, m_Parent);
        }

        public async Task<(ShapesXrResourceData Data, ImportResource Resource)> Get(Guid id)
        {
            if (m_Resources.TryGetValue(id, out var existing)) { return existing; }
            ShapesXrResourceData data;
            using (var request = UnityWebRequest.Get($"https://api.shapes.app/resources/{id}"))
            {
                request.timeout = 30;
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new IOException($"ShapesXR resource {id} request failed: {request.error}");
                }
                data = new ShapesXrResourceData(JObject.Parse(request.downloadHandler.text), id);
            }
            string root = Path.Combine(m_Root, id.ToString("N"));
            Directory.CreateDirectory(root);
            foreach (var file in data.Files)
            {
                // Filenames have been validated as relative paths by the data reader.
                string destination = Path.GetFullPath(Path.Combine(root, file.Name));
                if (!destination.StartsWith($"{root}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("ShapesXR download escapes its resource directory");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using (var request = UnityWebRequest.Get(file.Url.AbsoluteUri))
                {
                    request.timeout = 30;
                    request.downloadHandler = new DownloadHandlerFile(destination) { removeFileOnAbort = true };
                    await request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        throw new IOException($"ShapesXR resource {id} file request failed: {request.error}");
                    }
                }
                if (new FileInfo(destination).Length != file.Size) { throw new InvalidDataException($"Incomplete ShapesXR resource {id}"); }
            }
            var resource = ImportResource.FromFile(Path.Combine(root, data.MainFile.Name), root);
            var result = (data, resource);
            m_Resources.Add(id, result);
            return result;
        }

        public void Dispose()
        {
            Workspace.Dispose();
            m_Source.Dispose();
            // Only this instance's freshly allocated temporary child is removed. Persistent
            // image/model library copies are owned by the ordinary Open Brush caches.
            string target = Path.GetFullPath(m_Root);
            if (!target.StartsWith($"{m_Parent}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Invalid ShapesXR temporary cleanup target");
            }
            if (Directory.Exists(target)) { Directory.Delete(target, true); }
        }
    }
}
