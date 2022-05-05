using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using TiltBrush.MeshEditing;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class PolyhydraTool : SelectionTool
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

        private Mesh previewMesh;
        private Material previewMaterial;


        private float[] angleSnaps;
        private int currentSnap;
        private float angleSnappingAngle;

        //Init is similar to Awake(), and should be used for initializing references and other setup code
        public override void Init()
        {
            base.Init();
            m_toolDirectionIndicator = transform.GetChild(0).gameObject;
            angleSnaps = new[] { 0f, 15f, 30f, 45f, 60f, 75f, 90f };
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

            Vector3 rAttachPoint_GS = InputManager.Brush.Geometry.ToolAttachPoint.position;
            TrTransform rAttachPoint_CS = App.Scene.ActiveCanvas.AsCanvas[InputManager.Brush.Geometry.ToolAttachPoint];

            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            PointerManager.m_Instance.SetMainPointerPosition(rAttachPoint.position);
            m_toolDirectionIndicator.transform.localRotation = Quaternion.Euler(PointerManager.m_Instance.FreePaintPointerAngle, 0f, 0f);

            if (InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Undo))
            {
                currentSnap++;
                currentSnap %= angleSnaps.Length;
                angleSnappingAngle = angleSnaps[currentSnap];
                GetComponentInChildren<TextMeshPro>().text = angleSnappingAngle.ToString();
            }
            bool angleSnap = !(currentSnap == 0);

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                m_WasClicked = true;
                // Initially click. Store the transform and grab the poly mesh and material.
                VrUiPoly uiPoly = FindObjectOfType<VrUiPoly>();
                if (uiPoly == null) return;
                m_FirstPositionClicked_CS = rAttachPoint_CS;
                m_FirstPositionClicked_GS = rAttachPoint_GS;
                previewMesh = uiPoly.GetComponent<MeshFilter>().mesh;
                previewMaterial = uiPoly.GetComponent<MeshRenderer>().material;
            }

            if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                var drawnVector_CS = rAttachPoint_CS.translation - m_FirstPositionClicked_CS.translation;
                var drawnVector_GS = rAttachPoint_GS - m_FirstPositionClicked_GS;
                var rotation_CS = Quaternion.LookRotation(drawnVector_CS, Vector3.up);
                var rotation_GS = Quaternion.LookRotation(drawnVector_GS, Vector3.up);

                // Snapping needs compensating for the different rotation between global space and canvas space
                var CS_GS_offset = rotation_GS.eulerAngles - rotation_CS.eulerAngles;
                rotation_CS *= Quaternion.Euler(-CS_GS_offset);
                rotation_CS = angleSnap ? QuantizeAngle(rotation_CS) : rotation_CS;
                rotation_CS *= Quaternion.Euler(CS_GS_offset);

                Matrix4x4 transform_GS = Matrix4x4.TRS(
                    m_FirstPositionClicked_GS,
                    rotation_CS,
                    Vector3.one * drawnVector_GS.magnitude
                );
                Graphics.DrawMesh(previewMesh, transform_GS, previewMaterial, 0);

            }
            else if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                if (m_WasClicked)
                {
                    m_WasClicked = false;
                    VrUiPoly uiPoly = FindObjectOfType<VrUiPoly>();
                    if (uiPoly == null) return;

                    var drawnVector_CS = rAttachPoint_CS.translation - m_FirstPositionClicked_CS.translation;
                    var drawnVector_GS = rAttachPoint_GS - m_FirstPositionClicked_GS;
                    var scale_CS = drawnVector_CS.magnitude;
                    var rotation_CS = Quaternion.LookRotation(drawnVector_CS, Vector3.up);
                    rotation_CS = angleSnap ? QuantizeAngle(rotation_CS) : rotation_CS;

                    bool strokeShapeMode = false; // TODO

                    var poly = uiPoly._conwayPoly;
                    
                    if (!strokeShapeMode)
                    {
                        var creationTr = TrTransform.TRS(
                            m_FirstPositionClicked_CS.translation,
                            rotation_CS,
                            scale_CS
                        );
                        var shapeType = GeneratorTypes.Uniform;
                        var parameters = new Dictionary<string,object>
                        {  
                        };
                        EditableModelManager.m_Instance.GeneratePolyMesh(poly, creationTr, 
                            ColorMethods.ByRole, shapeType, parameters);
                    }
                    else
                    {

                        var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
                        uint time = 0;
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
                                        m_TimestampMs = time
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
        private Quaternion QuantizeAngle(Quaternion rotation)
        {
            float round(float val) { return Mathf.Round(val / angleSnappingAngle) * angleSnappingAngle; }

            Vector3 euler = rotation.eulerAngles;
            euler = new Vector3(round(euler.x), round(euler.y), round(euler.z));
            return Quaternion.Euler(euler);
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

        override protected bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup)
        {
            return false;
        }

        override protected bool HandleIntersectionWithWidget(GrabWidget widget)
        {
            
            bool result = base.HandleIntersectionWithWidget(widget);
            return result;
        }
    }
}
