using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class LayersApiWrapper
    {
        public CanvasScript _CanvasScript;

        public LayersApiWrapper()
        {
            ApiMethods.AddLayer();
            _CanvasScript = active._CanvasScript;
        }

        public LayersApiWrapper(CanvasScript canvasScript)
        {
            _CanvasScript = canvasScript;
        }

        public int index => App.Scene.LayerCanvases.ToList().IndexOf(_CanvasScript);

        // public static LayersApiWrapper New()
        // {
        //     var instance = new LayersApiWrapper();
        //     return instance;
        // }

        public override string ToString()
        {
            return $"Layer({_CanvasScript.name})";
        }

        public CanvasScript this[int index] => App.Scene.GetCanvasByLayerIndex(index);
        public CanvasScript this[string name] => App.Scene.LayerCanvases.First(x => x.name == name);
        public CanvasScript last => this[count - 1];
        public  CanvasScript main => this[0];

        public static int count => App.Scene.LayerCanvases.Count();

        public static LayersApiWrapper active
        {
            get => new(App.Scene.ActiveCanvas);
            set
            {
                ActivateLayerCommand cmd = new ActivateLayerCommand(value._CanvasScript);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
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

        public CanvasScript Squash()
        {
            int destinationIndex = index - 1;
            if (destinationIndex >= 0)
            {
                return SquashTo(this[destinationIndex]);
            }
            return null;
        }

        public CanvasScript SquashTo(CanvasScript destinationLayer)
        {
            SquashLayerCommand cmd = new SquashLayerCommand(
                _CanvasScript,
                destinationLayer
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
