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

using UnityEngine;

namespace TiltBrush
{
    public class DeleteStrokeCommand : BaseCommand
    {
        public Stroke m_TargetStroke;
        private bool m_SilenceFirstAudio;

        private Vector3 CommandAudioPosition
        {
            get { return GetPositionForCommand(m_TargetStroke); }
        }

        public DeleteStrokeCommand(Stroke stroke, BaseCommand parent = null)
            : base(parent)
        {
            m_TargetStroke = stroke;
            m_SilenceFirstAudio = true;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            if (!m_SilenceFirstAudio)
            {
                AudioManager.m_Instance.PlayUndoSound(CommandAudioPosition);
            }
            m_SilenceFirstAudio = false;
            m_TargetStroke.Hide(true);
        }

        protected override void OnUndo()
        {
            if (!m_SilenceFirstAudio)
            {
                AudioManager.m_Instance.PlayRedoSound(CommandAudioPosition);
            }
            m_SilenceFirstAudio = false;
            m_TargetStroke.Hide(false);
        }
    }
} // namespace TiltBrush
