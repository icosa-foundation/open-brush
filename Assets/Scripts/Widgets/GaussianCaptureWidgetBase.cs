// Copyright 2026 The Open Brush Authors
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
    public abstract class GaussianCaptureWidgetBase : ShapeWidget
    {
        private const float kScaleStepSize = 0.12f;

        private float m_RuntimeScaleAccumulator;

        public override void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            base.AssignControllerMaterials(controller);
            if (CameraCaptureRuntime.m_Instance != null &&
                (controller == InputManager.ControllerName.Brush || controller == InputManager.ControllerName.Wand))
            {
                InputManager.GetControllerGeometry(controller).ShowBrushSizer();
            }
        }

        protected override void OnUserBeginInteracting()
        {
            base.OnUserBeginInteracting();
            m_RuntimeScaleAccumulator = 0.0f;
        }

        protected override void OnUserEndInteracting()
        {
            m_RuntimeScaleAccumulator = 0.0f;
            base.OnUserEndInteracting();
        }

        protected abstract string GetAdjustmentHintText();

        protected abstract bool TryApplyCaptureStep(int stepCount, out string statusText);

        public bool TryAdjustCaptureParametersFromScale(float deltaScale)
        {
            if (deltaScale <= 0.0f || Mathf.Approximately(deltaScale, 1.0f))
            {
                m_RuntimeScaleAccumulator = 0.0f;
                return false;
            }

            m_RuntimeScaleAccumulator += Mathf.Log(deltaScale);
            float stepThreshold = Mathf.Log(1.0f + kScaleStepSize);
            if (Mathf.Abs(m_RuntimeScaleAccumulator) < stepThreshold)
            {
                return true;
            }

            int stepCount = Mathf.FloorToInt(Mathf.Abs(m_RuntimeScaleAccumulator) / stepThreshold);
            stepCount *= m_RuntimeScaleAccumulator > 0.0f ? 1 : -1;
            if (stepCount == 0)
            {
                return true;
            }

            m_RuntimeScaleAccumulator -= stepCount * stepThreshold;
            if (!TryApplyCaptureStep(stepCount, out string statusText))
            {
                return true;
            }

            InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.05f);
            InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Wand, 0.05f);

            return true;
        }
    }
} // namespace TiltBrush
