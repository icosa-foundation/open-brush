﻿// Copyright 2020 The Tilt Brush Authors
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
    public class ProfilePopUpButton : OptionButton
    {
        [SerializeField] private bool m_CommandIgnored = false;

        override protected void OnButtonPressed()
        {
            // For some circumstances on mobile, we want to ignore the command, but still
            // notify the popup that we were pressed.  Which happens below.
            if (!m_CommandIgnored)
            {
                base.OnButtonPressed();
            }

            ProfilePopUpWindow popup = m_Manager.GetComponent<ProfilePopUpWindow>();
            Debug.Assert(popup != null);
            popup.OnProfilePopUpButtonPressed(this);
        }
    }
} // namespace TiltBrush
