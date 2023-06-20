using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
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

        public int index => App.Scene.LayerCanvases.ToList().IndexOf(_CanvasScript);

        public static LayerApiWrapper New()
        {
            var instance = new LayerApiWrapper();
            return instance;
        }

        public override string ToString()
        {
            return $"Layer({_CanvasScript.name})";
        }

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

        public TrTransform transform
        {
            get => App.Scene.Pose.inverse * _CanvasScript.Pose;
            set
            {
                value = App.Scene.Pose * value;
                _CanvasScript.Pose = value;
            }
        }

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

        public void CenterPivot() => _CanvasScript.CenterPivot();

        public void ShowPivot() => _CanvasScript.ShowGizmo();

        public void HidePivot() =>_CanvasScript.HideGizmo();

        public void Clear()
        {
            ClearLayerCommand cmd = new ClearLayerCommand(_CanvasScript);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        public void Delete()
        {
            DeleteLayerCommand cmd = new DeleteLayerCommand(_CanvasScript);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        public LayerApiWrapper Squash()
        {
            int destinationIndex = index - 1;
            if (destinationIndex >= 0)
            {
                return SquashTo(SketchApiWrapper.layers[destinationIndex]);
            }
            return null;
        }

        public LayerApiWrapper SquashTo(LayerApiWrapper destinationLayer)
        {
            SquashLayerCommand cmd = new SquashLayerCommand(
                _CanvasScript,
                destinationLayer._CanvasScript
            );
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
            return destinationLayer;
        }

        public void Show()
        {
            App.Scene.ShowLayer(_CanvasScript);
        }

        public void Hide()
        {
            App.Scene.HideLayer(_CanvasScript);
        }

        public void Toggle()
        {
            App.Scene.ToggleLayerVisibility(_CanvasScript);
        }
    }
}
