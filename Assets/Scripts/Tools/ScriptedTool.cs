
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using TMPro;
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

        public Mesh previewMesh;
        public Material previewMaterial;


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
                m_FirstPositionClicked_CS = rAttachPoint_CS;
                m_FirstPositionClicked_GS = rAttachPoint_GS;
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

                    var drawnVector_CS = rAttachPoint_CS.translation - m_FirstPositionClicked_CS.translation;
                    var drawnVector_GS = rAttachPoint_GS - m_FirstPositionClicked_GS;
                    var scale_CS = drawnVector_CS.magnitude;
                    var rotation_CS = Quaternion.LookRotation(drawnVector_CS, Vector3.up);
                    rotation_CS = angleSnap ? QuantizeAngle(rotation_CS) : rotation_CS;


                    var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
                    var pos = m_FirstPositionClicked_CS.translation;

                    Vector3 TableToVector3(Table t)
                    {
                        return rotation_CS * new Vector3(
                            (float)t.Get(1).Number,
                            (float)t.Get(2).Number,
                            (float)t.Get(3).Number
                        );
                    };

                    // string scriptCode = @"
                    //         function circle(x, y, z, r)
                    //         points = {}
                    //         for i = 1, 360, 10 do
                    //             angle = i * math.pi / 180
                    //             table.insert(points, {x+r*math.cos(angle), y+r*math.sin(angle), z})
                    //         end
                    //         return points
                    //         end
                    //     ";
                    // Script script = new Script();
                    // script.DoString(scriptCode);

                    string activeToolScriptName = LuaManager.Instance.ToolScripts.Keys.Last(); // TODO
                    Script activeToolScript = LuaManager.Instance.ToolScripts[activeToolScriptName];
                    Closure activeToolFunction = activeToolScript.Globals.Get(activeToolScriptName).Function;
                    Table activeToolWidgets = activeToolScript.Globals.Get("Widgets").Table;
                    DynValue result = activeToolFunction.Call(0, 0, 0, 1);
                    List<Vector3> points = result.Table.Values.Select(x => TableToVector3(x.Table)).ToList();

                    DrawStrokes.PositionPathsToStroke(points, pos, scale_CS, 1f / App.ActiveCanvas.Pose.scale);
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
    }
}
