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

        [LuaDocsDescription("The zero-based model index within the containing document")]
        public int index
        {
            get
            {
                for (int i = 0; i < m_Document.Models.Count; i++)
                {
                    if (ReferenceEquals(m_Document.Models[i], _Model))
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

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
        public float voxelSize => Mathf.Abs(m_DocumentWrapper.GetSceneTransform().scale);

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

        [LuaDocsDescription("Returns true when a canvas position maps to a cell inside this model's editable volume")]
        public bool ContainsAt(Vector3 canvasPosition)
            => _Model.IsInBounds(Vector3Int.RoundToInt(CanvasToVoxel(canvasPosition)));

        [LuaDocsDescription("Draws a model-aligned local voxel grid around a canvas position for this frame")]
        public void PreviewAt(Vector3 canvasPosition, Color color)
            => VoxEditPreviewRenderer.Draw(
                _Model,
                m_DocumentWrapper.GetModelTransform(_Model),
                canvasPosition,
                color);

        [MoonSharpHidden]
        internal void PreviewForTool(
            Vector3 canvasPosition,
            Color targetColor,
            Color guideColor)
            => VoxEditPreviewRenderer.Draw(
                _Model,
                m_DocumentWrapper.GetModelTransform(_Model),
                canvasPosition,
                targetColor,
                guideColor);

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

        [LuaDocsDescription("Changes the color of an existing voxel at a canvas position. Returns false if the cell is empty or unchanged; never creates a voxel.")]
        [LuaDocsExample("model:RecolorAt(Brush.position, Brush.colorRgb)")]
        public bool RecolorAt(Vector3 canvasPosition, Color color)
        {
            Vector3Int cell = Vector3Int.RoundToInt(CanvasToVoxel(canvasPosition));
            if (!_Model.TryGetPaletteIndex(cell, out _))
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

        [LuaDocsDescription("Returns the first model (model 0), for simple one-model-per-file use")]
        public VoxModelApiWrapper first => (_Models == null || _Models.Count == 0)
            ? null
            : new VoxModelApiWrapper(m_DocumentWrapper._Document, _Models[0], m_DocumentWrapper);

        [LuaDocsDescription("Returns the model at the given index")]
        public VoxModelApiWrapper this[int index]
            => new VoxModelApiWrapper(
                m_DocumentWrapper._Document,
                Utils.WrappedIndexerGet(() => _Models[index]),
                m_DocumentWrapper);

        [LuaDocsDescription("The number of models")]
        public int count => _Models?.Count ?? 0;
    }

    [LuaDocsDescription("An experimental editable VOX widget document")]
    [MoonSharpUserData]
    public class VoxDocumentApiWrapper
    {
        [MoonSharpHidden] public RuntimeVoxDocument _Document;
        [MoonSharpHidden] private readonly RuntimeVoxDocument.WidgetViewState m_ViewState;

        public VoxDocumentApiWrapper(
            RuntimeVoxDocument document,
            ModelWidget widget = null,
            bool createWidgetOnRefresh = false)
        {
            _Document = document;
            m_ViewState = document.ViewState;
            if (widget != null)
            {
                if (m_ViewState.Widget != null && m_ViewState.Widget != widget)
                {
                    throw new InvalidOperationException(
                        "A VOX document cannot be attached to more than one model widget.");
                }
                m_ViewState.Widget = widget;
                m_ViewState.WidgetWasCreated = true;
            }
            m_ViewState.WidgetBacked |= widget != null || createWidgetOnRefresh;
        }

        [LuaDocsDescription("All models in this VOX document")]
        public VoxModelListApiWrapper models => new VoxModelListApiWrapper(this);

        [LuaDocsDescription("Returns the number of models in this VOX document")]
        public int modelCount => _Document.Models.Count;

        [LuaDocsDescription("Returns a model by index. Omitting the index selects model 0 for simple one-model-per-file use.")]
        public VoxModelApiWrapper Model(int modelIndex = 0)
        {
            if (modelIndex < 0)
            {
                modelIndex += _Document.Models.Count;
            }

            if (modelIndex < 0 || modelIndex >= _Document.Models.Count)
            {
                return null;
            }

            return new VoxModelApiWrapper(_Document, _Document.Models[modelIndex], this);
        }

        [LuaDocsDescription("The default model (model 0), for simple one-model-per-file use")]
        public VoxModelApiWrapper defaultModel => Model();

        [LuaDocsDescription("If true, model/palette edits automatically rebuild scene geometry")]
        public bool autoVisuals => m_ViewState.AutoVisuals;

        [LuaDocsDescription("False after this document's widget has been deleted")]
        public bool isValid => !m_ViewState.WidgetBacked ||
            !m_ViewState.WidgetWasCreated || m_ViewState.Widget != null;

        [MoonSharpHidden]
        internal ModelWidget Widget => m_ViewState.Widget;

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

        [LuaDocsDescription("Shows this document as an editable model widget, rebuilding its mesh if needed")]
        [LuaDocsExample("doc:Show(true)")]
        public void Show(bool optimized = true)
        {
            m_ViewState.OptimizedMesh = optimized;
            if (m_ViewState.WidgetBacked)
            {
                if (!_Document.Models.Any(model => model.Voxels.Count > 0))
                {
                    m_ViewState.Widget?.Hide();
                    m_ViewState.VisualsDirty = false;
                    return;
                }
                if (m_ViewState.Widget == null && m_ViewState.WidgetWasCreated)
                {
                    return;
                }
                if (m_ViewState.Widget == null && !TryCreateWidget())
                {
                    return;
                }
                if (!m_ViewState.Widget.Showing)
                {
                    m_ViewState.Widget.Show(true, false);
                }
                m_ViewState.Widget.RefreshEditableVoxMeshes(optimized);
                m_ViewState.VisualsDirty = false;
                return;
            }
        }

        [LuaDocsDescription("Shows this document as an editable model widget at a canvas position")]
        [LuaDocsExample("doc:ShowAt(0, 0, 0, true)")]
        public void ShowAt(float x, float y, float z, bool optimized = true)
        {
            m_ViewState.OptimizedMesh = optimized;
            var transform = m_ViewState.WidgetTransform;
            transform.translation = new Vector3(x, y, z);
            SetTransform(transform);
            Show(optimized);
        }

        [LuaDocsDescription("Sets this document widget's position, rotation and scale in canvas space")]
        [LuaDocsExample("doc:SetTransform(Transform:New(Vector3:New(0,1,0), 0.1))")]
        public void SetTransform(TrTransform transform)
        {
            if (transform.scale <= 0 || float.IsNaN(transform.scale) || float.IsInfinity(transform.scale))
            {
                throw new ArgumentOutOfRangeException(nameof(transform), "VOX scene scale must be positive and finite.");
            }

            m_ViewState.WidgetTransform = transform;
            if (m_ViewState.Widget != null)
            {
                TrTransform current = GetSceneTransform();
                if (current == transform)
                {
                    return;
                }
                m_ViewState.Widget.SetSignedWidgetSize(transform.scale);
                transform.scale = m_ViewState.Widget.GetSignedWidgetSize();
                App.Scene.ActiveCanvas.AsCanvas[m_ViewState.Widget.transform] = transform;
                m_ViewState.WidgetTransform = GetSceneTransform();
                SaveLoadScript.m_Instance?.SketchChanged();
                return;
            }
        }

        [LuaDocsDescription("Configures automatic mesh updates. When disabled, call Refresh to show pending edits.")]
        [LuaDocsExample("doc:SetAutoVisuals(true, true)")]
        public void SetAutoVisuals(bool enabled = true, bool optimized = true)
        {
            m_ViewState.VisualsDirty |= m_ViewState.OptimizedMesh != optimized;
            m_ViewState.AutoVisuals = enabled;
            m_ViewState.OptimizedMesh = optimized;
            if (enabled)
            {
                Refresh();
            }
        }

        [LuaDocsDescription("Shows pending edits, or shows this document's widget if hidden. Does nothing if the visuals are already current.")]
        public void Refresh()
        {
            if (m_ViewState.VisualsDirty || m_ViewState.Widget == null ||
                !m_ViewState.Widget.Showing)
            {
                Show(m_ViewState.OptimizedMesh);
            }
        }

        [LuaDocsDescription("Hides this document's model widget, if present")]
        public void Hide()
        {
            m_ViewState.WidgetTransform = GetSceneTransform();
            if (m_ViewState.Widget != null)
            {
                m_ViewState.Widget.Show(false, false);
            }
        }

        [MoonSharpHidden]
        internal void OnDocumentMutated()
        {
            m_ViewState.VisualsDirty = true;
            if (m_ViewState.WidgetBacked)
            {
                SaveLoadScript.m_Instance?.SketchChanged();
                if (!_Document.Models.Any(model => model.Voxels.Count > 0))
                {
                    m_ViewState.Widget?.Hide();
                }
            }
            if (m_ViewState.AutoVisuals)
            {
                Refresh();
            }
        }

        [MoonSharpHidden]
        internal TrTransform GetSceneTransform()
        {
            if (m_ViewState.Widget != null)
            {
                return App.Scene.ActiveCanvas.AsCanvas[m_ViewState.Widget.transform];
            }
            return m_ViewState.WidgetTransform;
        }

        [MoonSharpHidden]
        private bool TryCreateWidget()
        {
            if (!_Document.Models.Any(model => model.Voxels.Count > 0))
            {
                return false;
            }

            TiltBrush.Model model = TiltBrush.Model.CreateEditableVoxModelFromBytes(_Document.ToVoxBytes());
            if (model == null)
            {
                throw new InvalidOperationException("Could not create a model for the generated VOX document.");
            }

            ModelWidget widget = null;
            try
            {
                widget = UnityEngine.Object.Instantiate(WidgetManager.m_Instance.ModelWidgetPrefab);
                widget.LoadingFromSketch = true;
                widget.Model = model;
                widget.AdoptEditableVoxDocument(_Document);
                m_ViewState.Widget = widget;
                m_ViewState.WidgetWasCreated = true;
                SetTransform(m_ViewState.WidgetTransform);
                TiltMeterScript.m_Instance.AdjustMeterWithWidget(widget.GetTiltMeterCost(), up: true);
                model.ReleaseFromCatalog();
                return true;
            }
            catch
            {
                if (widget != null)
                {
                    UnityEngine.Object.Destroy(widget.gameObject);
                }
                model.ReleaseFromCatalog();
                m_ViewState.Widget = null;
                m_ViewState.WidgetWasCreated = false;
                throw;
            }
        }

        [MoonSharpHidden]
        internal TrTransform GetModelTransform(RuntimeVoxDocument.RuntimeModel model)
            => GetSceneTransform() * TrTransform.TR(model.TransformOffset, VoxMeshBuilder.ModelRotation);

    }

    [LuaDocsDescription("Experimental editable VOX widget API")]
    [MoonSharpUserData]
    public class VoxApiWrapper
    {
        [LuaDocsDescription("Finds an occupied voxel in a visible editable VOX widget. Returns nil if none is found.")]
        [LuaDocsExample("local model = Vox:FindModelAt(Brush.position)")]
        public static VoxModelApiWrapper FindModelAt(Vector3 canvasPosition)
        {
            if (WidgetManager.m_Instance != null && WidgetManager.m_Instance.IsInitialized)
            {
                foreach (ModelWidget widget in WidgetManager.m_Instance.ModelWidgets.Reverse())
                {
                    RuntimeVoxDocument document = widget.EditableVoxDocument;
                    if (document == null || !widget.Showing || !widget.gameObject.activeInHierarchy ||
                        !string.IsNullOrEmpty(widget.Subtree))
                    {
                        continue;
                    }
                    var wrapper = new VoxDocumentApiWrapper(document, widget);
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
            }
            return null;
        }

        [LuaDocsDescription("Creates an editable document that becomes a normal model widget after its first voxel is painted")]
        [LuaDocsExample("local doc = Vox:NewWidget(16,16,16)")]
        public static VoxDocumentApiWrapper NewWidget(int sizeX, int sizeY, int sizeZ)
        {
            var document = new RuntimeVoxDocument();
            document.CreateModel("model_0", new Vector3Int(sizeX, sizeY, sizeZ));
            return new VoxDocumentApiWrapper(document, createWidgetOnRefresh: true);
        }

    }
}
