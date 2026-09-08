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
    public static class FlyPathCapture
    {
        public static bool IsRecording =>
            FlyPathRecorder.Instance != null && FlyPathRecorder.Instance.IsRecording;

        public static bool StartRecording()
        {
            FlyTool flyTool = GetFlyTool(activate: true);
            if (flyTool == null)
            {
                Debug.LogError("FlyPathCapture: Fly tool is not available");
                return false;
            }

            WidgetManager.m_Instance.CameraPathsVisible = true;
            return flyTool.StartPathRecording();
        }

        public static CameraPathWidget StopRecordingAndCreatePath()
        {
            List<FlyPathRecorder.RecordedFrame> frames = StopRecording();
            if (frames == null || frames.Count <= 1)
            {
                Debug.LogWarning("FlyPathCapture: Need at least 2 frames to create a camera path");
                return null;
            }

            CameraPathWidget widget = CameraPathFromFrames.CreateCameraPath(frames);
            if (widget == null)
            {
                return null;
            }

            WidgetManager.m_Instance.CameraPathsVisible = true;
            WidgetManager.m_Instance.SetCurrentCameraPath(widget);
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.CameraPathTool);
            App.Switchboard.TriggerCameraPathModeChanged(CameraPathTool.Mode.AddPositionKnot);
            return widget;
        }

        private static List<FlyPathRecorder.RecordedFrame> StopRecording()
        {
            FlyTool flyTool = GetFlyTool(activate: false);
            if (flyTool != null)
            {
                return flyTool.StopPathRecording();
            }

            if (FlyPathRecorder.Instance != null && FlyPathRecorder.Instance.IsRecording)
            {
                return FlyPathRecorder.Instance.StopRecording();
            }

            Debug.LogWarning("FlyPathCapture: Not currently recording");
            return null;
        }

        private static FlyTool GetFlyTool(bool activate)
        {
            FlyTool flyTool = SketchSurfacePanel.m_Instance.ActiveTool as FlyTool;
            if (flyTool != null || !activate)
            {
                return flyTool;
            }

            SketchControlsScript.m_Instance.EatGazeObjectInput();
            SketchSurfacePanel.m_Instance.RequestHideActiveTool(true);
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FlyTool);
            return SketchSurfacePanel.m_Instance.ActiveTool as FlyTool;
        }
    }
}
