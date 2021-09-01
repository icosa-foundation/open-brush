using System;
using UnityEngine;

namespace TiltBrush {

  public class MarkerPoint : GrabWidget {
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)

    protected override void Awake() {
      transform.SetParent(App.Instance.m_CanvasTransform, true);
      base.Awake();
    }
#endif
  }

}