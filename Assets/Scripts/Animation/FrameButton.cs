using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.Animation
{
public class FrameButton : BaseButton
{
    public int Layer = -1;
    public int Frame = -1;
     protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            print("BUTTON PRESSED");
            print(Layer + " " +Frame );
        
            App.Scene.animationUI_manager.printTimeline();
            App.Scene.animationUI_manager.selectTimelineFrame(
                Layer,Frame

             );
         
            
    }
    public void setButtonCoordinate(int updatedLayer, int updatedFrame){

        Layer = updatedLayer;
        Frame = updatedFrame;

    }


}
}
