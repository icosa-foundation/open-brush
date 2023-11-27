// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System.Collections;

namespace TiltBrush.FrameAnimation
{
    public class DeleteFrameCommand : BaseCommand
    {
        private (int,int) timelineLocation;
        private (int,int) insertingAt;
        AnimationUI_Manager manager;

        bool expandTimeline;
        bool justMoved = true;

        int frameOnStart;

        AnimationUI_Manager.DeletedFrame deletedFrame;


        public DeleteFrameCommand()
        {
           manager = App.Scene.animationUI_manager;
           timelineLocation = manager.getCanvasLocation(App.Scene.ActiveCanvas);

        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnDispose()
        {
         
        }

        protected override void OnRedo()
        {
           
            deletedFrame = manager.removeKeyFrame();
        }

        protected override void OnUndo()
        {

            for (int i = 0; i< deletedFrame.length;i++){
                if (deletedFrame.location.Item2 + i >= manager.timeline[deletedFrame.location.Item1].Frames.Count){

                     manager.timeline[deletedFrame.location.Item1].Frames.Add(deletedFrame.frame);

                }else{

                     manager.timeline[deletedFrame.location.Item1].Frames[deletedFrame.location.Item2 + i] = deletedFrame.frame;

                }
              
            }

            manager.resetTimeline();
            manager.selectTimelineFrame(deletedFrame.location.Item1,deletedFrame.location.Item2);
            // if (justMoved) return;

            // manager.timeline[previousTrack.Item1] = previousTrack.Item2;


    
 
        }

        // public override bool Merge(BaseCommand other)
        // {
          
        // }
    }
} // namespace TiltBrush
