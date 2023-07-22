using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A guide widget")]
    [MoonSharpUserData]
    public class GuideApiWrapper
    {
        public StencilWidget _StencilWidget;

        public GuideApiWrapper(StencilWidget widget)
        {
            _StencilWidget = widget;
        }

        [LuaDocsDescription("The index of the active widget")]
        public int index => WidgetManager.m_Instance.GetActiveWidgetIndex(_StencilWidget);

        [LuaDocsDescription("Returns a string representation of the GuideApiWrapper")]
        [LuaDocsReturnValue("A string representation of the GuideApiWrapper")]
        public override string ToString()
        {
            return $"Guide({_StencilWidget})";
        }

        [LuaDocsDescription("The layer the guide is on")]
        public LayerApiWrapper layer
        {
            get => _StencilWidget != null ? new LayerApiWrapper(_StencilWidget.Canvas) : null;
            set => _StencilWidget.SetCanvas(value._CanvasScript);
        }

        [LuaDocsDescription("The group this guide is part of")]
        public GroupApiWrapper group
        {
            get => _StencilWidget != null ? new GroupApiWrapper(_StencilWidget.Group, layer._CanvasScript) : null;
            set => _StencilWidget.Group = value._Group;
        }

        [LuaDocsDescription("The transform of the Guide Widget")]
        public TrTransform transform
        {
            get => App.Scene.MainCanvas.AsCanvas[_StencilWidget.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_StencilWidget.transform] = value;
            }
        }

        [LuaDocsDescription("The 3D position of the Guide Widget")]
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

        [LuaDocsDescription("The 3D orientation of the Guide Widget")]
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

        [LuaDocsDescription("The scale of the Guide Widget")]
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

        [LuaDocsDescription("Creates a new cube guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewCube(Transform:New(0, 5, 2)")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new cube guide")]
        public static GuideApiWrapper NewCube(TrTransform transform) => _Add(StencilType.Cube, transform);

        [LuaDocsDescription("Creates a new sphere guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewSphere(Transform:New(0, 5, 2)")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new sphere guide")]
        public static GuideApiWrapper NewSphere(TrTransform transform) => _Add(StencilType.Sphere, transform);

        [LuaDocsDescription("Creates a new capsule guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewCapsule(Transform:New(0, 5, 2)")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new capsule guide")]
        public static GuideApiWrapper NewCapsule(TrTransform transform) => _Add(StencilType.Capsule, transform);

        [LuaDocsDescription("Creates a new cone guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewCone(Transform:New(0, 5, 2)")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new cone guide")]
        public static GuideApiWrapper NewCone(TrTransform transform) => _Add(StencilType.Cone, transform);

        [LuaDocsDescription("Creates a new ellipsoid guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewEllipsoid(Transform:New(0, 5, 2)")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new ellipsoid guide")]
        public static GuideApiWrapper NewEllipsoid(TrTransform transform) => _Add(StencilType.Ellipsoid, transform);

        [LuaDocsDescription(@"Creates a new custom guide from a 3d model. Note that custom guides have to be convex so your model will be ""wrapped"" as a convex hull")]
        [LuaDocsExample("myGuide = Guide:NewCustom(Transform:New(0, 5, 2), myModel")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsParameter("model", "The ModelApiWrapper to use for the custom stencil")]
        [LuaDocsReturnValue("A new custom guide based on the convex hull of the model")]
        public static GuideApiWrapper NewCustom(TrTransform transform, ModelApiWrapper model)
        {
            var guide = _Add(StencilType.Custom, transform);
            var customGuide = guide._StencilWidget as CustomStencil;
            if (customGuide == null) return null;
            customGuide.SetCustomStencil(model._ModelWidget.Model.GetMeshes().First().mesh);
            customGuide.SetColliderScale(model._ModelWidget.InitSize_CS);
            return guide;
        }

        [LuaDocsDescription("Adds the guide to the current selection")]
        [LuaDocsExample("myGuide:Select()")]
        public void Select() => ApiMethods.SelectWidget(_StencilWidget);

        [LuaDocsDescription("Deletes the guide")]
        [LuaDocsExample("myGuide:Delete()")]
        public void Delete() => ApiMethods.DeleteWidget(_StencilWidget);

        [LuaDocsDescription("Scales the guide (scale can be non-uniform as some guide types can be stretched)")]
        [LuaDocsExample("myGuide:Scale(Vector3:New(2, 0, 0)")]
        [LuaDocsParameter("scale", "The scale vector for scaling the Guide Widget")]
        public void Scale(Vector3 scale) => SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            new MoveWidgetCommand(_StencilWidget, _StencilWidget.LocalTransform, scale));

        private static GuideApiWrapper _Add(StencilType type, TrTransform tr)
        {
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.GetStencilPrefab(type), tr, forceTransform: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            var widget = createCommand.Widget as StencilWidget;
            return new GuideApiWrapper(widget);
        }
    }
}