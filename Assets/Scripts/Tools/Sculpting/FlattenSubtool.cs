using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TiltBrush {
public class FlattenSubtool : BaseSculptSubtool
{
    public override Vector3 CalculateDirection(Vector3 vertex, Vector3 toolPos, bool isPushing, BatchSubset rGroup) {
        return Vector3.zero; //CTODO: implement
    }
}

} // namespace TiltBrush
