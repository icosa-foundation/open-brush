using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.Animation
{
public class FrameButton : BaseButton
{
    public int Layer = 0;
     protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            print("PRESSED");
            print(transform.GetSiblingIndex() + " " +transform.parent.parent.GetSiblingIndex() );
        
             App.Scene.animationUI_manager.selectTimelineFrame(
                transform.GetSiblingIndex(),transform.parent.parent.GetSiblingIndex()

             );
         
            
    }


}
}
