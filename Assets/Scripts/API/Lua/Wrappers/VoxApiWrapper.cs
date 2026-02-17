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
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("Mesh statistics for a runtime VOX model")]
    [MoonSharpUserData]
    public class VoxMeshStatsApiWrapper
    {
        [LuaDocsDescription("Mesh generation mode used")]
        public string mode { get; }

        [LuaDocsDescription("Number of mesh vertices")]
        public int vertexCount { get; }

        [LuaDocsDescription("Number of triangle indices")]
        public int triangleIndexCount { get; }

        [LuaDocsDescription("Mesh bounds center")]
        public Vector3 boundsCenter { get; }

        [LuaDocsDescription("Mesh bounds size")]
        public Vector3 boundsSize { get; }

        public VoxMeshStatsApiWrapper(string meshMode, Mesh mesh)
        {
            mode = meshMode;
            vertexCount = mesh.vertexCount;
            triangleIndexCount = mesh.triangles.Length;
            boundsCenter = mesh.bounds.center;
            boundsSize = mesh.bounds.size;
        }
    }

    [LuaDocsDescription("A runtime VOX model")]
    [MoonSharpUserData]
    public class VoxModelApiWrapper
    {
        [MoonSharpHidden] private readonly RuntimeVoxDocument m_Document;
        [MoonSharpHidden] private readonly VoxDocumentApiWrapper m_DocumentWrapper;
        [MoonSharpHidden] public RuntimeVoxDocument.RuntimeModel _Model;

        public VoxModelApiWrapper(
            RuntimeVoxDocument document,
            RuntimeVoxDocument.RuntimeModel model,
            VoxDocumentApiWrapper documentWrapper = null)
        {
            m_Document = document;
            _Model = model;
            m_DocumentWrapper = documentWrapper;
        }

        [LuaDocsDescription("The model name")]
        public string name => _Model.Name;

        [LuaDocsDescription("Number of voxels in this model")]
        public int voxelCount => _Model.Voxels.Count;

        [LuaDocsDescription("X size of the model in voxels")]
        public int sizeX => _Model.Size.x;

        [LuaDocsDescription("Y size of the model in voxels")]
        public int sizeY => _Model.Size.y;

        [LuaDocsDescription("Z size of the model in voxels")]
        public int sizeZ => _Model.Size.z;

        [LuaDocsDescription("Sets or adds one voxel")]
        [LuaDocsExample("model:SetVoxel(1,2,3,5)")]
        public bool SetVoxel(int x, int y, int z, int paletteIndex)
        {
            bool changed = _Model.AddOrUpdateVoxel(new Vector3Int(x, y, z), (byte)Mathf.Clamp(paletteIndex, 0, 255));
            if (changed)
            {
                m_DocumentWrapper?.OnDocumentMutated();
            }

            return changed;
        }

        [LuaDocsDescription("Removes one voxel")]
        public bool RemoveVoxel(int x, int y, int z)
        {
            bool changed = _Model.RemoveVoxel(new Vector3Int(x, y, z));
            if (changed)
            {
                m_DocumentWrapper?.OnDocumentMutated();
            }

            return changed;
        }

        [LuaDocsDescription("Moves one voxel")]
        public bool MoveVoxel(int fromX, int fromY, int fromZ, int toX, int toY, int toZ, bool overwrite = true)
        {
            bool changed = _Model.MoveVoxel(
                new Vector3Int(fromX, fromY, fromZ),
                new Vector3Int(toX, toY, toZ),
                overwrite);
            if (changed)
            {
                m_DocumentWrapper?.OnDocumentMutated();
            }

            return changed;
        }

        [LuaDocsDescription("Alias for SetVoxel, intended for quick interactive editing")]
        public bool Set(int x, int y, int z, int paletteIndex)
            => SetVoxel(x, y, z, paletteIndex);

        [LuaDocsDescription("Alias for RemoveVoxel, intended for quick interactive editing")]
        public bool Remove(int x, int y, int z)
            => RemoveVoxel(x, y, z);

        [LuaDocsDescription("Alias for MoveVoxel, intended for quick interactive editing")]
        public bool Move(int fromX, int fromY, int fromZ, int toX, int toY, int toZ, bool overwrite = true)
            => MoveVoxel(fromX, fromY, fromZ, toX, toY, toZ, overwrite);

        [LuaDocsDescription("Builds a mesh and returns statistics for this model")]
        [LuaDocsExample("local stats = model:MeshStats(true)")]
        public VoxMeshStatsApiWrapper MeshStats(bool optimized = true)
        {
            var builder = new VoxMeshBuilder();
            Mesh mesh = optimized
                ? builder.GenerateOptimizedMesh(_Model, m_Document.Palette)
                : builder.GenerateSeparateCubesMesh(_Model, m_Document.Palette);

            try
            {
                return new VoxMeshStatsApiWrapper(optimized ? "optimized" : "cubes", mesh);
            }
            finally
            {
                UnityEngine.Object.Destroy(mesh);
            }
        }
    }

    [LuaDocsDescription("The list of runtime VOX models in this document")]
    [MoonSharpUserData]
    public class VoxModelListApiWrapper
    {
        [MoonSharpHidden] private readonly VoxDocumentApiWrapper m_DocumentWrapper;

        [MoonSharpHidden]
        public System.Collections.Generic.IReadOnlyList<RuntimeVoxDocument.RuntimeModel> _Models;

        public VoxModelListApiWrapper(VoxDocumentApiWrapper documentWrapper)
        {
            m_DocumentWrapper = documentWrapper;
            _Models = documentWrapper._Document.Models;
        }

        [LuaDocsDescription("Returns the last model")]
        public VoxModelApiWrapper last => (_Models == null || _Models.Count == 0)
            ? null
            : new VoxModelApiWrapper(m_DocumentWrapper._Document, _Models[^1], m_DocumentWrapper);

        [LuaDocsDescription("Returns the model at the given index")]
        public VoxModelApiWrapper this[int index]
            => new VoxModelApiWrapper(
                m_DocumentWrapper._Document,
                Utils.WrappedIndexerGet(() => _Models[index]),
                m_DocumentWrapper);

        [LuaDocsDescription("The number of models")]
        public int count => _Models?.Count ?? 0;
    }

    [LuaDocsDescription("A runtime VOX document")]
    [MoonSharpUserData]
    public class VoxDocumentApiWrapper
    {
        [MoonSharpHidden] public RuntimeVoxDocument _Document;
        [MoonSharpHidden] private GameObject m_SceneRoot;
        [MoonSharpHidden] private bool m_AutoVisuals;
        [MoonSharpHidden] private bool m_LastSpawnOptimized = true;
        [MoonSharpHidden] private bool m_LastSpawnCollider = true;
        [MoonSharpHidden] private Vector3 m_SpawnPosition = Vector3.zero;

        public VoxDocumentApiWrapper(RuntimeVoxDocument document)
        {
            _Document = document;
        }

        [LuaDocsDescription("All models in this VOX document")]
        public VoxModelListApiWrapper models => new VoxModelListApiWrapper(this);

        [LuaDocsDescription("Returns the number of models in this VOX document")]
        public int modelCount => _Document.Models.Count;

        [LuaDocsDescription("If true, model/palette edits automatically rebuild scene geometry")]
        public bool autoVisuals => m_AutoVisuals;

        [LuaDocsDescription("Returns a model with the given name")]
        [LuaDocsExample("local m = doc:FindModel('model_0')")]
        public VoxModelApiWrapper FindModel(string name)
        {
            RuntimeVoxDocument.RuntimeModel model = _Document.Models.FirstOrDefault(x => x.Name == name);
            return model == null ? null : new VoxModelApiWrapper(_Document, model, this);
        }

        [LuaDocsDescription("Adds a model to this document")]
        [LuaDocsExample("local m = doc:AddModel(16,16,16,'model_1')")]
        public VoxModelApiWrapper AddModel(int sizeX, int sizeY, int sizeZ, string name = null)
        {
            string modelName = string.IsNullOrWhiteSpace(name)
                ? $"model_{_Document.Models.Count}"
                : name;
            RuntimeVoxDocument.RuntimeModel model = _Document.CreateModel(modelName, new Vector3Int(sizeX, sizeY, sizeZ));
            OnDocumentMutated();
            return new VoxModelApiWrapper(_Document, model, this);
        }

        [LuaDocsDescription("Sets one palette entry")]
        public bool SetPalette(int paletteIndex, int r, int g, int b, int a = 255)
        {
            var color = new Color32(
                (byte)Mathf.Clamp(r, 0, 255),
                (byte)Mathf.Clamp(g, 0, 255),
                (byte)Mathf.Clamp(b, 0, 255),
                (byte)Mathf.Clamp(a, 0, 255));
            bool changed = _Document.ReplacePaletteEntry(paletteIndex, color);
            if (changed)
            {
                OnDocumentMutated();
            }

            return changed;
        }

        [LuaDocsDescription("Exports this document as base64 VOX bytes")]
        public string ExportBase64()
            => Convert.ToBase64String(_Document.ToVoxBytes());

        [LuaDocsDescription("Spawns or refreshes this document in the current canvas")]
        [LuaDocsExample("doc:Spawn(true, true)")]
        public void Spawn(bool optimized = true, bool generateCollider = true)
        {
            m_LastSpawnOptimized = optimized;
            m_LastSpawnCollider = generateCollider;
            EnsureSceneRoot();
            RebuildSceneChildren();
        }

        [LuaDocsDescription("Spawns this document at a specific world position")]
        [LuaDocsExample("doc:SpawnAt(0, 0, 0, true, true)")]
        public void SpawnAt(float x, float y, float z, bool optimized = true, bool generateCollider = true)
        {
            m_LastSpawnOptimized = optimized;
            m_LastSpawnCollider = generateCollider;
            m_SpawnPosition = new Vector3(x, y, z);
            EnsureSceneRoot();
            RebuildSceneChildren();
        }

        [LuaDocsDescription("Enables or disables automatic visual rebuild after edits")]
        [LuaDocsExample("doc:SetAutoVisuals(true, true, true)")]
        public void SetAutoVisuals(bool enabled = true, bool optimized = true, bool generateCollider = true)
        {
            m_AutoVisuals = enabled;
            m_LastSpawnOptimized = optimized;
            m_LastSpawnCollider = generateCollider;
            if (enabled)
            {
                Spawn(optimized, generateCollider);
            }
        }

        [LuaDocsDescription("Clears this document's spawned scene object, if present")]
        public void ClearScene()
        {
            if (m_SceneRoot != null)
            {
                UnityEngine.Object.Destroy(m_SceneRoot);
                m_SceneRoot = null;
            }
        }

        [MoonSharpHidden]
        internal void OnDocumentMutated()
        {
            if (m_AutoVisuals)
            {
                Spawn(m_LastSpawnOptimized, m_LastSpawnCollider);
            }
        }

        private void EnsureSceneRoot()
        {
            if (m_SceneRoot != null)
            {
                return;
            }

            m_SceneRoot = new GameObject("VoxRuntime_Lua");
            m_SceneRoot.transform.SetParent(App.Scene.ActiveCanvas.transform, false);
            m_SceneRoot.transform.localPosition = m_SpawnPosition;
            m_SceneRoot.transform.localRotation = Quaternion.identity;
            m_SceneRoot.transform.localScale = Vector3.one;
            ApiMethods.VoxRegisterSpawnedRoot(m_SceneRoot);
            WidgetManager.m_Instance.WidgetsDormant = false;
            SketchControlsScript.m_Instance.EatGazeObjectInput();
        }

        private void RebuildSceneChildren()
        {
            if (m_SceneRoot == null)
            {
                return;
            }

            for (int i = m_SceneRoot.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(m_SceneRoot.transform.GetChild(i).gameObject);
            }

            var builder = new VoxMeshBuilder();

            for (int i = 0; i < _Document.Models.Count; i++)
            {
                RuntimeVoxDocument.RuntimeModel model = _Document.Models[i];
                if (model.Voxels.Count == 0)
                {
                    continue;
                }

                var modelObject = new GameObject($"Model_{i}_{model.Name}");
                modelObject.transform.SetParent(m_SceneRoot.transform, false);
                modelObject.transform.localPosition = model.TransformOffset;
                modelObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                Mesh mesh = m_LastSpawnOptimized
                    ? builder.GenerateOptimizedMesh(model, _Document.Palette)
                    : builder.GenerateSeparateCubesMesh(model, _Document.Palette);
                if (mesh == null)
                {
                    continue;
                }

                var mf = modelObject.AddComponent<MeshFilter>();
                mf.mesh = mesh;

                var mr = modelObject.AddComponent<MeshRenderer>();
                mr.material = ModelCatalog.m_Instance.m_VoxLoaderStandardMaterial;

                if (m_LastSpawnCollider)
                {
                    var collider = modelObject.AddComponent<BoxCollider>();
                    collider.size = mesh.bounds.size;
                    collider.center = mesh.bounds.center;
                }
            }
        }
    }

    [LuaDocsDescription("Runtime VOX document API")]
    [MoonSharpUserData]
    public class VoxApiWrapper
    {
        [LuaDocsDescription("Creates a new runtime VOX document with one default model")]
        [LuaDocsExample("local doc = Vox:New(16,16,16)")]
        public static VoxDocumentApiWrapper New(int sizeX, int sizeY, int sizeZ)
        {
            var document = new RuntimeVoxDocument();
            document.CreateModel("model_0", new Vector3Int(sizeX, sizeY, sizeZ));
            return new VoxDocumentApiWrapper(document);
        }

        [LuaDocsDescription("Creates a new runtime VOX document and immediately spawns it for interactive editing")]
        [LuaDocsExample("local doc = Vox:NewScene(16,16,16,true,true)")]
        public static VoxDocumentApiWrapper NewScene(
            int sizeX,
            int sizeY,
            int sizeZ,
            bool optimized = true,
            bool generateCollider = true)
        {
            VoxDocumentApiWrapper document = New(sizeX, sizeY, sizeZ);
            document.SetAutoVisuals(true, optimized, generateCollider);
            return document;
        }

        [LuaDocsDescription("Imports VOX bytes from base64")]
        [LuaDocsExample("local doc = Vox:ImportBase64(base64)")]
        public static VoxDocumentApiWrapper ImportBase64(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            RuntimeVoxDocument document = RuntimeVoxDocument.FromBytes(bytes);
            return new VoxDocumentApiWrapper(document);
        }

        [LuaDocsDescription("Imports VOX bytes and immediately spawns it for interactive editing")]
        [LuaDocsExample("local doc = Vox:ImportSceneBase64(base64,true,true)")]
        public static VoxDocumentApiWrapper ImportSceneBase64(
            string base64,
            bool optimized = true,
            bool generateCollider = true)
        {
            VoxDocumentApiWrapper document = ImportBase64(base64);
            document.SetAutoVisuals(true, optimized, generateCollider);
            return document;
        }

        [LuaDocsDescription("Clears scene objects previously spawned by runtime VOX APIs")]
        public static void ClearSpawned()
            => ApiMethods.VoxSpawnClear();
    }
}
