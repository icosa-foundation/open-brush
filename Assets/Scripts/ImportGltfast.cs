// Copyright 2024 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiltBrushToolkit;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Loader;

namespace TiltBrush
{
    internal class NewGltfImporter
    {

        public sealed class ImportState : IDisposable
        {
            // Change-of-basis matrix
            internal readonly Matrix4x4 gltfFromUnity;
            // Change-of-basis matrix
            internal readonly Matrix4x4 unityFromGltf;
            // The parsed gltf; filled in by BeginImport
            internal GameObject root;

            internal ImportState(AxisConvention axes)
            {
                gltfFromUnity = AxisConvention.GetFromUnity(axes);
                unityFromGltf = AxisConvention.GetToUnity(axes);
            }

            public void Dispose()
            {
                // if (root != null) { root.Dispose(); }
            }
        }

        public static Task StartSyncImport(string localPath, string assetLocation, Model model, List<string> warnings)
        {
            return _ImportUsingUnityGltf(localPath, assetLocation, model, warnings);
        }

        // Shared AsyncCoroutineHelper used to time-slice UnityGLTF imports across frames.
        // Lives on a hidden, persistent GameObject so its per-frame timeout coroutine keeps running.
        private static AsyncCoroutineHelper sm_AsyncCoroutineHelper;
        private static AsyncCoroutineHelper GetAsyncCoroutineHelper()
        {
            if (sm_AsyncCoroutineHelper == null)
            {
                var go = new GameObject("UnityGltfAsyncCoroutineHelper")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                UnityEngine.Object.DontDestroyOnLoad(go);
                sm_AsyncCoroutineHelper = go.AddComponent<AsyncCoroutineHelper>();
            }
            return sm_AsyncCoroutineHelper;
        }

        private static GameObject _ImportUsingLegacyGltf(
            string localPath, string assetLocation, IUriLoader loader = null)
        {
            loader = loader ?? new TiltBrushUriLoader(
                localPath, assetLocation, loadImages: false);
            var materialCollector = new ImportMaterialCollector(assetLocation, uniqueSeed: localPath);
            var importOptions = new GltfImportOptions
            {
                rescalingMode = GltfImportOptions.RescalingMode.CONVERT,
                scaleFactor = App.METERS_TO_UNITS,
                recenter = false
            };
            ImportGltf.GltfImportResult result = ImportGltf.Import(localPath, loader, materialCollector, importOptions);
            return result.root;
        }

        /// The legacy importer insists on opening the primary glTF by filename. Materialize that
        /// one file, then copy sidecars lazily as its ordinary URI loader asks for them. This is
        /// only the exception fallback; the normal UnityGLTF path remains stream-backed.
        private sealed class SafLegacyGltfLoader : IUriLoader, IDisposable
        {
            private readonly IUserStorageBackend m_Backend;
            private readonly StorageArea m_Area;
            private readonly string m_Directory;
            private readonly string m_TemporaryAreaRoot;
            private readonly TiltBrushUriLoader m_LocalLoader;

            public string PrimaryPath { get; }
            public string AssetLocation => Path.GetDirectoryName(PrimaryPath);

            public SafLegacyGltfLoader(
                IUserStorageBackend backend, StorageArea area,
                string directory, string fileName)
            {
                m_Backend = backend;
                m_Area = area;
                m_Directory = directory;
                m_TemporaryAreaRoot = Path.Combine(
                    Application.temporaryCachePath,
                    "OpenBrushSafLegacyGltf",
                    Guid.NewGuid().ToString("N"));
                try
                {
                    if (!SafGltfDataLoader.TryResolveAreaRelativePath(
                            directory, fileName, out string primaryRelativePath))
                    {
                        throw new IOException($"Invalid SAF glTF path: {fileName}");
                    }
                    PrimaryPath = Materialize(primaryRelativePath);
                    m_LocalLoader = new TiltBrushUriLoader(
                        PrimaryPath, AssetLocation, loadImages: false);
                }
                catch
                {
                    DeleteTemporaryFiles();
                    throw;
                }
            }

            public IBufferReader Load(string uri)
            {
                if (uri != null)
                {
                    string sanitized = IcosaRawAsset.GetPolySanitizedFilePath(uri);
                    if (!SafGltfDataLoader.TryResolveAreaRelativePath(
                            m_Directory, sanitized, out string relativePath))
                    {
                        throw new IOException($"glTF reference escapes shared storage: {uri}");
                    }
                    Materialize(relativePath);
                }
                return m_LocalLoader.Load(uri);
            }

            public bool CanLoadImages() => false;
            public TiltBrushToolkit.RawImage LoadAsImage(string uri) =>
                throw new NotSupportedException();

#if UNITY_EDITOR
            public Texture2D LoadAsAsset(string uri) => m_LocalLoader.LoadAsAsset(uri);
#endif

            public void Dispose()
            {
                DeleteTemporaryFiles();
            }

            private void DeleteTemporaryFiles()
            {
                try
                {
                    if (Directory.Exists(m_TemporaryAreaRoot))
                    {
                        Directory.Delete(m_TemporaryAreaRoot, recursive: true);
                    }
                }
                catch (Exception e) when (
                    e is IOException || e is UnauthorizedAccessException)
                {
                    Debug.LogWarning(
                        $"Could not remove legacy glTF staging: {e.Message}");
                }
            }

            private string Materialize(string areaRelativePath)
            {
                string destination = Path.GetFullPath(Path.Combine(
                    m_TemporaryAreaRoot,
                    areaRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                string rootPrefix = Path.GetFullPath(m_TemporaryAreaRoot).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        $"glTF reference escapes temporary storage: {areaRelativePath}");
                }
                if (File.Exists(destination))
                {
                    return destination;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                using (Stream input = m_Backend.OpenRead(
                    m_Area, areaRelativePath, requireSeekable: false,
                    System.Threading.CancellationToken.None))
                using (var output = new FileStream(
                    destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                }
                return destination;
            }
        }

        /// A model in the shared media library can be read without being copied out of it. Returns
        /// false for every other source - Icosa downloads, bundled content, non-SAF platforms -
        /// which keep the ordinary filesystem route.
        private static bool TryGetStorageModelLocation(
            Model model, out StorageArea area, out string directory, out string fileName)
        {
            area = StorageArea.MediaLibraryModels;
            directory = null;
            fileName = null;
            if (model == null ||
                UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                return false;
            }
            string relativePath = model.RelativePath;
            if (string.IsNullOrEmpty(relativePath))
            {
                return false;
            }
            string normalized = relativePath.Replace('\\', '/').Trim('/');
            int separator = normalized.LastIndexOf('/');
            directory = separator < 0 ? string.Empty : normalized.Substring(0, separator);
            fileName = separator < 0 ? normalized : normalized.Substring(separator + 1);
            return !string.IsNullOrEmpty(fileName);
        }

        private static async Task _ImportUsingUnityGltf(
            string localPath,
            string assetLocation,
            Model model,
            List<string> warnings)
        {
            try
            {
                ImportOptions options = new ImportOptions();
                // TODO - should we import disabled to help round-tripping?
                options.CameraImport = CameraImportOption.None;
                options.AnimationMethod = AnimationMethod.Legacy;
                // Lets GLTFSceneImporter time-slice the load across frames (it yields once the
                // per-frame budget is exceeded) instead of doing it all in one blocking call.
                options.AsyncCoroutineHelper = GetAsyncCoroutineHelper();

                // Load from disk via FileLoader rather than letting the importer fall back to the
                // auto-selected UnityWebRequestLoader. FileLoader resolves external references
                // (textures, .bin buffers) with plain File IO (File.OpenRead / Path.Combine) plus an
                // Uri.UnescapeDataString fallback for %20-escaped names, so it avoids the
                // UnityWebRequest "new Uri(absolutePath)" path that mishandled spaces and bare POSIX
                // (Android) paths - which is why we previously had to hand-build a file:/// URI.
                // See https://github.com/KhronosGroup/UnityGLTF/issues/805. FileLoader also implements
                // IDataLoader2, letting the importer read the glTF JSON off the main thread when
                // IsMultithreaded is set.
                var fullPath = string.IsNullOrEmpty(localPath)
                    ? null
                    : Uri.UnescapeDataString(localPath).Replace("\\", "/");
                // The importer's file name must be RELATIVE to the FileLoader root (the directory),
                // not absolute. Passing the full path makes FileLoader concatenate root + absolute
                // path into a doubled, invalid path (e.g. ".../id/C:/.../id/file.gltf2"), which fails
                // File.Exists and spams "Invalid AssetDatabase path" before falling through.
                // On a backend with no filesystem, resolve the glTF and its external references
                // straight out of storage. UnityGLTF's loader contract is stream-based, so this
                // needs no materialized copy - only a different IDataLoader.
                string gltfFileName;
                SafGltfDataLoader safDataLoader = null;
                if (TryGetStorageModelLocation(model, out StorageArea area, out string directory,
                        out string fileName))
                {
                    safDataLoader = new SafGltfDataLoader(area, directory);
                    options.DataLoader = safDataLoader;
                    gltfFileName = fileName;
                }
                else
                {
                    options.DataLoader = new FileLoader(Path.GetDirectoryName(fullPath));
                    gltfFileName = Path.GetFileName(fullPath);
                }
                GLTFSceneImporter gltf = new GLTFSceneImporter(gltfFileName, options);

                if (options.ImportContext.TryGetPlugin<UnityGLTF.Plugins.OpenBrushAudioImportContext>(out var audioPlugin))
                {
                    audioPlugin.GltfDirectory = string.IsNullOrEmpty(localPath)
                        ? null
                        : Path.GetDirectoryName(localPath);
                    audioPlugin.OpenSidecar = safDataLoader == null
                        ? null
                        : safDataLoader.LoadStream;
                }

                // Device builds only: GLTFSceneImporter hard-forces this false in the editor (to
                // avoid a historical editor freeze), so editor imports stay single-threaded regardless.
                // In a player build it moves mesh/buffer construction off the main thread, shrinking
                // the per-frame chunks. Validate on-device before relying on it.
                gltf.IsMultithreaded = true;
                // Await rather than AsyncHelpers.RunSync: blocking the main thread defeats the
                // time-slicing above and (in the editor) deadlocks the UnityWebRequest file read,
                // because the player loop can't tick while blocked.
                await gltf.LoadSceneAsync();
                GameObject go = gltf.CreatedObject;

                var clips = gltf.CreatedAnimationClips;
                if (clips != null && clips.Length > 0)
                {
                    var player = go.AddComponent<PlayGltfAnimationClip>();
                    // TODO - allow users to control autoplay
                    player.PlayAnimation(clips);
                }

                model.CalcBoundsGltf(go);
                if (!model.EndCreatePrefab(go, warnings))
                {
                    return;
                }
                var materialCollector = new ImportMaterialCollector(assetLocation, uniqueSeed: localPath);
                // Gather all the unity materials created by UnityGltf
                var mrs = go.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var mr in mrs)
                {
                    foreach (var material in mr.materials)
                    {
                        materialCollector.Add(material);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import using UnityGltf. Falling back to legacy import.\nUnityGltf Exception: {e}");
                // Fall back to the older import code
                GameObject go;
                if (TryGetStorageModelLocation(
                        model, out StorageArea area, out string directory, out string fileName))
                {
                    using (var loader = new SafLegacyGltfLoader(
                        UserStorage.Backend, area, directory, fileName))
                    {
                        go = _ImportUsingLegacyGltf(
                            loader.PrimaryPath, loader.AssetLocation, loader);
                    }
                }
                else
                {
                    go = _ImportUsingLegacyGltf(localPath, assetLocation);
                }
                model.CalcBoundsGltf(go);
                model.EndCreatePrefab(go, warnings);
            }
        }
    }
}
