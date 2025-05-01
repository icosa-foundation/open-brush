using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A 3D model widget")]
    [MoonSharpUserData]
    public class ModelApiWrapper
    {

        public ModelWidget _ModelWidget;

        public ModelApiWrapper(ModelWidget widget)
        {
            _ModelWidget = widget;
        }

        [LuaDocsDescription(@"The index of the active Model Widget")]
        public int index => WidgetManager.m_Instance.GetActiveWidgetIndex(_ModelWidget);

        public override string ToString()
        {
            return $"Model({_ModelWidget})";
        }

        [LuaDocsDescription("The layer the model is on")]
        public LayerApiWrapper layer
        {
            get => _ModelWidget != null ? new LayerApiWrapper(_ModelWidget.Canvas) : null;
            set => _ModelWidget.SetCanvas(value._CanvasScript);
        }

        [LuaDocsDescription("The group this model is part of")]
        public GroupApiWrapper group
        {
            get => _ModelWidget != null ? new GroupApiWrapper(_ModelWidget.Group, layer._CanvasScript) : null;
            set => _ModelWidget.Group = value._Group;
        }

        [LuaDocsDescription(@"The transformation of the Model Widget")]
        public TrTransform transform
        {
            get => App.Scene.MainCanvas.AsCanvas[_ModelWidget.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_ModelWidget.transform] = value;
            }
        }

        [LuaDocsDescription(@"The 3D position of the Model Widget")]
        public Vector3 position
        {
            get => transform.translation;
            set => transform = TrTransform.TRS(value, transform.rotation, transform.scale);
        }

        [LuaDocsDescription(@"The 3D orientation of the Model Widget")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set => transform = TrTransform.TRS(transform.translation, value, transform.scale);
        }

        [LuaDocsDescription(@"The scale of the Model Widget")]
        public float scale
        {
            get => transform.scale;
            set => transform = TrTransform.TRS(transform.translation, transform.rotation, value);
        }

        [LuaDocsDescription(@"Imports a new model from the MediaLibrary/Models folder")]
        [LuaDocsExample(@"Model:Import(""Andy.obj"")")]
        [LuaDocsParameter(@"filename", "The filename of the model to be imported")]
        [LuaDocsReturnValue(@"Returns the Model instance")]
        public static ModelApiWrapper Import(string filename) => new ModelApiWrapper(ApiMethods.ImportModel(filename));

        [LuaDocsDescription(@"Adds this model to the current selection")]
        [LuaDocsExample(@"myModel:Select()")]
        public void Select() => ApiMethods.SelectWidget(_ModelWidget);

        [LuaDocsDescription("Removes the model from the current selection")]
        [LuaDocsExample("myModel:Deselect()")]
        public void Deselect() => ApiMethods.DeselectWidget(_ModelWidget);

        [LuaDocsDescription(@"Deletes this model")]
        [LuaDocsExample(@"myModel:Delete()")]
        public void Delete() => ApiMethods.DeleteWidget(_ModelWidget);
    }
}
