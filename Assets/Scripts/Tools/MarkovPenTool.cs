using UnityEngine;

namespace TiltBrush
{
    public class MarkovPenTool : BaseTool
    {
        // The parent of all of our tool's visual indicator objects.
        private GameObject m_ToolDirectionIndicator;

        // Whether this tool should follow the controller or not.
        private bool m_IsLockedToController;

        // The controller that this tool is attached to.
        private Transform m_BrushController;

        // Init is similar to Awake(), and should be used for initializing references and other setup code.
        public override void Init()
        {
            base.Init();

            // Get the visual direction indicator by name, like FlyTool does.
            m_ToolDirectionIndicator = transform.Find("DirectionIndicator").gameObject;
        }

        // What to do when the tool is enabled or disabled.
        public override void EnableTool(bool isEnabled)
        {
            base.EnableTool(isEnabled);

            if (isEnabled)
            {
                m_IsLockedToController = m_SketchSurface.IsInFreePaintMode();

                if (m_IsLockedToController)
                {
                    m_BrushController = InputManager.m_Instance.GetController(InputManager.ControllerName.Brush);
                }

            }

            // Make sure our UI reticle isn't active.
            SketchControlsScript.m_Instance.ForceShowUIReticle(false);
        }

        // What to do when the tool is hidden / shown.
        public override void HideTool(bool isHidden)
        {
            base.HideTool(isHidden);

            // Show the direction indicator while the tool is visible.
            m_ToolDirectionIndicator.SetActive(!isHidden);
        }

        // What to do when all the tools run their update functions.
        // Note that this is separate from Unity's Update script.
        // All input handling should be done here.
        public override void UpdateTool()
        {
            base.UpdateTool();

            Transform attachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            PointerManager.m_Instance.SetMainPointerPosition(attachPoint.position);

            // Keep the tool angle correct.
            m_ToolDirectionIndicator.transform.localRotation =
                Quaternion.Euler(PointerManager.m_Instance.FreePaintPointerAngle, 0f, 0f);
        }

        // The actual Unity update function, used to update transforms and perform per-frame operations.
        private void Update()
        {
            // If we're not locking to a controller, update our transforms now, instead of in LateUpdate.
            if (!m_IsLockedToController)
            {
                UpdateTransformsFromControllers();
            }
        }

        public override void LateUpdateTool()
        {
            base.LateUpdateTool();
            UpdateTransformsFromControllers();
        }

        private void UpdateTransformsFromControllers()
        {
            // Lock tool to camera controller.
            if (m_IsLockedToController)
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