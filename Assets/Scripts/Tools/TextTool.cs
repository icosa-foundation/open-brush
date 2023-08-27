// Copyright 2021 The Open Brush Authors
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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace TiltBrush
{
    public class TextTool : BaseTool
    {
        private PopUpWindow m_PopUpPrefab;
        private PopUpWindow m_ActivePopUp;
        
        override public void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);

            if (bEnable)
            {
                EatInput();
            }

            // Make sure our UI reticle isn't active.
            SketchControlsScript.m_Instance.ForceShowUIReticle(false);
        }

        float BoundsRadius
        {
            get
            {
                return SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
            }
        }

        override public void UpdateTool()
        {
            base.UpdateTool();
            
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                TextWidget nearestWidget = WidgetManager.m_Instance.GetNearestTextWidget(rAttachPoint.position, 0.1f);
                if (!nearestWidget)
                {
                    var tr = TrTransform.TR(
                        rAttachPoint.position,
                        rAttachPoint.rotation
                    );

                    var cmd = new CreateWidgetCommand(
                        WidgetManager.m_Instance.TextWidgetPrefab, tr, null, true
                    );

                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);

                    var textWidget = cmd.Widget as TextWidget;
                    if (textWidget != null)
                    {
                        textWidget.Text = "Text Widget";
                        textWidget.Show(true);
                        cmd.SetWidgetCost(textWidget.GetTiltMeterCost());
                    }

                    WidgetManager.m_Instance.WidgetsDormant = false;
                    SketchControlsScript.m_Instance.EatGazeObjectInput();
                    SelectionManager.m_Instance.RemoveFromSelection(false);
                    AudioManager.m_Instance.ShowHideWidget(true, transform.position);
                    nearestWidget = textWidget;
                }
                
                // Create a new popup.
                m_ActivePopUp = Instantiate(
                    m_PopUpPrefab,
                    rAttachPoint.position,
                    rAttachPoint.rotation
                );

                Vector3 vPos = rAttachPoint.position +
                    (rAttachPoint.forward * m_ActivePopUp.GetPopUpForwardOffset()) +
                    rAttachPoint.TransformVector(Vector3.forward);
                m_ActivePopUp.transform.position = vPos;
                m_ActivePopUp.transform.parent = rAttachPoint;
                m_ActivePopUp.Init(gameObject, nearestWidget.Text);

                m_ActivePopUp.m_OnClose += () =>
                {
                    nearestWidget.Text = KeyboardPopUpWindow.m_LastInput;
                };
                m_EatInput = !m_ActivePopUp.IsLongPressPopUp();
            }
            PointerManager.m_Instance.SetMainPointerPosition(rAttachPoint.position);
        }
        
        override public void LateUpdateTool()
        {
            base.LateUpdateTool();
            UpdateTransformsFromControllers();
        }

        private void UpdateTransformsFromControllers()
        {
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            transform.position = rAttachPoint.position;
            transform.rotation = rAttachPoint.rotation;
        }
    }
}
