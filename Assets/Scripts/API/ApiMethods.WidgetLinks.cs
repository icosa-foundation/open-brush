using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        public static TrTransform GetWidgetTransform(GrabWidget widget) =>
            App.Scene.MainCanvas.Pose.inverse * widget.Canvas.Pose * widget.LocalTransform;

        public static void SetWidgetTransform(GrabWidget widget, TrTransform transform)
        {
            var local = widget.Canvas.Pose.inverse * App.Scene.MainCanvas.Pose * transform;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(widget, local, widget.CustomDimension, true));
        }

        public static void MoveWidgetToLayer(GrabWidget widget, CanvasScript target)
        {
            var group = widget.SymmetryPeerGroup;
            if (group == null) { widget.SetCanvas(target); return; }
            SymmetryMirrors.EndSelectionOwnedBy(group.Mirror);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveSymmetryGroupsToLayerCommand(new SymmetryStrokeGroup[0], target,
                    widgetGroups: new[] { group }));
        }
    }
}
