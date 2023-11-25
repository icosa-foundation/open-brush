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


        public AddFrameCommand()
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
           
            insertingAt = manager.addKeyFrame();
        }

        protected override void OnUndo()
        {
            // if (justMoved) return;

   
            Debug.Log("UNDO ADD FRAME");
            UnityEngine.Object.Destroy( manager.timeline[insertingAt.Item1].Frames[insertingAt.Item2].canvas);
            manager.timeline[insertingAt.Item1].Frames.RemoveAt(insertingAt.Item2);


            manager.fillTimeline();

            manager.selectTimelineFrame(timelineLocation.Item1,timelineLocation.Item2);

    
 
        }

        // public override bool Merge(BaseCommand other)
        // {
          
        // }
    }
} // namespace TiltBrush
