// Copyright 2024 The Open Brush Authors
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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("tool.fly.recordpath.start", "Start recording camera path while using fly tool")]
        public static void StartFlyPathRecording()
        {
            FlyTool flyTool = SketchSurfacePanel.m_Instance.ActiveTool as FlyTool;
            if (flyTool == null)
            {
                SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
                flyTool = SketchSurfacePanel.m_Instance.ActiveTool as FlyTool;
                if (flyTool == null) return;
            }
            flyTool.StartPathRecording();
        }

        [ApiEndpoint("tool.fly.recordpath.stop", "Stop recording and create camera path from recorded frames")]
        public static void StopFlyPathRecording()
        {
            FlyTool flyTool = SketchSurfacePanel.m_Instance.ActiveTool as FlyTool;
            if (flyTool == null) return;
            List<FlyPathRecorder.RecordedFrame> frames = flyTool.StopPathRecording();
            if (frames != null && frames.Count > 1)
            {
                CameraPathFromFrames.CreateCameraPath(frames);
            }
        }
    }
}