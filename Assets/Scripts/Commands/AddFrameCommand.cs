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
    public class AddFrameCommand : BaseCommand
    {
        private (int,int) timelineLocation;
        private (int,int) insertingAt;
        AnimationUI_Manager manager;

        bool expandTimeline;
        bool justMoved = true;

        int frameOnStart;


        public AddFrameCommand( (int,int) Loc , AnimationUI_Manager animationManager)
        {
           manager = animationManager;
           timelineLocation = Loc;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnDispose()
        {
         
        }

        protected override void OnRedo()
        {
           
            frameOnStart = manager.getFrameOn();

            Debug.Log("ON REDO");
            (int, int ) nextIndex = manager.getFollowingFrameIndex(timelineLocation.Item1,timelineLocation.Item2);

            if (nextIndex.Item2 >= manager.timeline[nextIndex.Item1].Frames.Count ){

                  AnimationUI_Manager.Frame addingFrame = manager.newFrame(App.Scene.AddCanvas());

                manager.timeline[nextIndex.Item1].Frames.Insert(manager.timeline[nextIndex.Item1].Frames.Count, addingFrame);
                nextIndex.Item2 = manager.timeline[nextIndex.Item1].Frames.Count - 1;


                expandTimeline = true;
                insertingAt = (nextIndex.Item1,manager.timeline[nextIndex.Item1].Frames.Count - 1);
                justMoved = false;

            }else if(  manager.getFrameFilled(nextIndex.Item1,nextIndex.Item2)) {

                AnimationUI_Manager.Frame  addingFrame = manager.newFrame(App.Scene.AddCanvas());

                manager.timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2, addingFrame);

                expandTimeline = false;
                insertingAt = nextIndex;
                justMoved = false;


            }

            manager.fillTimeline();

            manager.selectTimelineFrame(nextIndex.Item1,nextIndex.Item2);
        }

        protected override void OnUndo()
        {
            if (justMoved) return;

   
            Debug.Log("UNDO ADD FRAME");
            UnityEngine.Object.Destroy( manager.timeline[insertingAt.Item1].Frames[insertingAt.Item2].canvas);
            manager.timeline[insertingAt.Item1].Frames.RemoveAt(insertingAt.Item2);


            manager.fillTimeline();

            manager.selectTimelineFrame(timelineLocation.Item1,frameOnStart);

    
 
        }

        // public override bool Merge(BaseCommand other)
        // {
          
        // }
    }
} // namespace TiltBrush
