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

        [LuaDocsDescription("Gets the index of the layer in the layer canvases")]
        public int index => App.Scene.LayerCanvases.ToList().IndexOf(_CanvasScript);

        [LuaDocsDescription("Creates and returns a new instance of a Layer")]
        [LuaDocsExample(@"Layer:New()")]
        [LuaDocsReturnValue("The new instance of LayerApiWrapper")]
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

        [LuaDocsDescription("Gets or sets a value indicating whether the layer is active")]
        public bool active
        {
            get => App.Scene.ActiveCanvas == _CanvasScript;
            set
            {
                if (value)
                {
                    App.Scene.ActiveCanvas = _CanvasScript;
                }
                else if (active)
                {
                    App.Scene.ActiveCanvas = App.Scene.MainCanvas;
                }
            }
        }

        [LuaDocsDescription("Gets or sets the transform of the layer")]
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
            get => (App.Scene.Pose.inverse * _CanvasScript.Pose).translation;
            set
            {
                var tr = _CanvasScript.Pose;
                var newTransform = TrTransform.T(value);
                newTransform = App.Scene.Pose * newTransform;
                tr.translation = newTransform.translation;
                _CanvasScript.Pose = tr;
            }
        }

        [LuaDocsDescription("Gets or sets the rotation of the layer in 3D space")]
        public Quaternion rotation
        {
            get => (App.Scene.Pose.inverse * _CanvasScript.Pose).rotation;
            set
            {
                var tr = _CanvasScript.Pose;
                var newTransform = TrTransform.R(value);
                newTransform = App.Scene.Pose * newTransform;
                tr.rotation = newTransform.rotation;
                _CanvasScript.Pose = tr;
            }
        }

        [LuaDocsDescription("Gets or sets the scale of the layer")]
        public float scale
        {
            get => transform.scale;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.S(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.scale = newTransform.scale;
                transform = tr_CS;
            }
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
    }
}