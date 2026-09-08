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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class SelectionTray : BaseTray
    {
        [SerializeField] private OptionButton m_GroupButton;

        protected override void OnSelectionChanged()
        {
            m_GroupButton.UpdateVisuals();
        }

        public void RepaintSelected()
        {
            var pm = PointerManager.m_Instance;
            SketchMemoryScript.m_Instance.RepaintSelected(
                pm.RebrushOn,
                pm.RecolorOn,
                pm.ResizeOn,
                pm.JitterOn
            );
        }
    }

} // namespace TiltBrush
