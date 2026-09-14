using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("The list of Guides in the scene. (You don't instantiate this yourself. Access this via Sketch.guides)")]
    [MoonSharpUserData]
    public class GuideListApiWrapper
    {
        [MoonSharpHidden]
        public List<StencilWidget> _Guides;

        [LuaDocsDescription("Returns the last guide that was selected")]
        public GuideApiWrapper lastSelected
        {
            get
            {
                StencilWidget stencil = SelectionManager.m_Instance.LastSelectedStencil;
                return stencil == null ? null : new GuideApiWrapper(stencil);
            }
        }

        [LuaDocsDescription("Returns the last Guide")]
        public GuideApiWrapper last => (_Guides == null || _Guides.Count == 0) ? null : new GuideApiWrapper(_Guides[^1]);

        public GuideListApiWrapper()
        {
            _Guides = new List<StencilWidget>();
        }

        public GuideListApiWrapper(List<StencilWidget> guides)
        {
            _Guides = guides;
        }

        [LuaDocsDescription(@"Gets or sets the state of ""Enable guides""")]
        public bool enabled
        {
            get => WidgetManager.m_Instance.StencilsDisabled;
            set => WidgetManager.m_Instance.StencilsDisabled = value;
        }

        [LuaDocsDescription("Returns the guide at the specified index")]
        public GuideApiWrapper this[int index] => new(Utils.WrappedIndexerGet(() => _Guides[index]));

        [LuaDocsDescription("The number of guides")]
        public int count => _Guides?.Count ?? 0;

        [LuaDocsDescription("Returns the signed distance from a point to the combined volume of these guides; negative values are inside")]
        [LuaDocsExample("distance = Sketch.guides:SignedDistance(Vector3:New(2, 3, 4))")]
        [LuaDocsParameter("point", "The point to measure from")]
        [LuaDocsReturnValue("The signed distance to the closest contributing guide, or positive infinity when the list is empty")]
        public float SignedDistance(Vector3 point)
        {
            TrTransform canvasPose = App.Scene.ActiveCanvas.Pose;
            Vector3 point_GS = (canvasPose * TrTransform.T(point)).translation;
            return ApiMethods.GetSignedDistanceToGuides(_Guides, point_GS) /
                   canvasPose.scale;
        }

        [LuaDocsDescription("Steps in a direction and projects the result onto the combined surface of these guides")]
        [LuaDocsExample("nextPoint = Sketch.guides:NextPointOnSurface(point, 0.1, Vector3.forward)")]
        [LuaDocsParameter("point", "The current point on or near the guide surface")]
        [LuaDocsParameter("stepDistance", "The distance to step before projecting back to the surface")]
        [LuaDocsParameter("direction", "The direction in which to step")]
        [LuaDocsReturnValue("The next point on the combined guide surface, or the input point when the list is empty")]
        public Vector3 NextPointOnSurface(
            Vector3 point, float stepDistance, Vector3 direction)
        {
            TrTransform canvasPose = App.Scene.ActiveCanvas.Pose;
            Vector3 point_GS = (canvasPose * TrTransform.T(point)).translation;
            Vector3 direction_GS = canvasPose.rotation * direction;
            Vector3 result_GS = ApiMethods.GetNextPointOnGuideSurfaces(
                _Guides, point_GS, stepDistance * canvasPose.scale, direction_GS);
            return (canvasPose.inverse * TrTransform.T(result_GS)).translation;
        }

    }
}
