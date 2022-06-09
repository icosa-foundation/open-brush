using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TiltBrush {
public class FlattenSubtool : BaseSculptSubtool
{
    // Return direction from the vertex to the flattening tool mesh. 
    // If the vertex is already there, return a zero vertex. (could be done in IsInReach)
    override public Vector3 CalculateDirection(Vector3 vertex, Vector3 toolPos, bool isPushing, BatchSubset rGroup) {
        return Vector3.zero; //CTODO: implement
        
    }
}

} // namespace TiltBrush
