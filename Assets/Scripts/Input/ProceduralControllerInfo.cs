// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using UnityEngine;

namespace TiltBrush
{
    /// Input edges stay valid for the whole frame, regardless of how many consumers query them.
    public sealed class ProceduralControllerInfo : NonVrControllerInfo
    {
        private bool m_Trigger;
        private bool m_PreviousTrigger;
        private Vector2 m_Thumbstick;
        private VrInput[] m_Buttons = Array.Empty<VrInput>();
        private VrInput[] m_PreviousButtons = Array.Empty<VrInput>();

        public ProceduralControllerInfo(BaseControllerBehavior behavior) : base(behavior)
        {
            IsTrackedObjectValid = true;
        }

        public void SetTrigger(bool pressed)
        {
            m_PreviousTrigger = m_Trigger;
            m_Trigger = pressed;
        }

        public override float GetTriggerRatio() => GetTriggerValue();
        public void SetButtons(VrInput[] buttons)
        {
            m_PreviousButtons = m_Buttons;
            m_Buttons = buttons;
        }

        public override float GetGripValue() => GetVrInput(VrInput.Grip) ? 1 : 0;
        public void SetThumbstick(Vector2 value) => m_Thumbstick = Vector2.ClampMagnitude(value, 1);
        public override Vector2 GetThumbStickValue() => m_Thumbstick;
        public override Vector2 GetPadValueDelta() => m_Thumbstick;
        public override float GetScrollXDelta() => m_Thumbstick.x;
        public override float GetScrollYDelta() => m_Thumbstick.y;
        public override bool GetVrInputTouch(VrInput input) =>
            input == VrInput.Thumbstick && (m_Thumbstick.sqrMagnitude > 0 || GetVrInput(input));
        public override float GetTriggerValue() => m_Trigger ? 1 : 0;
        public override bool GetVrInput(VrInput input) => input == VrInput.Trigger ? m_Trigger : Array.IndexOf(m_Buttons, input) >= 0;
        public override bool GetVrInputDown(VrInput input) =>
            input == VrInput.Trigger ? m_Trigger && !m_PreviousTrigger :
                Array.IndexOf(m_Buttons, input) >= 0 && Array.IndexOf(m_PreviousButtons, input) < 0;
        public override bool GetVrInputUp(VrInput input) =>
            input == VrInput.Trigger ? !m_Trigger && m_PreviousTrigger :
                Array.IndexOf(m_Buttons, input) < 0 && Array.IndexOf(m_PreviousButtons, input) >= 0;
    }
}
