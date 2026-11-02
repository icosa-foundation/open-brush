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

namespace TiltBrush
{

    public class GalleryButton : ModeButton
    {
        public enum Type
        {
            Delete,    // Deprecated
            Selection, // Deprecated
            LogOut,
            Liked,
            Exit,
            Showcase,
            Local,
            Drive,
        }

        public Type m_ButtonType;

        protected override void Awake()
        {
#if OPEN_BRUSH_VIEWER
            if (m_ButtonType == Type.Local)
            {
                m_LocalizedDescription = ViewerSketchbookLabels.Featured;
                m_LocalizedDescriptionExtra = ViewerSketchbookLabels.FeaturedHint;
            }
#endif
            base.Awake();
        }

        override protected void OnButtonPressed()
        {
            SketchbookPanel panel = m_Manager.GetComponent<SketchbookPanel>();
            if (panel)
            {
                panel.ButtonPressed(m_ButtonType, this);
            }
        }
    }
} // namespace TiltBrush
