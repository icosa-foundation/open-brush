// Copyright 2026 The Open Brush Authors
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
using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        public sealed class RuntimeVoxSavePayload
        {
            public RuntimeVoxDocument Document;
            public RuntimeVoxState State;
            public byte[] VoxBytes;
        }

        private sealed class VoxSceneState
        {
            public GameObject Root;
            public bool Optimized = true;
            public bool GenerateCollider = true;
        }

        private sealed class VoxDocumentSourceState
        {
            public string SourceKind = VoxSourceKindGenerated;
            public string SourcePath = string.Empty;
            public bool Dirty = true;
        }

        private const string VoxSourceKindGenerated = "Generated";
        private const string VoxSourceKindMediaLibraryFile = "MediaLibraryFile";
        private const string VoxSourceKindEmbeddedSubfile = "EmbeddedSubfile";
        private const string VoxSourceKindImportedBase64 = "ImportedBase64";

        private static readonly List<RuntimeVoxDocument> s_voxDocuments = new List<RuntimeVoxDocument>();
        private static readonly Dictionary<RuntimeVoxDocument, VoxSceneState> s_voxSceneByDocument = new Dictionary<RuntimeVoxDocument, VoxSceneState>();
        private static readonly Dictionary<RuntimeVoxDocument, VoxDocumentSourceState> s_voxSourceByDocument = new Dictionary<RuntimeVoxDocument, VoxDocumentSourceState>();
        private static readonly List<GameObject> s_spawnedVoxRoots = new List<GameObject>();
        private static int s_activeVoxDocumentIndex = -1;
        private static int s_activeVoxModelIndex = 0;
        private static bool s_autoVisuals = true;

        [ApiEndpoint(
            "vox.new",
            "Creates a new runtime VOX document with one default model and makes it active",
            "16,16,16"
        )]
        public static void VoxNew(int sizeX, int sizeY, int sizeZ)
        {
            var document = new RuntimeVoxDocument();
            document.CreateModel("model_0", new Vector3Int(sizeX, sizeY, sizeZ));
            s_voxDocuments.Add(document);
            s_voxSourceByDocument[document] = new VoxDocumentSourceState
            {
                SourceKind = VoxSourceKindGenerated,
                SourcePath = string.Empty,
                Dirty = true,
            };
            s_activeVoxDocumentIndex = s_voxDocuments.Count - 1;
            s_activeVoxModelIndex = 0;
            RefreshActiveDocumentVisual(spawnNearBrush: true);
        }

        [ApiEndpoint(
            "vox.select",
            "Sets the active runtime VOX document by index",
            "0"
        )]
        public static void VoxSelect(int index)
        {
            index = _NegativeIndexing(index, s_voxDocuments);
            if (index < 0 || index >= s_voxDocuments.Count)
            {
                return;
            }

            s_activeVoxDocumentIndex = index;
            s_activeVoxModelIndex = 0;
            RefreshActiveDocumentVisual(spawnNearBrush: true);
        }

        [ApiEndpoint(
            "vox.delete",
            "Deletes a runtime VOX document by index",
            "0"
        )]
        public static void VoxDelete(int index)
        {
            index = _NegativeIndexing(index, s_voxDocuments);
            if (index < 0 || index >= s_voxDocuments.Count)
            {
                return;
            }

            RuntimeVoxDocument removed = s_voxDocuments[index];
            DestroyDocumentScene(removed);
            s_voxSourceByDocument.Remove(removed);
            s_voxDocuments.RemoveAt(index);
            if (s_voxDocuments.Count == 0)
            {
                s_activeVoxDocumentIndex = -1;
                s_activeVoxModelIndex = 0;
            }
            else if (s_activeVoxDocumentIndex >= s_voxDocuments.Count)
            {
                s_activeVoxDocumentIndex = s_voxDocuments.Count - 1;
            }

            RefreshActiveDocumentVisual(spawnNearBrush: true);
        }

        [ApiEndpoint(
            "vox.info",
            "Returns summary JSON for runtime VOX document state",
            "0"
        )]
        public static string VoxInfo(int index = -1)
        {
            int resolvedIndex = ResolveDocIndex(index);
            if (resolvedIndex < 0)
            {
                return JsonConvert.SerializeObject(new
                {
                    active = -1,
                    count = s_voxDocuments.Count,
                    models = Array.Empty<object>(),
                });
            }

            RuntimeVoxDocument document = s_voxDocuments[resolvedIndex];
            VoxDocumentSourceState source = GetSourceState(document);
            var info = new
            {
                active = s_activeVoxDocumentIndex,
                activeModel = s_activeVoxModelIndex,
                autoVisuals = s_autoVisuals,
                index = resolvedIndex,
                count = s_voxDocuments.Count,
                modelCount = document.Models.Count,
                sourceKind = source.SourceKind,
                sourcePath = source.SourcePath,
                dirty = source.Dirty,
                embedOnSave = ShouldEmbedOnSave(source),
                models = BuildModelSummaries(document),
            };
            return JsonConvert.SerializeObject(info);
        }

        [ApiEndpoint(
            "vox.autovisuals",
            "Turns automatic scene updates on or off for VOX document edits",
            "true"
        )]
        public static void VoxSetAutoVisuals(bool enabled)
        {
            s_autoVisuals = enabled;
            if (enabled)
            {
                RefreshActiveDocumentVisual(spawnNearBrush: true);
            }
        }

        [ApiEndpoint(
            "vox.model.add",
            "Adds a model to the active runtime VOX document",
            "16,16,16,model_1"
        )]
        public static void VoxModelAdd(int sizeX, int sizeY, int sizeZ, string name = null)
        {
            if (!TryGetActiveDoc(out RuntimeVoxDocument document))
            {
                return;
            }

            string modelName = string.IsNullOrWhiteSpace(name) ? $"model_{document.Models.Count}" : name;
            document.CreateModel(modelName, new Vector3Int(sizeX, sizeY, sizeZ));
            s_activeVoxModelIndex = document.Models.Count - 1;
            MarkDocumentDirty(document);
            RefreshActiveDocumentVisual();
        }

        [ApiEndpoint(
            "vox.model.select",
            "Sets the active model index in the active runtime VOX document",
            "0"
        )]
        public static void VoxModelSelect(int index)
        {
            if (!TryGetActiveDoc(out RuntimeVoxDocument document))
            {
                return;
            }

            index = _NegativeIndexing(index, document.Models);
            if (index < 0 || index >= document.Models.Count)
            {
                return;
            }

            s_activeVoxModelIndex = index;
        }

        [ApiEndpoint(
            "vox.voxel.set",
            "Adds or updates one voxel in a model in the active document",
            "0,1,2,3,5"
        )]
        public static void VoxVoxelSet(int modelIndex, int x, int y, int z, int paletteIndex)
        {
            if (!TryGetModel(modelIndex, out RuntimeVoxDocument.RuntimeModel model))
            {
                return;
            }

            if (model.AddOrUpdateVoxel(new Vector3Int(x, y, z), (byte)Mathf.Clamp(paletteIndex, 0, 255)))
            {
                MarkActiveDocumentDirty();
                RefreshActiveDocumentVisual();
            }
        }

        [ApiEndpoint(
            "vox.voxel.remove",
            "Removes one voxel in a model in the active document",
            "0,1,2,3"
        )]
        public static void VoxVoxelRemove(int modelIndex, int x, int y, int z)
        {
            if (!TryGetModel(modelIndex, out RuntimeVoxDocument.RuntimeModel model))
            {
                return;
            }

            if (model.RemoveVoxel(new Vector3Int(x, y, z)))
            {
                MarkActiveDocumentDirty();
                RefreshActiveDocumentVisual();
            }
        }

        [ApiEndpoint(
            "vox.voxel.move",
            "Moves one voxel in a model in the active document",
            "0,1,2,3,2,2,3,true"
        )]
        public static void VoxVoxelMove(
            int modelIndex,
            int fromX,
            int fromY,
            int fromZ,
            int toX,
            int toY,
            int toZ,
            bool overwrite = true)
        {
            if (!TryGetModel(modelIndex, out RuntimeVoxDocument.RuntimeModel model))
            {
                return;
            }

            if (model.MoveVoxel(
                new Vector3Int(fromX, fromY, fromZ),
                new Vector3Int(toX, toY, toZ),
                overwrite))
            {
                MarkActiveDocumentDirty();
                RefreshActiveDocumentVisual();
            }
        }

        [ApiEndpoint(
            "vox.set",
            "Adds or updates one voxel in the active model",
            "1,2,3,5"
        )]
        public static void VoxSet(int x, int y, int z, int paletteIndex)
        {
            VoxVoxelSet(s_activeVoxModelIndex, x, y, z, paletteIndex);
        }

        [ApiEndpoint(
            "vox.remove",
            "Removes one voxel in the active model",
            "1,2,3"
        )]
        public static void VoxRemove(int x, int y, int z)
        {
            VoxVoxelRemove(s_activeVoxModelIndex, x, y, z);
        }

        [ApiEndpoint(
            "vox.move",
            "Moves one voxel in the active model",
            "1,2,3,2,2,3,true"
        )]
        public static void VoxMove(
            int fromX,
            int fromY,
            int fromZ,
            int toX,
            int toY,
            int toZ,
            bool overwrite = true)
        {
            VoxVoxelMove(s_activeVoxModelIndex, fromX, fromY, fromZ, toX, toY, toZ, overwrite);
        }

        [ApiEndpoint(
            "vox.palette.set",
            "Sets one palette entry in the active runtime VOX document",
            "1,255,0,0,255"
        )]
        public static void VoxPaletteSet(int paletteIndex, int r, int g, int b, int a = 255)
        {
            if (!TryGetActiveDoc(out RuntimeVoxDocument document))
            {
                return;
            }

            var color = new Color32(
                (byte)Mathf.Clamp(r, 0, 255),
                (byte)Mathf.Clamp(g, 0, 255),
                (byte)Mathf.Clamp(b, 0, 255),
                (byte)Mathf.Clamp(a, 0, 255));
            if (document.ReplacePaletteEntry(paletteIndex, color))
            {
                MarkDocumentDirty(document);
                RefreshActiveDocumentVisual();
            }
        }

        [ApiEndpoint(
            "vox.mesh.stats",
            "Builds mesh for a model in the active document and returns mesh stats JSON",
            "0,optimized"
        )]
        public static string VoxMeshStats(int modelIndex, string mode = "optimized")
        {
            if (!TryGetActiveDoc(out RuntimeVoxDocument document) ||
                !TryGetModel(modelIndex, out RuntimeVoxDocument.RuntimeModel model))
            {
                return "error: invalid document or model";
            }

            var builder = new VoxMeshBuilder();
            bool optimized = !string.Equals(mode, "cubes", StringComparison.OrdinalIgnoreCase);
            Mesh mesh = optimized
                ? builder.GenerateOptimizedMesh(model, document.Palette)
                : builder.GenerateSeparateCubesMesh(model, document.Palette);

            try
            {
                var stats = new
                {
                    mode = optimized ? "optimized" : "cubes",
                    vertexCount = mesh.vertexCount,
                    triangleIndexCount = mesh.triangles.Length,
                    boundsCenter = ToVector3Payload(mesh.bounds.center),
                    boundsSize = ToVector3Payload(mesh.bounds.size),
                };
                return JsonConvert.SerializeObject(stats);
            }
            finally
            {
                UnityEngine.Object.Destroy(mesh);
            }
        }

        [ApiEndpoint(
            "vox.spawn",
            "Spawns the active runtime VOX document into the scene at brush position",
            "optimized,true"
        )]
        public static void VoxSpawn(string mode = "optimized", bool generateCollider = true)
        {
            if (!TryGetActiveDoc(out RuntimeVoxDocument document))
            {
                return;
            }

            RebuildSceneForDocument(
                document,
                spawnNearBrush: true,
                optimizedOverride: !string.Equals(mode, "cubes", StringComparison.OrdinalIgnoreCase),
                colliderOverride: generateCollider);
        }

        [ApiEndpoint(
            "vox.spawn.clear",
            "Destroys runtime VOX scene objects previously created by vox.spawn",
            ""
        )]
        public static void VoxSpawnClear()
        {
            foreach (VoxSceneState state in s_voxSceneByDocument.Values)
            {
                if (state.Root != null)
                {
                    UnityEngine.Object.Destroy(state.Root);
                }
            }
            s_voxSceneByDocument.Clear();

            foreach (GameObject root in s_spawnedVoxRoots)
            {
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                }
            }

            s_spawnedVoxRoots.Clear();
        }

        public static void VoxRegisterSpawnedRoot(GameObject root)
        {
            if (root != null)
            {
                s_spawnedVoxRoots.Add(root);
            }
        }

        public static void VoxResetRuntimeState()
        {
            VoxSpawnClear();
            s_voxDocuments.Clear();
            s_voxSourceByDocument.Clear();
            s_activeVoxDocumentIndex = -1;
            s_activeVoxModelIndex = 0;
            s_autoVisuals = true;
        }

        public static RuntimeVoxSavePayload[] VoxGetSavePayloads()
        {
            if (s_voxDocuments.Count == 0)
            {
                return null;
            }

            var payloads = new RuntimeVoxSavePayload[s_voxDocuments.Count];
            for (int i = 0; i < s_voxDocuments.Count; i++)
            {
                RuntimeVoxDocument document = s_voxDocuments[i];
                s_voxSceneByDocument.TryGetValue(document, out VoxSceneState sceneState);
                VoxDocumentSourceState source = GetSourceState(document);
                bool embed = ShouldEmbedOnSave(source);

                TrTransform transform = TrTransform.identity;
                if (sceneState?.Root != null)
                {
                    transform = TrTransform.TRS(
                        sceneState.Root.transform.localPosition,
                        sceneState.Root.transform.localRotation,
                        sceneState.Root.transform.localScale.x);
                }

                payloads[i] = new RuntimeVoxSavePayload
                {
                    Document = document,
                    VoxBytes = embed ? document.ToVoxBytes() : null,
                    State = new RuntimeVoxState
                    {
                        FilePath = embed ? $"vox/{i}.vox" : null,
                        Transform = transform,
                        Optimized = sceneState?.Optimized ?? true,
                        GenerateCollider = sceneState?.GenerateCollider ?? true,
                        SourceKind = source.SourceKind,
                        SourcePath = source.SourcePath,
                        Dirty = source.Dirty,
                    },
                };
            }

            return payloads;
        }

        public static void VoxRestoreFromTilt(SceneFileInfo fileInfo, RuntimeVoxState[] runtimeVoxIndex)
        {
            VoxResetRuntimeState();
            if (fileInfo == null || runtimeVoxIndex == null || runtimeVoxIndex.Length == 0)
            {
                return;
            }

            foreach (RuntimeVoxState item in runtimeVoxIndex)
            {
                if (item == null)
                {
                    continue;
                }

                try
                {
                    byte[] bytes = LoadRuntimeVoxBytes(fileInfo, item);
                    if (bytes == null || bytes.Length == 0)
                    {
                        continue;
                    }

                    RuntimeVoxDocument document = RuntimeVoxDocument.FromBytes(bytes);
                    s_voxDocuments.Add(document);
                    s_voxSourceByDocument[document] = new VoxDocumentSourceState
                    {
                        SourceKind = string.IsNullOrEmpty(item.SourceKind)
                            ? (string.IsNullOrEmpty(item.FilePath) ? VoxSourceKindGenerated : VoxSourceKindEmbeddedSubfile)
                            : item.SourceKind,
                        SourcePath = item.SourcePath ?? string.Empty,
                        Dirty = item.Dirty,
                    };
                    RebuildSceneForDocument(
                        document,
                        spawnNearBrush: false,
                        optimizedOverride: item.Optimized,
                        colliderOverride: item.GenerateCollider);

                    if (s_voxSceneByDocument.TryGetValue(document, out VoxSceneState state) &&
                        state?.Root != null)
                    {
                        float scale = item.Transform.scale <= 0f ? 1f : item.Transform.scale;
                        state.Root.transform.localPosition = item.Transform.translation;
                        state.Root.transform.localRotation = item.Transform.rotation;
                        state.Root.transform.localScale = Vector3.one * scale;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to restore runtime VOX item '{item.FilePath ?? item.SourcePath}': {e.Message}");
                }
            }

            if (s_voxDocuments.Count > 0)
            {
                s_activeVoxDocumentIndex = 0;
                s_activeVoxModelIndex = 0;
            }
        }

        [ApiEndpoint(
            "vox.export.base64",
            "Exports the active runtime VOX document to base64",
            ""
        )]
        public static string VoxExportBase64()
        {
            if (!TryGetActiveDoc(out RuntimeVoxDocument document))
            {
                return string.Empty;
            }

            byte[] bytes = document.ToVoxBytes();
            return Convert.ToBase64String(bytes);
        }

        [ApiEndpoint(
            "vox.import.base64",
            "Imports base64 VOX bytes as a new active runtime VOX document",
            "dm94IGJ5dGVzIGhlcmU="
        )]
        public static void VoxImportBase64(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            RuntimeVoxDocument document = RuntimeVoxDocument.FromBytes(bytes);
            s_voxDocuments.Add(document);
            s_voxSourceByDocument[document] = new VoxDocumentSourceState
            {
                SourceKind = VoxSourceKindImportedBase64,
                SourcePath = string.Empty,
                Dirty = true,
            };
            s_activeVoxDocumentIndex = s_voxDocuments.Count - 1;
            s_activeVoxModelIndex = 0;
            RefreshActiveDocumentVisual(spawnNearBrush: true);
        }

        [ApiEndpoint(
            "vox.open.file",
            "Opens a VOX file from Media Library/Models (relative path) as an editable document",
            "vox/house.vox"
        )]
        public static void VoxOpenFile(string relativePath)
        {
            string normalized = NormalizeRelativeVoxPath(relativePath);
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            var model = new Model(normalized);
            string absolutePath = model.GetLocation().AbsolutePath;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                return;
            }

            byte[] bytes = File.ReadAllBytes(absolutePath);
            RuntimeVoxDocument document = RuntimeVoxDocument.FromBytes(bytes);
            s_voxDocuments.Add(document);
            s_voxSourceByDocument[document] = new VoxDocumentSourceState
            {
                SourceKind = VoxSourceKindMediaLibraryFile,
                SourcePath = normalized,
                Dirty = false,
            };
            s_activeVoxDocumentIndex = s_voxDocuments.Count - 1;
            s_activeVoxModelIndex = 0;
            RefreshActiveDocumentVisual(spawnNearBrush: true);
        }

        [ApiEndpoint(
            "vox.doc.source",
            "Returns source/dirty metadata JSON for a VOX document (default active)",
            "0"
        )]
        public static string VoxDocSource(int index = -1)
        {
            int resolvedIndex = ResolveDocIndex(index);
            if (resolvedIndex < 0)
            {
                return JsonConvert.SerializeObject(new
                {
                    index = -1,
                    sourceKind = string.Empty,
                    sourcePath = string.Empty,
                    dirty = false,
                    embedOnSave = false,
                });
            }

            RuntimeVoxDocument document = s_voxDocuments[resolvedIndex];
            VoxDocumentSourceState source = GetSourceState(document);
            return JsonConvert.SerializeObject(new
            {
                index = resolvedIndex,
                sourceKind = source.SourceKind,
                sourcePath = source.SourcePath,
                dirty = source.Dirty,
                embedOnSave = ShouldEmbedOnSave(source),
            });
        }

        private static bool TryGetActiveDoc(out RuntimeVoxDocument document)
        {
            document = null;
            if (s_activeVoxDocumentIndex < 0 || s_activeVoxDocumentIndex >= s_voxDocuments.Count)
            {
                return false;
            }

            document = s_voxDocuments[s_activeVoxDocumentIndex];
            return true;
        }

        private static bool TryGetModel(int modelIndex, out RuntimeVoxDocument.RuntimeModel model)
        {
            model = null;
            if (!TryGetActiveDoc(out RuntimeVoxDocument document))
            {
                return false;
            }

            modelIndex = _NegativeIndexing(modelIndex, document.Models);
            if (modelIndex < 0 || modelIndex >= document.Models.Count)
            {
                return false;
            }

            model = document.Models[modelIndex];
            return true;
        }

        private static int ResolveDocIndex(int index)
        {
            if (s_voxDocuments.Count == 0)
            {
                return -1;
            }

            if (index == -1)
            {
                return s_activeVoxDocumentIndex;
            }

            index = _NegativeIndexing(index, s_voxDocuments);
            if (index < 0 || index >= s_voxDocuments.Count)
            {
                return -1;
            }

            return index;
        }

        private static void RefreshActiveDocumentVisual(bool spawnNearBrush = false)
        {
            if (!s_autoVisuals || !TryGetActiveDoc(out RuntimeVoxDocument document))
            {
                return;
            }

            RebuildSceneForDocument(document, spawnNearBrush);
        }

        private static void RebuildSceneForDocument(
            RuntimeVoxDocument document,
            bool spawnNearBrush,
            bool? optimizedOverride = null,
            bool? colliderOverride = null)
        {
            if (document == null)
            {
                return;
            }

            s_voxSceneByDocument.TryGetValue(document, out VoxSceneState existingState);
            GameObject previousRoot = existingState?.Root;

            var state = existingState ?? new VoxSceneState();
            if (optimizedOverride.HasValue)
            {
                state.Optimized = optimizedOverride.Value;
            }
            if (colliderOverride.HasValue)
            {
                state.GenerateCollider = colliderOverride.Value;
            }

            Transform parent = App.Scene.ActiveCanvas.transform;
            Vector3 localPosition = Vector3.zero;
            Quaternion localRotation = Quaternion.identity;
            Vector3 localScale = Vector3.one;

            if (previousRoot != null)
            {
                parent = previousRoot.transform.parent;
                localPosition = previousRoot.transform.localPosition;
                localRotation = previousRoot.transform.localRotation;
                localScale = previousRoot.transform.localScale;
            }
            else if (spawnNearBrush)
            {
                TrTransform brushXf = _CurrentBrushTransform();
                localPosition = brushXf.translation;
                localRotation = brushXf.rotation;
            }

            var root = new GameObject($"VoxRuntime_{s_voxDocuments.IndexOf(document)}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = localRotation;
            root.transform.localScale = localScale;

            var builder = new VoxMeshBuilder();
            for (int i = 0; i < document.Models.Count; i++)
            {
                RuntimeVoxDocument.RuntimeModel model = document.Models[i];
                if (model.Voxels.Count == 0)
                {
                    continue;
                }

                var modelObject = new GameObject($"Model_{i}_{model.Name}");
                modelObject.transform.SetParent(root.transform, false);
                modelObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                Mesh mesh = state.Optimized
                    ? builder.GenerateOptimizedMesh(model, document.Palette)
                    : builder.GenerateSeparateCubesMesh(model, document.Palette);
                if (mesh == null)
                {
                    continue;
                }

                var mf = modelObject.AddComponent<MeshFilter>();
                mf.mesh = mesh;

                var mr = modelObject.AddComponent<MeshRenderer>();
                mr.material = ModelCatalog.m_Instance.m_VoxLoaderStandardMaterial;

                if (state.GenerateCollider)
                {
                    var collider = modelObject.AddComponent<BoxCollider>();
                    collider.size = mesh.bounds.size;
                    collider.center = mesh.bounds.center;
                }
            }

            if (previousRoot != null)
            {
                UnityEngine.Object.Destroy(previousRoot);
            }

            state.Root = root;
            s_voxSceneByDocument[document] = state;
            WidgetManager.m_Instance.WidgetsDormant = false;
            SketchControlsScript.m_Instance.EatGazeObjectInput();
        }

        private static void DestroyDocumentScene(RuntimeVoxDocument document)
        {
            if (document == null || !s_voxSceneByDocument.TryGetValue(document, out VoxSceneState state))
            {
                return;
            }

            if (state.Root != null)
            {
                UnityEngine.Object.Destroy(state.Root);
            }

            s_voxSceneByDocument.Remove(document);
        }

        private static void MarkActiveDocumentDirty()
        {
            if (TryGetActiveDoc(out RuntimeVoxDocument document))
            {
                MarkDocumentDirty(document);
            }
        }

        private static void MarkDocumentDirty(RuntimeVoxDocument document)
        {
            VoxDocumentSourceState source = GetSourceState(document);
            source.Dirty = true;
            if (source.SourceKind == VoxSourceKindMediaLibraryFile && string.IsNullOrEmpty(source.SourcePath))
            {
                source.SourceKind = VoxSourceKindGenerated;
            }
        }

        private static VoxDocumentSourceState GetSourceState(RuntimeVoxDocument document)
        {
            if (document == null)
            {
                return new VoxDocumentSourceState();
            }

            if (!s_voxSourceByDocument.TryGetValue(document, out VoxDocumentSourceState source) || source == null)
            {
                source = new VoxDocumentSourceState();
                s_voxSourceByDocument[document] = source;
            }

            source.SourceKind = source.SourceKind ?? VoxSourceKindGenerated;
            source.SourcePath = source.SourcePath ?? string.Empty;
            return source;
        }

        private static bool ShouldEmbedOnSave(VoxDocumentSourceState source)
        {
            if (source == null)
            {
                return true;
            }

            if (string.Equals(source.SourceKind, VoxSourceKindMediaLibraryFile, StringComparison.OrdinalIgnoreCase) &&
                !source.Dirty)
            {
                return false;
            }

            return true;
        }

        private static byte[] LoadRuntimeVoxBytes(SceneFileInfo fileInfo, RuntimeVoxState item)
        {
            if (!string.IsNullOrEmpty(item.FilePath))
            {
                using (Stream stream = fileInfo.GetReadStream(item.FilePath))
                {
                    return ReadAllBytes(stream);
                }
            }

            if (string.Equals(item.SourceKind, VoxSourceKindMediaLibraryFile, StringComparison.OrdinalIgnoreCase))
            {
                string normalized = NormalizeRelativeVoxPath(item.SourcePath);
                if (!string.IsNullOrEmpty(normalized))
                {
                    var model = new Model(normalized);
                    string absolutePath = model.GetLocation().AbsolutePath;
                    if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
                    {
                        return File.ReadAllBytes(absolutePath);
                    }
                }
            }

            return null;
        }

        private static string NormalizeRelativeVoxPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            string normalized = relativePath.Trim().Replace("\\", "/");
            if (normalized.StartsWith("/"))
            {
                normalized = normalized.Substring(1);
            }

            if (Path.IsPathRooted(normalized))
            {
                return null;
            }

            if (normalized.IndexOf("../", StringComparison.Ordinal) >= 0 ||
                normalized.IndexOf("..\\", StringComparison.Ordinal) >= 0)
            {
                return null;
            }

            if (!normalized.EndsWith(".vox", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return normalized;
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            if (stream == null)
            {
                return Array.Empty<byte>();
            }

            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private static object ToVector3Payload(Vector3 value)
        {
            return new
            {
                x = value.x,
                y = value.y,
                z = value.z,
            };
        }

        private static object[] BuildModelSummaries(RuntimeVoxDocument document)
        {
            var models = new object[document.Models.Count];
            for (int i = 0; i < document.Models.Count; i++)
            {
                RuntimeVoxDocument.RuntimeModel model = document.Models[i];
                models[i] = new
                {
                    index = i,
                    name = model.Name,
                    size = ToVector3Payload(model.Size),
                    voxelCount = model.Voxels.Count,
                };
            }

            return models;
        }
    }
}
