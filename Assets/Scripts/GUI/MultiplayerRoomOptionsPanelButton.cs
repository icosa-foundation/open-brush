﻿// Copyright 2023 The Open Brush Authors
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
    public class MultiplayerRoomOptionsPanelButton : OptionButton
    {
        [SerializeField] private bool m_CommandIgnored = false;
        [HideInInspector] public int playerId;

        override protected void OnButtonPressed()
        {

            MultiplayerRoomOptionsPopUpWindow popup = m_Manager.GetComponent<MultiplayerRoomOptionsPopUpWindow>();

            // For some circumstances on mobile, we want to ignore the command, but still
            // notify the popup that we were pressed.  Which happens below.
            if (!m_CommandIgnored)
            {
                if (m_RequiresPopup & m_Command == SketchControlsScript.GlobalCommands.ToggleUserVoiceInMultiplayer)
                {

                }

                base.OnButtonPressed();
            }


            Debug.Assert(popup != null);
            popup.OnMultiplayerRoomOptionsPopUpWindowButtonPressed(this);
        }

        public void SetToggleState(bool isActive)
        {
            m_ToggleActive = isActive;
            UpdateVisuals();
        }

        public bool GetToggleState()
        {
            return m_ToggleActive;
        }
    }
} // namespace TiltBrush
