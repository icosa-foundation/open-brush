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

        [LuaDocsDescription("Centers the pivot of the layer")]
        public void CenterPivot() => _CanvasScript.CenterPivot();

        [LuaDocsDescription("Shows the pivot of the layer")]
        public void ShowPivot() => _CanvasScript.ShowGizmo();

        [LuaDocsDescription("Hides the pivot of the layer")]
        public void HidePivot() => _CanvasScript.HideGizmo();

        [LuaDocsDescription("Clears the layer")]
        public void Clear()
        {
            ClearLayerCommand cmd = new ClearLayerCommand(_CanvasScript);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [LuaDocsDescription("Deletes the layer")]
        public void Delete()
        {
            DeleteLayerCommand cmd = new DeleteLayerCommand(_CanvasScript);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [LuaDocsDescription("Squashes the layer and returns the resulting LayerApiWrapper instance")]
        [LuaDocsReturnValue("The resulting LayerApiWrapper instance")]
        public LayerApiWrapper Squash()
        {
            int destinationIndex = index - 1;
            if (destinationIndex >= 0)
            {
                return SquashTo(SketchApiWrapper.layers[destinationIndex]);
            }
            return null;
        }

        [LuaDocsDescription("Squashes the layer to the specified destination layer and returns the destination layer")]
        [LuaDocsParameter("destinationLayer", "The destination layer")]
        [LuaDocsReturnValue("The destination layer")]
        public LayerApiWrapper SquashTo(LayerApiWrapper destinationLayer)
        {
            SquashLayerCommand cmd = new SquashLayerCommand(
                _CanvasScript,
                destinationLayer._CanvasScript
            );
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
            return destinationLayer;
        }

        [LuaDocsDescription("Shows the layer")]
        public void Show()
        {
            App.Scene.ShowLayer(_CanvasScript);
        }

        [LuaDocsDescription("Hides the layer")]
        public void Hide()
        {
            App.Scene.HideLayer(_CanvasScript);
        }

        [LuaDocsDescription("Toggles the visibility of the layer")]
        public void Toggle()
        {
            App.Scene.ToggleLayerVisibility(_CanvasScript);
        }
    }
}