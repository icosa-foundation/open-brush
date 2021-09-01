#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
using UnityEngine;
using System.Collections.Generic;

namespace TiltBrush {
  public partial class FreePaintTool {
    private bool m_GridSnapActive;

    private void ApplyGridSnap(ref Vector3 pos, ref Quaternion rot) {
      pos = SnapToGrid(pos);
    }

    // based off of the MultiplyPoint3x4
    // note that positional offset (matrix[0].w, matrix[1].w, matrix[2].w) has been REVERSE and is subtracting
    // don't ask me why I had to do this, but it had to be done to make it work in this scenario.
    static Vector3 WorldToCanvasPos(Matrix4x4 matrix, Vector3 point) {
      Vector3 res;
      res.x = matrix.m00 * point.x + matrix.m01 * point.y + matrix.m02 * point.z - matrix.m03;
      res.y = matrix.m10 * point.x + matrix.m11 * point.y + matrix.m12 * point.z - matrix.m13;
      res.z = matrix.m20 * point.x + matrix.m21 * point.y + matrix.m22 * point.z - matrix.m23;
      return res;
    }

    public static Vector3 SnapToGrid(Vector3 position) {
      if (!DepthGuide.m_instance || !DepthGuide.m_instance.isActiveAndEnabled)
        return position;

      float gridScale = DepthGuide.m_instance.m_MainCanvas.lossyScale.x;
      float gridSubdivision = Mathf.Pow(2, Mathf.Floor(Mathf.Log(gridScale * 4, 2)));

      Vector3 localCanvasPos = DepthGuide.m_instance.m_MainCanvas.worldToLocalMatrix.MultiplyPoint3x4(position);

      Vector3 roundedCanvasPos =
        new Vector3(
          Mathf.Round(localCanvasPos.x * gridSubdivision) / gridSubdivision,
          Mathf.Round(localCanvasPos.y * gridSubdivision) / gridSubdivision,
          Mathf.Round(localCanvasPos.z * gridSubdivision) / gridSubdivision
          );

      return DepthGuide.m_instance.m_MainCanvas.localToWorldMatrix.MultiplyPoint3x4(roundedCanvasPos);
    }

  }
}

#endif