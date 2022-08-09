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

using CSCore.XAudio2.X3DAudio;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public class HoverActionButton : ActionButton
    {
        [SerializeField] private UnityEngine.Events.UnityEvent m_HoverAction;
        private bool m_IsEmitting = false;
        private ParticleSystem ps;

        protected override void Awake()
        {
            base.Awake();
            ps = GetComponent<ParticleSystem>();
        }

        public override void ResetState()
        {
            base.ResetState();
            m_IsEmitting = false;
        }

        public override void GainFocus()
        {
            m_IsEmitting = true;
        }

        void Update()
        {
            if (!m_IsEmitting) return;

            if (Time.frameCount % 3 != 0) return; // Throttle emission

            foreach (var widget in EditableModelManager.m_Instance.LinkedWidgets)
            {
                var vector = widget.transform.position - transform.position;
                var emitParams = new ParticleSystem.EmitParams();
                emitParams.position = transform.position;
                emitParams.velocity = vector;
                emitParams.startLifetime = vector.magnitude / emitParams.velocity.magnitude;
                ps.Emit(emitParams, 1);
            }
        }
    }
} // namespace TiltBrush
