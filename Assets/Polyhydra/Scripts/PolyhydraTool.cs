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

using System.Collections.Generic;
using System.Linq;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public class PolyhydraTool : BaseStrokeIntersectionTool
    {

        //the parent of all of our tool's visual indicator objects
        private GameObject m_toolDirectionIndicator;

        //the controller that this tool is attached to
        private Transform m_BrushController;

        // Set true when the tool is activated so we can detect when it's released
        private bool m_WasClicked = false;

        // The position of the pointed when m_ClickedLastUpdate was set to true;
        private TrTransform m_FirstPositionClicked_CS;

        private Mesh previewMesh;
        private Material previewMaterial;
        
        //whether this tool should follow the controller or not
        private bool m_LockToController;
        
        private bool m_ValidWidgetFoundThisFrame;
        private EditableModelWidget LastIntersectedEditableModelWidget;

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

            PolyhydraPanel polyhydraPanel = PanelManager.m_Instance.GetActivePanelByType(BasePanel.PanelType.Polyhydra) as PolyhydraPanel;
            
            if (bEnable)
            {
                m_LockToController = m_SketchSurface.IsInFreePaintMode();
                if (m_LockToController)
                {
                    m_BrushController = InputManager.m_Instance.GetController(InputManager.ControllerName.Brush);
                }
                polyhydraPanel.CurrentPolyhedra.transform.parent = this.transform;
                EatInput();
            }
            else
            {
                polyhydraPanel.CurrentPolyhedra.transform.parent = polyhydraPanel.PreviewPolyParent.transform;
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

            //keep description locked to controller
            SnapIntersectionObjectToController();

            //always default to resetting detection
            m_ResetDetection = true;
            m_ValidWidgetFoundThisFrame = false;

            if (App.Config.m_UseBatchedBrushes)
            {
                // Required to trigger widget detection
                UpdateBatchedBrushDetection(InputManager.Brush.Geometry.ToolAttachPoint.position);
            }
            else
            {
                // Doesn't seem to handle widget detect so m_UseBatchedBrushes==false is probably a broken code path now
                UpdateSolitaryBrushDetection(InputManager.Brush.Geometry.ToolAttachPoint.position);
            }

            if (m_ResetDetection)
            {
                ResetDetection();
            }
            
            TrTransform rAttachPoint_CS = App.Scene.ActiveCanvas.AsCanvas[InputManager.Brush.Geometry.ToolAttachPoint];

            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            PointerManager.m_Instance.SetMainPointerPosition(rAttachPoint.position);
            m_toolDirectionIndicator.transform.localRotation = Quaternion.Euler(PointerManager.m_Instance.FreePaintPointerAngle, 0f, 0f);
            
            
            
            // TODO WIP... 
            if (m_ValidWidgetFoundThisFrame && InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                Debug.Log($"{LastIntersectedEditableModelWidget.name}");
                AudioManager.m_Instance.PlayDuplicateSound(Vector3.zero);
            }
            
            
            
            
                
            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                m_WasClicked = true;
                // Initially click. Store the transform and grab the poly mesh and material.
                PreviewPolyhedron uiPoly = FindObjectOfType<PreviewPolyhedron>();
                if (uiPoly == null) return;
                m_FirstPositionClicked_CS = rAttachPoint_CS;
                previewMesh = EditableModelManager.m_Instance.m_PreviewPolyhedron.GetComponent<MeshFilter>().mesh;
                previewMaterial = EditableModelManager.m_Instance.m_PreviewPolyhedron.GetComponent<MeshRenderer>().material;
            }

            Vector3 SnapToGrid(Vector3 v)
            {
                return SelectionManager.m_Instance.SnapToGrid(v);
            }
            
            var position_CS = SnapToGrid(m_FirstPositionClicked_CS.translation);
            var drawnVector_CS = rAttachPoint_CS.translation - m_FirstPositionClicked_CS.translation;
            var rotation_CS = SelectionManager.m_Instance.QuantizeAngle(
                Quaternion.LookRotation(drawnVector_CS, Vector3.up)
            );
            var scale_CS = SelectionManager.m_Instance.ScalarSnap(drawnVector_CS.magnitude);

            if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                Matrix4x4 mat_CS = Matrix4x4.TRS(
                    position_CS,
                    rotation_CS,
                    Vector3.one * scale_CS
                );
                Matrix4x4 mat_GS = App.ActiveCanvas.Pose.ToMatrix4x4() * mat_CS;
                Graphics.DrawMesh(previewMesh, mat_GS, previewMaterial, 0);

            }
            else if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                if (m_WasClicked)
                {
                    m_WasClicked = false;

                    // Create editable model (false) or brush strokes (true)
                    bool strokeShapeMode = InputManager.Brush.GetCommand(InputManager.SketchCommands.Undo);

                    var poly = EditableModelManager.m_Instance.m_PreviewPolyhedron.m_PolyMesh;

                    if (!strokeShapeMode)
                    {
                        PolyhydraPanel.CreateWidgetForPolyhedron(position_CS, rotation_CS, scale_CS, poly);
                    }
                    else
                    {
                        var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
                        float minPressure = PointerManager.m_Instance.MainPointer.CurrentBrush.PressureSizeMin(false);
                        float pressure = Mathf.Lerp(minPressure, 1f, 0.5f);

                        var group = App.GroupManager.NewUnusedGroup();

                        foreach (var (face, faceIndex) in poly.Faces.WithIndex())
                        {
                            float lineLength = 0;
                            var controlPoints = new List<PointerManager.ControlPoint>();
                            var faceVerts = face.GetVertices();
                            faceVerts.Add(faceVerts[0]);
                            for (var vertexIndex = 0; vertexIndex < faceVerts.Count; vertexIndex++)
                            {
                                var vert = faceVerts[vertexIndex];
                                var nextVert = faceVerts[(vertexIndex + 1) % faceVerts.Count];

                                for (float step = 0; step < 1f; step += .25f)
                                {
                                    var vertexPos = vert.Position + (nextVert.Position - vert.Position) * step;
                                    vertexPos = vertexPos * scale_CS;
                                    vertexPos = rotation_CS * vertexPos;
                                    controlPoints.Add(new PointerManager.ControlPoint
                                    {
                                        m_Pos = m_FirstPositionClicked_CS.translation + vertexPos,
                                        m_Orient = Quaternion.LookRotation(face.Normal, Vector3.up),
                                        m_Pressure = pressure,
                                        m_TimestampMs = (uint)(Time.unscaledTime * 1000)
                                    });
                                }

                                lineLength += (nextVert.Position - vert.Position).magnitude; // TODO Does this need scaling? Should be in Canvas space
                            }

                            var stroke = new Stroke
                            {
                                m_Type = Stroke.Type.NotCreated,
                                m_IntendedCanvas = App.Scene.ActiveCanvas,
                                m_BrushGuid = brush.m_Guid,
                                m_BrushScale = 1f,
                                m_BrushSize = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute,
                                m_Color = EditableModelManager.m_Instance.m_PreviewPolyhedron.GetFaceColorForStrokes(faceIndex),
                                m_Seed = 0,
                                m_ControlPoints = controlPoints.ToArray(),
                            };
                            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
                            stroke.Group = group;
                            stroke.Recreate(null, App.Scene.ActiveCanvas);
                            if (faceIndex != 0) stroke.m_Flags = SketchMemoryScript.StrokeFlags.IsGroupContinue;
                            SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                                new BrushStrokeCommand(stroke, WidgetManager.m_Instance.ActiveStencil, 123) // TODO calc length
                            );
                        }
                    }
                }
            }
        }

        //The actual Unity update function, used to update transforms and perform per-frame operations
        void Update()
        {
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
            // Lock tool to camera controller.
            if (m_LockToController)
            {
                transform.position = m_BrushController.position;
                transform.rotation = m_BrushController.rotation;
            }
            else
            {
                transform.position = SketchSurfacePanel.m_Instance.transform.position;
                transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
            }
        }
        
        override protected bool HandleIntersectionWithWidget(GrabWidget widget)
        {
            ResetDetection();
            // Only intersect with EditableModelWidget instances
            var editableModelWidget = widget as EditableModelWidget;
            LastIntersectedEditableModelWidget = editableModelWidget;
            m_ValidWidgetFoundThisFrame = widget != null;
            return m_ValidWidgetFoundThisFrame;
        }
    }
}
