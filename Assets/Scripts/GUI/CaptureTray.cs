// Copyright 2026 The Open Brush Authors
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

namespace TiltBrush
{
    /// Button-driven tray; ToggleTray lives on BaseTray. Kept as a distinct type because
    /// the prefabs' UnityEvent bindings name it.
    public class CaptureTray : BaseTray
    {
        /// This tray's visibility is owned by its toggle button, so don't let tool changes
        /// drive it. m_ShowOnToolType is unused here.
        protected override void OnToolChanged()
        {
        }
    }

} // namespace TiltBrush
