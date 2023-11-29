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
    public class SplitFrameCommand : BaseCommand
    {
        private (int,int) timelineLocation;
        private (int,int) splittingIndex;
        AnimationUI_Manager manager;

        bool expandTimeline;
        bool justMoved = true;

        int frameOnStart;

        AnimationUI_Manager.DeletedFrame deletedFrame;


        public SplitFrameCommand()
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
           
            splittingIndex = manager.splitKeyFrame(timelineLocation.Item1,timelineLocation.Item2);
        }

        protected override void OnUndo()
        {

            if (splittingIndex.Item1 == -1 || splittingIndex.Item2 == -1) return;

            int followingLength = manager.getFrameLength(splittingIndex.Item1,splittingIndex.Item2);
            CanvasScript previousCanvas = manager.timeline[splittingIndex.Item1].Frames[splittingIndex.Item2 - 1].canvas;
            Debug.Log("PREVIOUS CANVAS");
             Debug.Log(previousCanvas);
            
            for (int i = 0; i < followingLength; i++){

                AnimationUI_Manager.Frame differentFrame = manager.timeline[splittingIndex.Item1].Frames[splittingIndex.Item2 + i];

                differentFrame.canvas = previousCanvas;
                manager.timeline[splittingIndex.Item1].Frames[splittingIndex.Item2 + i] = differentFrame;
            }
            manager.selectTimelineFrame(splittingIndex.Item1,splittingIndex.Item2);
            manager.fillandCleanTimeline();
            manager.resetTimeline();
            // if (justMoved) return;

            // manager.timeline[previousTrack.Item1] = previousTrack.Item2;


    
 
        }

        // public override bool Merge(BaseCommand other)
        // {
          
        // }
    }
} // namespace TiltBrush
