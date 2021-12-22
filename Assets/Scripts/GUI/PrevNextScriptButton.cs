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

using TMPro;
using UnityEngine;
namespace TiltBrush
{
    public class PrevNextScriptButton : BaseButton
    {

        public int increment;
        private ScriptUiNav nav;

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            if (nav == null) nav = transform.parent.GetComponent<ScriptUiNav>();
            nav.ChangeScript(increment);
        }

    }
}
