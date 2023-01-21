using System.Collections.Generic;
using System.Linq;
using SVGMeshUnity;
using UnityEngine;

namespace TiltBrush
{
    public static class Drawing
    {
        public static void DrawPath(List<List<float>> path)
        {
            DrawStrokes.SinglePathToStroke(path, Vector3.zero);
        }

        public static List<TrTransform> PathFromSvg(string svgPathString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            SVGData svgData = new SVGData();
            svgData.Path(svgPathString);
            SVGPolyline svgPolyline = new SVGPolyline();
            svgPolyline.Fill(svgData);
            return svgPolyline.Polyline[0].Select(p => TrTransform.T(p)).ToList();
        }
    }
}
