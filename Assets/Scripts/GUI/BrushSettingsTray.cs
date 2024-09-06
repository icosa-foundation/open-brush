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

using System;
using System.Collections;
using UnityEngine;

namespace TiltBrush
{
    public class BrushSettingsTray : BaseTray
    {

        override protected void Start()
        {
            base.Start();

            if (true)
            {
                DoAnimateIn();
            }

            // m_AnimateIn = true;
            // Vector3 localScale = transform.localScale;
            // localScale.x = m_AnimateRange.y;
            // transform.localScale = localScale;
            // m_Mesh.SetActive(true);
            // m_Collider.enabled = true;

            transform.GetComponentInChildren<AdvancedSlider>().SetInitialValueAndUpdate(
                PointerManager.m_Instance.MainPointer.BrushSize01
            );
        }

        protected override void OnToolChanged()
        {
            // No op
        }

        public void OnSliderChanged(Vector3 value)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 = value.y;
        }
    }

} // namespace TiltBrush
