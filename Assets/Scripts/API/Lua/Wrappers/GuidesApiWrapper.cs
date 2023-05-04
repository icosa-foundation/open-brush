using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class GuidesApiWrapper
    {

        public StencilWidget _StencilWidget;

        public GuidesApiWrapper(StencilWidget widget)
        {
            _StencilWidget = widget;
        }

        public int index => WidgetManager.m_Instance.GetActiveWidgetIndex(_StencilWidget);

        public override string ToString()
        {
            return $"CameraPath({_StencilWidget})";
        }

        public StencilWidget this[int index] => WidgetManager.m_Instance.ActiveStencilWidgets[index].WidgetScript;
        public StencilWidget last => this[count - 1];

        public static int count => WidgetManager.m_Instance.ActiveStencilWidgets.Count;

        public TrTransform transform
        {
            get =>  App.Scene.MainCanvas.AsCanvas[_StencilWidget.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_StencilWidget.transform] = value;
            }
        }

        public Vector3 position
        {
            get => transform.translation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.T(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.translation = newTransform.translation;
                transform = tr_CS;
            }
        }

        public Quaternion rotation
        {
            get => transform.rotation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.R(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.rotation = newTransform.rotation;
                transform = tr_CS;
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

        public static StencilWidget AddCube(TrTransform tr) => _Add(StencilType.Cube, tr);
        public static StencilWidget AddSphere(TrTransform tr) => _Add(StencilType.Sphere, tr);
        public static StencilWidget AddCapsule(TrTransform tr) => _Add(StencilType.Capsule, tr);
        public static StencilWidget AddCone(TrTransform tr) => _Add(StencilType.Cone, tr);
        public static StencilWidget AddEllipsoid(TrTransform tr) => _Add(StencilType.Ellipsoid, tr);
        public void Select() => ApiMethods.SelectGuide(index);
        public void Scale(Vector3 scale) => ApiMethods.ScaleGuide(index, scale);
        public void Toggle() => ApiMethods.StencilsDisable();

        private static StencilWidget _Add(StencilType type, TrTransform tr)
        {
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.GetStencilPrefab(type), tr);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            return createCommand.Widget as StencilWidget;
        }
    }
}
