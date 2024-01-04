// Copyright 2023 The Open Brush Authors
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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class ScriptedTool : BaseTool
    {

        //the parent of all of our tool's visual indicator objects
        private GameObject m_toolDirectionIndicator;

        //whether this tool should follow the controller or not
        private bool m_LockToController;

        //the controller that this tool is attached to
        private Transform m_BrushController;

        // Set true when the tool is activated so we can detect when it's released
        private bool m_WasClicked = false;

        // The position of the pointed when m_ClickedLastUpdate was set to true;
        private TrTransform m_FirstPositionClicked_CS;
        private Vector3 m_FirstPositionClicked_GS;

        public Transform m_AttachmentSphere;
        public Mesh previewCube;
        public Mesh previewSphere;
        public Mesh previewQuad;
        public Mesh previewCapsule;
        public Mesh previewCylinder;

        public Material previewMaterial;

        private int currentSnap;
        private float angleSnappingAngle;

        //Init is similar to Awake(), and should be used for initializing references and other setup code
        public override void Init()
        {
            base.Init();
            m_toolDirectionIndicator = transform.GetChild(0).gameObject;
        }

        //What to do when the tool is enabled or disabled
        public override void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);

            if (bEnable)
            {
                var script = LuaManager.Instance.GetActiveScript(LuaApiCategory.ToolScript);
                LuaManager.Instance.InitScript(script);

                m_AttachmentSphere.parent = InputManager.Brush.Geometry.ToolAttachPoint;
                m_AttachmentSphere.gameObject.SetActive(true);
                m_AttachmentSphere.localPosition = Vector3.zero;

                m_LockToController = m_SketchSurface.IsInFreePaintMode();
                if (m_LockToController)
                {
                    m_BrushController = InputManager.m_Instance.GetController(InputManager.ControllerName.Brush);
                }

                EatInput();
            }
            else
            {
                LuaManager.Instance.EndActiveScript(LuaApiCategory.ToolScript);
                m_AttachmentSphere.parent = transform;
                m_AttachmentSphere.gameObject.SetActive(false);
            }

            // Make sure our UI reticle isn't active.
            SketchControlsScript.m_Instance.ForceShowUIReticle(false);
        }

        //What to do when the tool is hidden / shown
        public override void HideTool(bool bHide)
        {
            base.HideTool(bHide);
            m_toolDirectionIndicator.SetActive(!bHide);
        }

        //What to do when all the tools run their update functions. Note that this is separate from Unity's Update script
        //All input handling should be done here
        override public void UpdateTool()
        {

            base.UpdateTool();

            Vector3 rAttachPoint_GS;
            TrTransform rAttachPoint_CS;

            if (App.Config.m_SdkMode == SdkMode.Monoscopic)
            {
                rAttachPoint_CS = TrTransform.TR(
                    LuaManager.Instance.GetPastBrushPos(0),
                    LuaManager.Instance.GetPastBrushRot(0)
                );
                rAttachPoint_GS = (App.Scene.Pose.inverse * rAttachPoint_CS).translation;
                Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
                PointerManager.m_Instance.SetMainPointerPosition(rAttachPoint.position);
                m_toolDirectionIndicator.transform.localRotation = Quaternion.Euler(PointerManager.m_Instance.FreePaintPointerAngle, 0f, 0f);
            }
            else
            {
                rAttachPoint_GS = InputManager.Brush.Geometry.ToolAttachPoint.position;
                rAttachPoint_CS = App.Scene.ActiveCanvas.AsCanvas[InputManager.Brush.Geometry.ToolAttachPoint];

                Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
                PointerManager.m_Instance.SetMainPointerPosition(rAttachPoint.position);
                m_toolDirectionIndicator.transform.localRotation = Quaternion.Euler(PointerManager.m_Instance.FreePaintPointerAngle, 0f, 0f);
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                m_WasClicked = true;
                // Initial click. Store the transform
                m_FirstPositionClicked_CS = rAttachPoint_CS;
                m_FirstPositionClicked_GS = rAttachPoint_GS;

                SetApiProperty($"Tool.{LuaNames.ToolScriptStartPoint}", m_FirstPositionClicked_CS);
                ApiManager.Instance.StartUndo();
            }

            bool shouldEndUndo = false;

            if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                var previewTypeVal = LuaManager.Instance.GetSettingForActiveScript(LuaApiCategory.ToolScript, LuaNames.ToolPreviewType);
                var previewAxisVal = LuaManager.Instance.GetSettingForActiveScript(LuaApiCategory.ToolScript, LuaNames.ToolPreviewAxis);
                var drawnVector_CS = rAttachPoint_CS.translation - m_FirstPositionClicked_CS.translation;
                var drawnVector_GS = rAttachPoint_GS - m_FirstPositionClicked_GS;

                var stableUp = CalcStableUp(drawnVector_CS);
                if (drawnVector_GS.sqrMagnitude > 0)
                {
                    var rotation_CS = Quaternion.LookRotation(drawnVector_CS, stableUp);
                    var rotation_GS = Quaternion.LookRotation(drawnVector_GS, stableUp);

                    // Snapping needs compensating for the different rotation between global space and canvas space
                    var CS_GS_offset = rotation_GS.eulerAngles - rotation_CS.eulerAngles;
                    rotation_CS *= Quaternion.Euler(-CS_GS_offset);
                    rotation_CS *= Quaternion.Euler(CS_GS_offset);

                    Matrix4x4 transform_GS = TrTransform.TRS(
                        m_FirstPositionClicked_GS,
                        App.Scene.Pose.rotation * rotation_CS,
                        drawnVector_CS.magnitude * 2
                    ).ToMatrix4x4();

                    switch (previewTypeVal.String?.ToLower())
                    {
                        case "alignedbox":
                            var aabbTr = Matrix4x4.TRS(
                                transform_GS.GetPosition(),
                                App.Scene.Pose.rotation,
                                // TODO Scale isn't correct but _CS doesn't seem to work either
                                drawnVector_GS * 2
                            );
                            Graphics.DrawMesh(previewCube, aabbTr, previewMaterial, 0);
                            break;
                        case "alignedquad":
                            var aaquadTr = Matrix4x4.TRS(
                                transform_GS.GetPosition(),
                                App.Scene.Pose.rotation * Quaternion.Euler(90, 90, 0),
                                // TODO Scale isn't correct but _CS doesn't seem to work either
                                new Vector3(drawnVector_GS.z * 2, drawnVector_GS.x * 2, 1)
                            );
                            Graphics.DrawMesh(previewQuad, aaquadTr, previewMaterial, 0);
                            break;
                        case "cube":
                            Graphics.DrawMesh(previewCube, transform_GS, previewMaterial, 0);
                            break;
                        case "sphere":
                            Graphics.DrawMesh(previewSphere, transform_GS, previewMaterial, 0);
                            break;
                        case "quad":
                            var mat = transform_GS;
                            switch (previewAxisVal.String?.ToLower())
                            {
                                case "x":
                                    mat *= Matrix4x4.Rotate(Quaternion.LookRotation(Vector3.right));
                                    break;
                                case "y":
                                    mat *= Matrix4x4.Rotate(Quaternion.LookRotation(Vector3.up));
                                    break;
                                default:
                                    break;
                            }
                            Graphics.DrawMesh(previewQuad, mat, previewMaterial, 0);
                            break;
                        case "capsule":
                            Graphics.DrawMesh(previewCapsule, transform_GS, previewMaterial, 0);
                            break;
                        case "cylinder":
                            Graphics.DrawMesh(previewCylinder, transform_GS, previewMaterial, 0);
                            break;
                        case null:
                            break;
                    }
                }
            }
            else if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                if (m_WasClicked)
                {
                    m_WasClicked = false;
                    var drawnVector_CS = rAttachPoint_CS.translation - m_FirstPositionClicked_CS.translation;
                    SetApiProperty($"Tool.{LuaNames.ToolScriptEndPoint}", rAttachPoint_CS);
                    SetApiProperty($"Tool.{LuaNames.ToolScriptVector}", drawnVector_CS);
                    SetApiProperty($"Tool.{LuaNames.ToolScriptRotation}", Quaternion.LookRotation(drawnVector_CS, CalcStableUp(drawnVector_CS)));
                    shouldEndUndo = true;
                }
            }

            LuaManager.Instance.DoToolScript(LuaNames.Main, m_FirstPositionClicked_CS, rAttachPoint_CS);
            if (shouldEndUndo) ApiManager.Instance.EndUndo();
        }

        public static Vector3 CalcStableUp(Vector3 vector)
        {
            // Check if the direction is nearly up or down, if it is, use world right instead.
            var referenceUp = Vector3.Dot(vector, Vector3.up) > 0.99f ||
                Vector3.Dot(vector, Vector3.up) < -0.99f
                    ? Vector3.right : Vector3.up;
            // Compute the right vector by crossing the direction with the reference up
            Vector3 right = Vector3.Cross(vector, referenceUp);
            // Compute a stable up vector by crossing the direction with the right vector
            Vector3 stableUp = Vector3.Cross(vector, right);
            return stableUp;
        }

        private void SetApiProperty(string key, object value)
        {
            var script = LuaManager.Instance.GetActiveScript(LuaApiCategory.ToolScript);
            LuaManager.Instance.SetApiProperty(script, key, value);
        }

        //The actual Unity update function, used to update transforms and perform per-frame operations
        protected override void Update()
        {
            base.Update();
            // If we're not locking to a controller, update our transforms now, instead of in LateUpdate.
            if (!m_LockToController)
            {
                UpdateTransformsFromControllers();
            }
        }

        override public void LateUpdateTool()
        {
            base.LateUpdateTool();
            UpdateTransformsFromControllers();
        }

        private void UpdateTransformsFromControllers()
        {
            var tr = transform;
            // Lock tool to camera controller.
            if (m_LockToController)
            {
                tr.position = m_BrushController.position;
                tr.rotation = m_BrushController.rotation;
            }
            else
            {
                var panelTr = SketchSurfacePanel.m_Instance.transform;
                tr.position = panelTr.position;
                tr.rotation = panelTr.rotation;
            }
        }
    }
}
