using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {
public abstract class BaseSculptSubtool : MonoBehaviour {
    
    /// For sculpting tools with an interactor that limits the sculpting tool's
    /// sphere of influence. If the interactor doesn't exist or shouldn't limit things, this is ignored.
    public virtual bool IsInReach(Vector3 vertex, TrTransform canvasPose) {
        return true;
    }

    public abstract Vector3 CalculateDirection(Vector3 vertex, Vector3 toolPos, bool isPushing, BatchSubset rGroup);
}
} //namespace TiltBrush
