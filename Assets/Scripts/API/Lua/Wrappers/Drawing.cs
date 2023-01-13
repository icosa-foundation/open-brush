using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public static class Drawing
    {
        public static void DrawPath(List<List<float>> path)
        {
            DrawStrokes.SinglePathToStroke(path, Vector3.zero);
        }
    }
}
