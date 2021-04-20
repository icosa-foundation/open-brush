using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Conway;
using UnityEngine;
using Random = System.Random;

namespace TiltBrush.AndyB
{
    public class PolyhydraTool : BaseTool
    {
        //the parent of all of our tool's visual indicator objects
        private GameObject m_toolDirectionIndicator;

        //whether this tool should follow the controller or not
        private bool m_LockToController;

        //the controller that this tool is attached to
        private Transform m_BrushController;

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
                m_LockToController = m_SketchSurface.IsInFreePaintMode();
                if (m_LockToController)
                {
                    m_BrushController = InputManager.m_Instance.GetController(InputManager.ControllerName.Brush);
                }

                EatInput();
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

            // Angle the pointer according to the user-defined pointer angle.
            var rAttachPoint = App.Scene.ActiveCanvas.AsCanvas[InputManager.Brush.Geometry.ToolAttachPoint];
            Vector3 pos = rAttachPoint.translation;
            Quaternion rot = rAttachPoint.rotation;

            //keep the tool angle correct
            m_toolDirectionIndicator.transform.localRotation = Quaternion.Euler(PointerManager.m_Instance.FreePaintPointerAngle, 0f, 0f);
            
            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                VrUiPoly uiPoly = FindObjectOfType<VrUiPoly>();
                if (uiPoly == null) return;

                var poly = uiPoly._conwayPoly;

                var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
                uint time = 0;
                float minPressure = PointerManager.m_Instance.MainPointer.CurrentBrush.PressureSizeMin(false);
                float pressure = Mathf.Lerp(minPressure, 1f, 0.5f);
                
                var strokes = new List<Stroke>();
                
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
                            controlPoints.Add(new PointerManager.ControlPoint
                            {
                                m_Pos = pos + vert.Position + ((nextVert.Position - vert.Position) * step),
                                m_Orient = Quaternion.LookRotation(face.Normal, Vector3.up),
                                m_Pressure = pressure,
                                m_TimestampMs = time++
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
                        m_Color = uiPoly.GetFaceColor(faceIndex),
                        m_Seed = 0,
                        m_ControlPoints = controlPoints.ToArray(),
                    };
                    stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
                    stroke.Uncreate();
                    stroke.Recreate(null, App.Scene.ActiveCanvas);

                    SketchMemoryScript.m_Instance.MemorizeBatchedBrushStroke(
                        stroke.m_BatchSubset,
                        stroke.m_Color,
                        stroke.m_BrushGuid,
                        stroke.m_BrushSize,
                        stroke.m_BrushScale,
                        stroke.m_ControlPoints.ToList(),
                        stroke.m_Flags,
                        WidgetManager.m_Instance.ActiveStencil,
                        lineLength,
                        123
                    );
                    
                    strokes.Add(stroke);
                }
                
                // SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                //     new SelectCommand(
                //         strokes,
                //         new List<GrabWidget>(),
                //         SelectionManager.m_Instance.SelectionTransform,
                //         deselect: false,
                //         initial: false
                //     )
                // );
                //
                // // SelectionManager.m_Instance.SelectStrokes(strokes);
                //
                // SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                //     new GroupStrokesAndWidgetsCommand(
                //         strokes, 
                //         new List<GrabWidget>(),
                //         null
                //     )
                // );
                //
                // AudioManager.m_Instance.PlayGroupedSound(InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Brush));
                // SketchSurfacePanel.m_Instance.RequestHideActiveTool(true);
                // SketchSurfacePanel.m_Instance.EnableSpecificTool(ToolType.PolyhydraTool);
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
    }
}