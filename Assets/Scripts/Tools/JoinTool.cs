// Copyright 2022 The Open Brush Authors
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

    public class JoinTool : BaseStrokeIntersectionTool
    {
        [SerializeField] private Transform m_DropperTransform;
        [SerializeField] private Renderer m_DropperConeRenderer;
        [SerializeField] private Renderer m_DropperRenderer;
        [SerializeField] private float m_DropperBrushSelectRadius;
        private Renderer m_DropperColorDescriptionSwatchRenderer;

        private bool m_ValidBrushFoundThisFrame;
        private bool m_SelectionValid;
        private Color m_SelectionColor;
        private BrushDescriptor m_SelectionBrush;
        private Stroke m_SelectionStroke;

        private enum State
        {
            Enter,
            Standard,
            Exit,
            Off
        }
        private State m_CurrentState;
        private float m_EnterAmount;
        [SerializeField] private float m_EnterSpeed = 16.0f;
        [SerializeField] private Transform m_OffsetTransform;
        private Vector3 m_OffsetTransformBaseScale;
        private Stroke m_StrokeA;

        public void DisableRequestExit_HackForSceneSurgeon() { m_RequestExit = false; }

        override public void Init()
        {
            base.Init();

            m_OffsetTransformBaseScale = m_OffsetTransform.localScale;
            SetState(State.Off);
            m_EnterAmount = 0.0f;
            UpdateScale();
            m_StrokeA = null;
        }

        override public void HideTool(bool bHide)
        {
            base.HideTool(bHide);

            if (bHide)
            {
                SetState(State.Exit);
            }
            ResetDetection();
            m_DropperRenderer.enabled = !bHide;
            m_DropperConeRenderer.enabled = !bHide;
        }

        override public void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);
            ResetDetection();
            m_SelectionValid = false;

            if (bEnable)
            {
                EatInput();
            }
            else
            {
                SetState(State.Off);
                m_EnterAmount = 0.0f;
                UpdateScale();
            }
            SnapIntersectionObjectToController();
        }

        void Update()
        {
            //update animations
            switch (m_CurrentState)
            {
                case State.Enter:
                    m_EnterAmount += (m_EnterSpeed * Time.deltaTime);
                    if (m_EnterAmount >= 1.0f)
                    {
                        m_EnterAmount = 1.0f;
                        m_CurrentState = State.Standard;
                    }
                    UpdateScale();
                    break;
                case State.Exit:
                    m_EnterAmount -= (m_EnterSpeed * Time.deltaTime);
                    if (m_EnterAmount <= 0.0f)
                    {
                        m_EnterAmount = 0.0f;
                        m_CurrentState = State.Off;
                    }
                    UpdateScale();
                    break;
            }
        }

        void SetState(State rDesiredState)
        {
            switch (rDesiredState)
            {
                case State.Enter:
                    break;
            }
            m_CurrentState = rDesiredState;
        }

        override public void UpdateTool()
        {
            base.UpdateTool();

            //keep description locked to controller
            SnapIntersectionObjectToController();

            //always default to resetting detection
            m_ResetDetection = true;
            m_ValidBrushFoundThisFrame = false;

            if (App.Config.m_UseBatchedBrushes)
            {
                UpdateBatchedBrushDetection(m_DropperTransform.position);
            }
            else
            {
                UpdateSolitaryBrushDetection(m_DropperTransform.position);
            }

            if (m_ResetDetection)
            {
                if (m_ValidBrushFoundThisFrame)
                {
                    SetState(State.Enter);
                }
                else
                {
                    SetState(State.Exit);
                }
                ResetDetection();
            }

            // Clear our "memory" when not in use
            if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                m_StrokeA = null;
            }
        }

        private int ClosestControlPoint(Stroke stroke)
        {
            var closestDistance = float.PositiveInfinity;
            int closestPoint = 0;
            for (var i = 0; i < stroke.m_ControlPoints.Length; i++)
            {
                var cp = stroke.m_ControlPoints[i];
                TrTransform toolTrTransform_CS = Coords.AsCanvas[m_DropperTransform];
                var dist = Vector3.Distance(cp.m_Pos, toolTrTransform_CS.translation);
                if (dist < closestDistance)
                {
                    closestPoint = i;
                    closestDistance = dist;
                }
            }
            return closestPoint;
        }

        override protected void SnapIntersectionObjectToController()
        {
            Vector3 vPos = InputManager.Brush.Geometry.ToolAttachPoint.position +
                InputManager.Brush.Geometry.ToolAttachPoint.forward * m_PointerForwardOffset;
            m_DropperTransform.position = vPos;
            m_DropperTransform.rotation = InputManager.Brush.Geometry.ToolAttachPoint.rotation;
        }

        override protected void HandleIntersection(Stroke strokeB)
        {
            if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                if (m_StrokeA == strokeB)
                {
                    // Do nothing
                }
                else if (m_StrokeA == null)
                {
                    // New selection
                    m_StrokeA = strokeB;
                    AudioManager.m_Instance.PlayGroupedSound(m_DropperRenderer.transform.position);
                }
                else
                {
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                        new JoinStrokeCommand(m_StrokeA, strokeB)
                    );
                    AudioManager.m_Instance.PlayGroupedSound(m_DropperRenderer.transform.position);
                }
            }
        }

        void UpdateScale()
        {
            Vector3 vScale = m_OffsetTransformBaseScale;
            vScale.x *= m_EnterAmount;
            m_OffsetTransform.localScale = vScale;
        }

        override public float GetSize()
        {
            return m_DropperBrushSelectRadius;
        }

    }
} // namespace TiltBrush
