using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace TiltBrush
{
    static public class TransformItems
    {
        public static void Transform(IEnumerable<Stroke> strokes, IEnumerable<GrabWidget> widgets,
                                     Vector3 pivot, TrTransform xf)
        {
            var strokeLayers = new Dictionary<Stroke, CanvasScript>();
            var widgetLayers = new Dictionary<GrabWidget, CanvasScript>();
            var tempLayer = App.Scene.AddLayerNow();

            foreach (var stroke in strokes)
            {
                strokeLayers[stroke] = stroke.Canvas;
                stroke.SetParentKeepWorldPosition(tempLayer);
            }

            foreach (var widget in widgets)
            {
                widgetLayers[widget] = widget.Canvas;
                widget.transform.SetParent(tempLayer.transform, true);
            }

            tempLayer.Pose *= TrTransform.T(pivot);
            tempLayer.Pose *= xf;
            tempLayer.Pose *= TrTransform.T(-pivot);

            foreach (var stroke in strokes) stroke.SetParentKeepWorldPosition(strokeLayers[stroke], tempLayer.Pose);
            foreach (var widget in widgets) widget.transform.SetParent(widgetLayers[widget].transform, true);
            App.Scene.DestroyLayer(tempLayer);
        }

        public static void TransformSelected(Vector3 mPivot, TrTransform xf)
        {
            var tempLayer = App.Scene.AddLayerNow();
            tempLayer.Pose = SelectionManager.m_Instance.SelectionTransform;

            var strokes = SelectionManager.m_Instance.SelectedStrokes;
            var widgets = SelectionManager.m_Instance.GetValidSelectedWidgets();

            foreach (var stroke in strokes) stroke.SetParentKeepWorldPosition(tempLayer);
            foreach (var widget in widgets) widget.transform.SetParent(tempLayer.transform, true);

            tempLayer.Pose *= TrTransform.T(mPivot);
            tempLayer.Pose *= xf;
            tempLayer.Pose *= TrTransform.T(-mPivot);

            foreach (var stroke in strokes) stroke.SetParentKeepWorldPosition(App.Scene.SelectionCanvas, tempLayer.Pose);
            foreach (var widget in widgets) widget.transform.SetParent(App.Scene.SelectionCanvas.transform, true);
            App.Scene.DestroyLayer(tempLayer);
        }

        public static void TransformList(List<Stroke> strokes, List<GrabWidget> widgets, List<TrTransform> xforms)
        {
            var strokeLayers = new Dictionary<Stroke, CanvasScript>();
            var widgetLayers = new Dictionary<GrabWidget, CanvasScript>();
            var tempLayer = App.Scene.AddLayerNow();

            for (var i = 0; i < xforms.Count; i++)
            {
                TrTransform pivot = TrTransform.identity;
                Stroke stroke = null;
                GrabWidget widget = null;

                if (i < strokes.Count)
                {
                    stroke = strokes[i];
                    strokeLayers[stroke] = stroke.Canvas;
                    stroke.SetParentKeepWorldPosition(tempLayer);
                    pivot = TrTransform.FromTransform(stroke.StrokeTransform);
                }
                else
                {
                    widget = widgets[i - strokes.Count];
                    widgetLayers[widget] = widget.Canvas;
                    widget.transform.SetParent(tempLayer.transform, true);
                    pivot = widget.LocalTransform;
                }

                tempLayer.Pose *= pivot;
                tempLayer.Pose *= xforms[i];
                tempLayer.Pose *= pivot.inverse;

                if (i < strokes.Count)
                {
                    stroke.SetParentKeepWorldPosition(strokeLayers[stroke], tempLayer.Pose);
                }
                else
                {
                    widget.transform.SetParent(widgetLayers[widget].transform, true);
                }

                tempLayer.Pose = TrTransform.identity;
            }

            App.Scene.DestroyLayer(tempLayer);
        }

        public static void SnapSelectedRotationAngles()
        {
            var xforms = new List<TrTransform>();
            var strokes = SelectionManager.m_Instance.SelectedStrokes.ToList();
            var widgets = SelectionManager.m_Instance.GetValidSelectedWidgets();
            foreach (var stroke in strokes)
            {
                // Do nothing as there's no intuitive "natural" rotation for brush strokes.
                // Pass in a translation that will leave the stroke unchanged
                xforms.Add(TrTransform.T(stroke.m_BatchSubset.m_Bounds.center));
            }

            foreach (var widget in widgets)
            {
                var tr = widget.LocalTransform;
                tr.rotation = SelectionManager.m_Instance.QuantizeAngle(tr.rotation);
                xforms.Add(tr);
            }
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new SetTransformsFromListCommand(strokes, widgets, xforms)
            );
        }

        public static void SnapSelectionToGrid()
        {
            var xforms = new List<TrTransform>();
            var strokes = SelectionManager.m_Instance.SelectedStrokes.ToList();
            var widgets = SelectionManager.m_Instance.GetValidSelectedWidgets();
            int i = 0;
            foreach (var stroke in strokes)
            {
                var snappedPos = SelectionManager.m_Instance.SnapToGrid_CS(stroke.m_BatchSubset.m_Bounds.center);
                xforms.Add(TrTransform.T(snappedPos));
            }
            foreach (var widget in widgets)
            {
                var tr = widget.LocalTransform;
                tr.translation = SelectionManager.m_Instance.SnapToGrid_CS(tr.translation);
                xforms.Add(tr);
            }
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new SetTransformsFromListCommand(strokes, widgets, xforms)
            );
        }
    }
}