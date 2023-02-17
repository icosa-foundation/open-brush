using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.Layers
{
public class FrameButton : BaseButton
{
    public int Layer = 0;
     protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            print("PRESSED");
            print(transform.name + " " +transform.parent.parent.name );
            // GetComponentInParent<LayerUI_Manager>().ToggleVisibility(transform.parent.gameObject);
    }
}
}
