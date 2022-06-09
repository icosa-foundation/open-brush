using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {
public class PushSubtool : BaseSculptSubtool
{
    override public Vector3 CalculateDirection(Vector3 vertex, Vector3 toolPos, bool isPushing, BatchSubset rGroup) {
        return (isPushing ? 1 : -1) * (vertex - toolPos).normalized;
    }
}

}// namespace TiltBrush
