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
            m_DocumentWrapper = documentWrapper ?? new VoxDocumentApiWrapper(document);
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

        [LuaDocsDescription("The document containing this model")]
        public VoxDocumentApiWrapper document => m_DocumentWrapper;

        [LuaDocsDescription("The middle voxel coordinate, used as the anchor by PlaceAt")]
        public Vector3 centerVoxel => new Vector3(_Model.Size.x / 2, _Model.Size.y / 2, _Model.Size.z / 2);

        [LuaDocsDescription("The size of one voxel in canvas units")]
        public float voxelSize => m_DocumentWrapper.GetSceneTransform().scale;

        [LuaDocsDescription("Places this model's middle voxel at a canvas position with the given voxel size")]
        [LuaDocsExample("model:PlaceAt(Brush.position, 0.1)")]
        public void PlaceAt(Vector3 canvasPosition, float voxelSize = 1f)
        {
            TrTransform transform = m_DocumentWrapper.GetSceneTransform();
            transform.scale = voxelSize;
            transform.translation = canvasPosition - transform.rotation *
                (_Model.TransformOffset + VoxMeshBuilder.ModelRotation * centerVoxel) * voxelSize;
            m_DocumentWrapper.SetTransform(transform);
        }

        [LuaDocsDescription("Returns the nearest voxel coordinate for a canvas position; it can be outside the model bounds")]
        public Vector3 CanvasToVoxel(Vector3 canvasPosition)
            => Vector3Int.RoundToInt(m_DocumentWrapper.GetModelTransform(_Model).inverse * canvasPosition);

        [LuaDocsDescription("Returns the canvas position of a voxel's center")]
        public Vector3 VoxelToCanvas(Vector3 voxelPosition)
            => m_DocumentWrapper.GetModelTransform(_Model) * voxelPosition;

        [LuaDocsDescription("Paints the voxel at a canvas position using an RGB color. Allocates an unused palette entry, or uses the nearest color if all 255 entries are occupied. Returns false outside the model or if unchanged.")]
        [LuaDocsExample("model:PaintAt(Brush.position, Brush.colorRgb)")]
        public bool PaintAt(Vector3 canvasPosition, Color color)
        {
            Vector3Int cell = Vector3Int.RoundToInt(CanvasToVoxel(canvasPosition));
            if (!_Model.IsInBounds(cell))
            {
                return false;
            }
            return SetVoxel(cell.x, cell.y, cell.z, m_Document.GetOrAddPaletteColor(color));
        }

        [LuaDocsDescription("Erases the voxel at a canvas position. Returns false if the cell is empty or outside the model.")]
        public bool EraseAt(Vector3 canvasPosition)
        {
            Vector3Int cell = Vector3Int.RoundToInt(CanvasToVoxel(canvasPosition));
            return RemoveVoxel(cell.x, cell.y, cell.z);
        }

        [LuaDocsDescription("Erases an occupied voxel at a canvas position, or paints an empty one with the given RGB color")]
        public bool ToggleAt(Vector3 canvasPosition, Color color)
            => EraseAt(canvasPosition) || PaintAt(canvasPosition, color);

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
        [MoonSharpHidden] private GameObject SceneRoot => ApiMethods.VoxGetDocumentRoot(_Document);
        [MoonSharpHidden] private bool m_VisualsDirty = true;
        [MoonSharpHidden] private bool m_AutoVisuals;
        [MoonSharpHidden] private bool m_LastSpawnOptimized = true;
        [MoonSharpHidden] private bool m_LastSpawnCollider = true;
        [MoonSharpHidden] private TrTransform m_SpawnTransform = TrTransform.identity;

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
            TrTransform placement = GetSceneTransform();
            ApiMethods.VoxShowDocument(_Document, optimized, generateCollider);
            SetTransform(placement);
            m_VisualsDirty = false;
        }

        [LuaDocsDescription("Spawns this document at a specific canvas position")]
        [LuaDocsExample("doc:SpawnAt(0, 0, 0, true, true)")]
        public void SpawnAt(float x, float y, float z, bool optimized = true, bool generateCollider = true)
        {
            m_LastSpawnOptimized = optimized;
            m_LastSpawnCollider = generateCollider;
            var transform = m_SpawnTransform;
            transform.translation = new Vector3(x, y, z);
            SetTransform(transform);
            Spawn(optimized, generateCollider);
        }

        [LuaDocsDescription("Sets the spawned document's position, rotation and scale in canvas space")]
        [LuaDocsExample("doc:SetTransform(Transform:New(Vector3:New(0,1,0), 0.1))")]
        public void SetTransform(TrTransform transform)
        {
            if (transform.scale <= 0 || float.IsNaN(transform.scale) || float.IsInfinity(transform.scale))
            {
                throw new ArgumentOutOfRangeException(nameof(transform), "VOX scene scale must be positive and finite.");
            }

            m_SpawnTransform = transform;
            GameObject root = SceneRoot;
            if (root != null)
            {
                if (root.transform.localPosition == transform.translation &&
                    root.transform.localRotation == transform.rotation &&
                    root.transform.localScale == Vector3.one * transform.scale)
                {
                    return;
                }
                root.transform.localPosition = transform.translation;
                root.transform.localRotation = transform.rotation;
                root.transform.localScale = Vector3.one * transform.scale;
                ApiMethods.VoxNotifySceneChanged();
            }
        }

        [LuaDocsDescription("Configures automatic visual updates and mesh options. Repeated unchanged calls do not rebuild geometry. When disabled, call Refresh to show pending edits.")]
        [LuaDocsExample("doc:SetAutoVisuals(true, true, true)")]
        public void SetAutoVisuals(bool enabled = true, bool optimized = true, bool generateCollider = true)
        {
            m_VisualsDirty |= m_LastSpawnOptimized != optimized || m_LastSpawnCollider != generateCollider;
            m_AutoVisuals = enabled;
            m_LastSpawnOptimized = optimized;
            m_LastSpawnCollider = generateCollider;
            if (enabled)
            {
                Refresh();
            }
        }

        [LuaDocsDescription("Shows pending edits, or spawns this document if it is not visible. Does nothing if the visuals are already current.")]
        public void Refresh()
        {
            if (m_VisualsDirty || SceneRoot == null)
            {
                Spawn(m_LastSpawnOptimized, m_LastSpawnCollider);
            }
        }

        [LuaDocsDescription("Clears this document's spawned scene object, if present")]
        public void ClearScene()
        {
            m_SpawnTransform = GetSceneTransform();
            ApiMethods.VoxHideDocument(_Document);
        }

        [MoonSharpHidden]
        internal void OnDocumentMutated()
        {
            m_VisualsDirty = true;
            ApiMethods.VoxMarkSourceDirty(_Document);
            if (m_AutoVisuals)
            {
                Refresh();
            }
        }

        [MoonSharpHidden]
        internal TrTransform GetSceneTransform()
        {
            GameObject root = SceneRoot;
            return root != null ? TrTransform.FromLocalTransform(root.transform) : m_SpawnTransform;
        }

        [MoonSharpHidden]
        internal TrTransform GetModelTransform(RuntimeVoxDocument.RuntimeModel model)
            => GetSceneTransform() * TrTransform.TR(model.TransformOffset, VoxMeshBuilder.ModelRotation);

    }

    [LuaDocsDescription("Runtime VOX document API")]
    [MoonSharpUserData]
    public class VoxApiWrapper
    {
        [LuaDocsDescription("Finds a visible model with an occupied voxel at a canvas position, including models restored from a sketch. Returns nil if none is found.")]
        [LuaDocsExample("local model = Vox:FindModelAt(Brush.position)")]
        public static VoxModelApiWrapper FindModelAt(Vector3 canvasPosition)
        {
            foreach (RuntimeVoxDocument document in ApiMethods.VoxGetVisibleDocuments().Reverse())
            {
                var wrapper = new VoxDocumentApiWrapper(document);
                for (int i = document.Models.Count - 1; i >= 0; i--)
                {
                    VoxModelApiWrapper model = wrapper.models[i];
                    Vector3Int cell = Vector3Int.RoundToInt(model.CanvasToVoxel(canvasPosition));
                    if (model._Model.TryGetPaletteIndex(cell, out _))
                    {
                        return model;
                    }
                }
            }
            return null;
        }

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
