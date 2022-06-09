using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TiltBrush {
public class CreaseSubtool : BaseSculptSubtool
{

    public override bool IsInReach(Vector3 vertex, TrTransform CanvasPose) {
        return GetComponent<Renderer>().bounds.Contains(CanvasPose * vertex);
    }

    public override Vector3 CalculateDirection(Vector3 vertex, Vector3 toolPos, bool isPushing, BatchSubset rGroup) {
        return (isPushing ? 1 : -1) * -(vertex - rGroup.m_Bounds.center).normalized;
    }
}

} // namespace TiltBrush
