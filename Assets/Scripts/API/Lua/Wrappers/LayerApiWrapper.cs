using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A layer in the current sketch")]
    [MoonSharpUserData]
    public class LayerApiWrapper
    {
        public CanvasScript _CanvasScript;

        public LayerApiWrapper()
        {
            ApiMethods.AddLayer();
            _CanvasScript = App.Scene.ActiveCanvas;
        }

        public LayerApiWrapper(CanvasScript canvasScript)
        {
            _CanvasScript = canvasScript;
        }

        [LuaDocsDescription("All the strokes on this layer")]
        public StrokeListApiWrapper strokes {
            get
            {
                var batches = _CanvasScript.BatchManager.AllBatches();
                var subsets = batches.SelectMany(x => x.m_Groups);
                // Only supports batched brush stroke - which is all we use now
                return new StrokeListApiWrapper(subsets.Select(x => x.m_Stroke).Where(x => x.m_Type == Stroke.Type.BatchedBrushStroke).ToList());
            }
        }

        [LuaDocsDescription("All the images on this layer")]
        public ImageListApiWrapper images {
            get
            {
                var imageWidgets = _CanvasScript.transform.GetComponentsInChildren<ImageWidget>().Where(w => w.gameObject.activeSelf);
                return new ImageListApiWrapper(imageWidgets.ToList());
            }
        }

        [LuaDocsDescription("All the videos on this layer")]
        public VideoListApiWrapper videos {
            get
            {
                var videoWidgets = _CanvasScript.transform.GetComponentsInChildren<VideoWidget>().Where(w => w.gameObject.activeSelf);
                return new VideoListApiWrapper(videoWidgets.ToList());
            }
        }

        [LuaDocsDescription("All the models on this layer")]
        public ModelListApiWrapper models {
            get
            {
                var modelWidgets = _CanvasScript.transform.GetComponentsInChildren<ModelWidget>().Where(w => w.gameObject.activeSelf);
                return new ModelListApiWrapper(modelWidgets.ToList());
            }
        }

        [LuaDocsDescription("All the guides on this layer")]
        public GuideListApiWrapper guides {
            get
            {
                var guideWidgets = _CanvasScript.transform.GetComponentsInChildren<StencilWidget>().Where(w => w.gameObject.activeSelf);
                return new GuideListApiWrapper(guideWidgets.ToList());
            }
        }

        [LuaDocsDescription("All the camera paths on this layer")]
        public CameraPathListApiWrapper cameraPaths {
            get
            {
                var cameraPathWidgets = _CanvasScript.transform.GetComponentsInChildren<CameraPathWidget>().Where(w => w.gameObject.activeSelf);
                return new CameraPathListApiWrapper(cameraPathWidgets.ToList());
            }
        }

        [LuaDocsDescription("All the groups on this layer")]
        public List<GroupApiWrapper> groups {
            get
            {
                var tags = new HashSet<SketchGroupTag>();
                tags.UnionWith(strokes._Strokes.Select(x => x.Group));
                tags.UnionWith(images._Images.Select(x => x.Group));
                tags.UnionWith(videos._Videos.Select(x => x.Group));
                tags.UnionWith(models._Models.Select(x => x.Group));
                tags.UnionWith(guides._Guides.Select(x => x.Group));
                tags.UnionWith(cameraPaths._CameraPaths.Select(x => x.Group));
                return tags.Select(x => new GroupApiWrapper(x, _CanvasScript)).ToList();
            }
        }

        [LuaDocsDescription("Gets the index of the layer in the layer canvases")]
        public int index => App.Scene.LayerCanvases.ToList().IndexOf(_CanvasScript);

        [LuaDocsDescription("Creates a new layer")]
        [LuaDocsExample(@"myLayer = Layer:New()")]
        [LuaDocsReturnValue("The new layer")]
        public static LayerApiWrapper New()
        {
            var instance = new LayerApiWrapper();
            return instance;
        }

        [LuaDocsDescription("Returns a string that represents the current layer")]
        [LuaDocsReturnValue("A string that represents the current layer")]
        public override string ToString()
        {
            return $"Layer({_CanvasScript.name})";
        }

        [LuaDocsDescription("Is the layer the active layer. Making another layer inactive will make the main layer the active layer again.")]
        public bool active
        {
            get => App.Scene.ActiveCanvas == _CanvasScript;
            set
            {
                if (value)
                {
                    App.Scene.ActiveCanvas = _CanvasScript;
                }
                else if (active) // Only switch to the main layer if this layer already is the active layer
                {
                    App.Scene.ActiveCanvas = App.Scene.MainCanvas;
                }
            }
        }

        [LuaDocsDescription("The transform of the layer")]
        public TrTransform transform
        {
            get => App.Scene.Pose.inverse * _CanvasScript.Pose;
            set
            {
                value = App.Scene.Pose * value;
                _CanvasScript.Pose = value;
            }
        }

        [LuaDocsDescription("The 3D position of the Layer (specifically the position of it's anchor point")]
        public Vector3 position
        {
            get => transform.translation;
            set => transform = TrTransform.TRS(value, transform.rotation, transform.scale);
        }

        [LuaDocsDescription("The rotation of the layer in 3D space")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set => transform = TrTransform.TRS(transform.translation, value, transform.scale);
        }

        [LuaDocsDescription("The scale of the layer")]
        public float scale
        {
            get => transform.scale;
            set => transform = TrTransform.TRS(transform.translation, transform.rotation, value);
        }

        [LuaDocsDescription("Move the pivot point of the layer to the average center of it's contents")]
        [LuaDocsExample(@"myLayer:CenterPivot()")]
        public void CenterPivot() => _CanvasScript.CenterPivot();

        [LuaDocsDescription("Shows a visible widget indicating the pivot point of the layer")]
        [LuaDocsExample(@"myLayer:ShowPivot()")]
        public void ShowPivot() => _CanvasScript.ShowGizmo();

        [LuaDocsDescription("Hides the visible widget indicating the pivot point of the layer")]
        [LuaDocsExample(@"myLayer:HidePivot()")]
        public void HidePivot() => _CanvasScript.HideGizmo();

        [LuaDocsDescription("Deletes all content from the layer")]
        [LuaDocsExample(@"myLayer:Clear()")]
        public void Clear()
        {
            ClearLayerCommand cmd = new ClearLayerCommand(_CanvasScript);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [LuaDocsDescription("Deletes the layer and all it's content")]
        [LuaDocsExample(@"myLayer:Delete()")]
        public void Delete()
        {
            DeleteLayerCommand cmd = new DeleteLayerCommand(_CanvasScript);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [LuaDocsDescription("Combines this layer and the one above it. If this layer is the first layer do nothing")]
        [LuaDocsReturnValue("The resulting LayerApiWrapper instance")]
        [LuaDocsExample(@"combinedLayer = myLayer:Squash()")]
        public LayerApiWrapper Squash()
        {
            int destinationIndex = index - 1;
            if (destinationIndex >= 0)
            {
                return _SquashTo(SketchApiWrapper.layers[destinationIndex]);
            }
            return this;
        }

        [LuaDocsDescription("Combines this layer with the specified layer")]
        [LuaDocsParameter("destinationLayer", "The destination layer")]
        [LuaDocsExample(@"myLayer:SquashTo(otherLayer)")]
        public void SquashTo(LayerApiWrapper destinationLayer)
        {
            _SquashTo(destinationLayer);
        }

        private LayerApiWrapper _SquashTo(LayerApiWrapper destinationLayer)
        {
            SquashLayerCommand cmd = new SquashLayerCommand(
                _CanvasScript,
                destinationLayer._CanvasScript
            );
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
            return destinationLayer;
        }

        [LuaDocsDescription("Shows the layer")]
        [LuaDocsExample(@"myLayer:Show()")]
        public void Show()
        {
            App.Scene.ShowLayer(_CanvasScript);
        }

        [LuaDocsDescription("Hides the layer")]
        [LuaDocsExample(@"myLayer:Hide()")]
        public void Hide()
        {
            App.Scene.HideLayer(_CanvasScript);
        }

        public IEnumerable<Batch> _GetBatches(BrushDescriptor desc)
        {
            return _CanvasScript.BatchManager.AllBatches().Where(b => b.Brush == desc);
        }

        public BrushDescriptor _GetDesc(string brushType)
        {
            return ApiMethods.LookupBrushDescriptor(brushType);
        }

        [LuaDocsDescription("Changes a shader float parameter. Affects all strokes on this layer of the given brush type")]
        public void SetShaderFloat(string brushType, string parameter, float value)
        {
            var desc = _GetDesc(brushType);
            if (desc == null || !desc.Material.HasFloat(parameter)) return;
            foreach (var batch in _GetBatches(desc))
            {
                batch.InstantiatedMaterial.SetFloat(parameter, value);
            }
        }

        [LuaDocsDescription("Changes a shader color parameter. Affects all strokes on this layer of the given brush type")]
        public void SetShaderColor(string brushType, string parameter, ColorApiWrapper color)
        {
            var desc = _GetDesc(brushType);
            if (_GetDesc(brushType) == null || !_GetDesc(brushType).Material.HasColor(parameter)) return;
            foreach (var batch in _GetBatches(desc))
            {
                batch.InstantiatedMaterial.SetColor(parameter, color._Color);
            }
        }

        [LuaDocsDescription("Changes a shader texture parameter. Affects all strokes on this layer of the given brush type")]
        public void SetShaderTexture(string brushType, string parameter, ImageApiWrapper image)
        {
            var desc = _GetDesc(brushType);
            if (_GetDesc(brushType) == null || !_GetDesc(brushType).Material.HasTexture(parameter)) return;
            foreach (var batch in _GetBatches(desc))
            {
                batch.InstantiatedMaterial.SetTexture(parameter, image._ImageWidget.ReferenceImage.FullSize);
            }
        }

        [LuaDocsDescription("Changes a shader vector parameter. Affects all strokes on this layer of the given brush type")]
        public void SetShaderVector(string brushType, string parameter, Vector3ApiWrapper vector, float w = 0)
        {
            var desc = _GetDesc(brushType);
            if (_GetDesc(brushType) == null || !_GetDesc(brushType).Material.HasVector(parameter)) return;
            foreach (var batch in _GetBatches(desc))
            {
                var vec = (Vector4)vector._Vector3;
                vec.w = w;
                batch.InstantiatedMaterial.SetVector(parameter, vec);
            }
        }
    }
}