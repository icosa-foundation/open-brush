// Copyright 2022 The Tilt Brush Authors
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

namespace TiltBrush
{
    public class PolyhydraModeTray : BaseTray
    {
        public Transform m_PreviewPolyAttachPoint;
        private RepaintTool m_repaintTool;

        public int CurrentGalleryPage { get; set; }

        void Update()
        {
            m_PreviewPolyAttachPoint.Rotate(0, 0.25f, 0);
            if (transform.localScale.x < .1f)
            {
                // Hide preview poly
                m_PreviewPolyAttachPoint.gameObject.SetActive(false);
            }
            else
            {
                m_PreviewPolyAttachPoint.gameObject.SetActive(true);
            }
        }
    }

} // namespace TiltBrush
