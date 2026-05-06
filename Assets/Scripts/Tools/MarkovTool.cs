using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class MarkovTool : BaseTool
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
            
            if (bEnable) {
                m_LockToController = m_SketchSurface.IsInFreePaintMode();
                if (m_LockToController) {
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
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            PointerManager.m_Instance.SetMainPointerPosition(rAttachPoint.position);
            
            //keep the tool angle correct
            m_toolDirectionIndicator.transform.localRotation =
                Quaternion.Euler(PointerManager.m_Instance.FreePaintPointerAngle, 0f, 0f);
        }

        //The actual Unity update function, used to update transforms and perform per-frame operations
        void Update()
        {
            
            // If we're not locking to a controller, update our transforms now, instead of in LateUpdate.
            if (!m_LockToController) {
                UpdateTransformsFromControllers();
            }
        }
        
        override public void LateUpdateTool() {
            base.LateUpdateTool();
            UpdateTransformsFromControllers();
        }
        
        private void UpdateTransformsFromControllers() {
            // Lock tool to camera controller.
            if (m_LockToController) {
                transform.position = m_BrushController.position;
                transform.rotation = m_BrushController.rotation;
            } else {
                transform.position = SketchSurfacePanel.m_Instance.transform.position;
                transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
            }
        }
    }

}
