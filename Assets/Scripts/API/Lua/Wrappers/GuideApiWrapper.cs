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

        [LuaDocsDescription("Returns a string representation of the Guide")]
        [LuaDocsReturnValue("A string representation of the Guide")]
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

        [LuaDocsDescription("The dimensions of the Guide Widget")]
        public Vector3 dimensions
        {
            get => _StencilWidget.Extents;
            set => _StencilWidget.Extents = value;
        }

        [LuaDocsDescription("The type of the Guide Widget")]
        public string guideType
        {
            get => _StencilWidget.Type.ToString();
        }

        [LuaDocsDescription("The 3D position of the Guide Widget")]
        public Vector3 position
        {
            get => transform.translation;
            set => transform = TrTransform.TRS(value, transform.rotation, transform.scale);
        }

        [LuaDocsDescription("The 3D orientation of the Guide Widget")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set => transform = TrTransform.TRS(transform.translation, value, transform.scale);
        }

        [LuaDocsDescription("The scale of the Guide Widget")]
        public float scale
        {
            get => transform.scale;
            set => transform = TrTransform.TRS(transform.translation, transform.rotation, value);
        }

        [LuaDocsDescription("Creates a new cube guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewCube(Transform:New(0, 5, 2))")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new cube guide")]
        public static GuideApiWrapper NewCube(TrTransform transform) => _Add(StencilType.Cube, transform);

        [LuaDocsDescription("Creates a new sphere guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewSphere(Transform:New(0, 5, 2))")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new sphere guide")]
        public static GuideApiWrapper NewSphere(TrTransform transform) => _Add(StencilType.Sphere, transform);

        [LuaDocsDescription("Creates a new capsule guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewCapsule(Transform:New(0, 5, 2))")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new capsule guide")]
        public static GuideApiWrapper NewCapsule(TrTransform transform) => _Add(StencilType.Capsule, transform);

        [LuaDocsDescription("Creates a new plane guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewPlane(Transform:New(0, 5, 2))")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new plane guide")]
        public static GuideApiWrapper NewPlane(TrTransform transform) => _Add(StencilType.Plane, transform);

        [LuaDocsDescription("Creates a new ellipsoid guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewEllipsoid(Transform:New(0, 5, 2))")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new ellipsoid guide")]
        public static GuideApiWrapper NewEllipsoid(TrTransform transform) => _Add(StencilType.Ellipsoid, transform);

        [LuaDocsDescription("Creates a new SDF guide with a default size using the transform for position and orientation")]
        [LuaDocsExample("myGuide = Guide:NewSDF(Transform:New(0, 5, 2))")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new SDF guide")]
        public static GuideApiWrapper NewSDF(TrTransform transform) => _AddSdf(transform);

        [LuaDocsDescription("Creates an empty SDF guide whose primitives can be defined at runtime")]
        [LuaDocsExample("myGuide = Guide:NewCustomSDF(Transform:New(0, 5, 2))")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsReturnValue("A new empty, editable SDF guide")]
        public static GuideApiWrapper NewCustomSDF(TrTransform transform)
        {
            GuideApiWrapper guide = _AddSdf(transform);
            guide.GetSdfStencil().ClearPrimitives();
            return guide;
        }

        [LuaDocsDescription(@"Creates a new custom guide from a 3d model. Note that custom guides have to be convex so your model will be ""wrapped"" as a convex hull")]
        [LuaDocsExample("myGuide = Guide:NewCustom(Transform:New(0, 5, 2), myModel)")]
        [LuaDocsParameter("transform", "The transform of the Guide Widget")]
        [LuaDocsParameter("model", "The Model to use for the custom guide")]
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

        [LuaDocsDescription("Removes the guide from the current selection")]
        [LuaDocsExample("myGuide:Deselect()")]
        public void Deselect() => ApiMethods.DeselectWidget(_StencilWidget);

        [LuaDocsDescription("Deletes the guide")]
        [LuaDocsExample("myGuide:Delete()")]
        public void Delete() => ApiMethods.DeleteWidget(_StencilWidget);

        [LuaDocsDescription("Scales the guide (scale can be non-uniform as some guide types can be stretched)")]
        [LuaDocsExample("myGuide:Scale(Vector3:New(2, 0, 0))")]
        [LuaDocsParameter("scale", "The scale vector for scaling the Guide Widget")]
        public void Scale(Vector3 scale) => SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            new MoveWidgetCommand(_StencilWidget, _StencilWidget.LocalTransform, scale));

        [LuaDocsDescription("Returns the closest point on the guide surface and its orientation")]
        [LuaDocsExample("closest = myGuide:ClosestPoint(Vector3:New(2, 3, 4))")]
        [LuaDocsParameter("point", "The point to project onto the guide surface")]
        [LuaDocsReturnValue("A transform whose position is on the guide and whose up axis follows the surface normal")]
        public TransformApiWrapper ClosestPoint(Vector3 point)
        {
            TrTransform canvasPose = App.Scene.ActiveCanvas.Pose;
            Vector3 point_GS = (canvasPose * TrTransform.T(point)).translation;
            ApiMethods.FindClosestPointOnGuide(
                _StencilWidget, point_GS, out Vector3 closestPoint_GS, out Vector3 normal_GS);
            Vector3 closestPoint_CS =
                (canvasPose.inverse * TrTransform.T(closestPoint_GS)).translation;
            Vector3 normal_CS = Quaternion.Inverse(canvasPose.rotation) * normal_GS;
            Quaternion rotation = normal_CS.sqrMagnitude > 0.000001f
                ? Quaternion.FromToRotation(Vector3.up, normal_CS.normalized)
                : Quaternion.identity;
            return new TransformApiWrapper(closestPoint_CS, rotation);
        }

        [LuaDocsDescription("Returns the signed distance from a point to this guide; negative values are inside")]
        [LuaDocsExample("distance = myGuide:SignedDistance(Vector3:New(2, 3, 4))")]
        [LuaDocsParameter("point", "The point to measure from")]
        [LuaDocsReturnValue("The signed distance to the guide surface")]
        public float SignedDistance(Vector3 point)
        {
            TrTransform canvasPose = App.Scene.ActiveCanvas.Pose;
            Vector3 point_GS = (canvasPose * TrTransform.T(point)).translation;
            return ApiMethods.GetSignedDistanceToGuide(_StencilWidget, point_GS) /
                   canvasPose.scale;
        }

        [LuaDocsDescription("Steps in a direction and projects the result back onto this guide surface")]
        [LuaDocsExample("nextPoint = myGuide:NextPointOnSurface(point, 0.1, Vector3.forward)")]
        [LuaDocsParameter("point", "The current point on or near the guide surface")]
        [LuaDocsParameter("stepDistance", "The distance to step before projecting back to the surface")]
        [LuaDocsParameter("direction", "The direction in which to step")]
        [LuaDocsReturnValue("The next point on this guide surface")]
        public Vector3 NextPointOnSurface(
            Vector3 point, float stepDistance, Vector3 direction)
        {
            TrTransform canvasPose = App.Scene.ActiveCanvas.Pose;
            Vector3 point_GS = (canvasPose * TrTransform.T(point)).translation;
            Vector3 direction_GS = canvasPose.rotation * direction;
            Vector3 result_GS = ApiMethods.GetNextPointOnGuideSurfaces(
                new[] { _StencilWidget }, point_GS,
                stepDistance * canvasPose.scale, direction_GS);
            return (canvasPose.inverse * TrTransform.T(result_GS)).translation;
        }

        [LuaDocsDescription("Converts a canvas-space transform to a transform relative to this guide")]
        [LuaDocsExample("localTransform = myGuide:ToLocalTransform(Transform:New(Brush.position, Brush.rotation, 1))")]
        [LuaDocsParameter("canvasTransform", "The transform in active-canvas space")]
        [LuaDocsReturnValue("The transform in guide-local space")]
        public TrTransform ToLocalTransform(TrTransform canvasTransform)
        {
            TrTransform transform_GS = App.Scene.ActiveCanvas.Pose * canvasTransform;
            TrTransform guide_GS = TrTransform.FromTransform(_StencilWidget.transform);
            return guide_GS.inverse * transform_GS;
        }

        [LuaDocsDescription("Converts a guide-local transform to active-canvas space")]
        [LuaDocsExample("canvasTransform = myGuide:ToCanvasTransform(localTransform)")]
        [LuaDocsParameter("localTransform", "The transform in guide-local space")]
        [LuaDocsReturnValue("The transform in active-canvas space")]
        public TrTransform ToCanvasTransform(TrTransform localTransform)
        {
            TrTransform guide_GS = TrTransform.FromTransform(_StencilWidget.transform);
            return App.Scene.ActiveCanvas.Pose.inverse * guide_GS * localTransform;
        }

        [LuaDocsDescription("The number of primitives in this SDF guide")]
        public int primitiveCount => GetSdfStencil().PrimitiveCount;

        [LuaDocsDescription("Adds a primitive to this SDF guide")]
        [LuaDocsExample("primitive = myGuide:AddPrimitive(\"sphere\", Vector4:New(1, 0, 0, 0), Transform:New(0, 0, 0), \"union\", 0)")]
        [LuaDocsParameter("primitiveType", "sphere, torus, cuboid, boxframe, or cylinder")]
        [LuaDocsParameter("geometry", "Primitive dimensions: sphere radius; torus radii; cuboid half-extents; boxframe half-extents and thickness; or cylinder radius and half-height")]
        [LuaDocsParameter("transform", "The primitive transform relative to the guide")]
        [LuaDocsParameter("operation", "union, subtract, or intersect")]
        [LuaDocsParameter("blend", "The non-negative smoothing distance for the operation")]
        [LuaDocsReturnValue("The new editable SDF primitive")]
        public SdfPrimitiveApiWrapper AddPrimitive(
            string primitiveType, Vector4ApiWrapper geometry, TrTransform transform,
            string operation, float blend)
        {
            SdfStencil stencil = GetSdfStencil();
            var primitive = stencil.AddPrimitive(
                SdfStencil.ParsePrimitiveType(primitiveType), geometry._Vector4, transform,
                SdfStencil.ParseOperation(operation), blend);
            return new SdfPrimitiveApiWrapper(stencil, primitive);
        }

        [LuaDocsDescription("Returns a primitive from this SDF guide; negative indexes count from the end")]
        [LuaDocsParameter("index", "The zero-based primitive index")]
        [LuaDocsReturnValue("The editable SDF primitive")]
        public SdfPrimitiveApiWrapper GetPrimitive(int index)
        {
            SdfStencil stencil = GetSdfStencil();
            return new SdfPrimitiveApiWrapper(stencil, stencil.GetPrimitive(index));
        }

        [LuaDocsDescription("Removes every primitive from this SDF guide")]
        public void ClearPrimitives()
        {
            GetSdfStencil().ClearPrimitives();
        }

        private SdfStencil GetSdfStencil()
        {
            if (!(_StencilWidget is SdfStencil stencil))
            {
                throw new System.InvalidOperationException(
                    "SDF primitives are only available on SDF guides.");
            }
            return stencil;
        }

        private static GuideApiWrapper _Add(StencilType type, TrTransform tr)
        {
            return _Add(WidgetManager.m_Instance.GetStencilPrefab(type), tr);
        }

        private static GuideApiWrapper _AddSdf(TrTransform tr)
        {
            return _Add(WidgetManager.m_Instance.SdfStencilPrefab, tr);
        }

        private static GuideApiWrapper _Add(StencilWidget prefab, TrTransform tr)
        {
            var cmd = new CreateWidgetCommand(prefab, tr, forceTransform: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
            var widget = cmd.Widget as StencilWidget;
            return new GuideApiWrapper(widget);
        }
    }
}
