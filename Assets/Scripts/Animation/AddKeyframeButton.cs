// Copyright 2023 The Open Brush Authors
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

namespace TiltBrush.FrameAnimation
{
    public class AddKeyframeButton : BaseButton
    {
        protected override void OnButtonPressed()
        {
            var command = new AddFrameCommand();
            if (command.IsAvailable)
            {
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                return;
            }

            (int, int) location = App.Scene.animationUI_manager.GetCanvasLocation(App.Scene.ActiveCanvas);
            App.Scene.animationUI_manager.SelectFollowingEmptyFrame(location.Item1, location.Item2);
        }
    }
} // namespace TiltBrush.FrameAnimation
