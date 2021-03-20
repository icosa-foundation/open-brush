using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.LachlanSleight
{
    public class FlyTool : BaseTool
    {

	    private GameObject _toolDirectionIndicator;
        private bool m_LockToController;
        private Transform m_BrushController;
        [SerializeField]
        [Range(0f, 2f)]
        private float m_MaxSpeed = 1f;
        [SerializeField]
        [Range(0f, 18f)]
        private float m_DampingUp = 2f;
        [SerializeField]
        [Range(0f, 18f)]
        private float m_DampingDown = 12f;
        [Range(0f, 1f)]
        private float m_StopThresholdSpeed = 0.01f;

        private bool m_Armed = false;
        
        private Vector3 m_Velocity;

        public override void Init()
        {
	        base.Init();
	        _toolDirectionIndicator = transform.Find("DirectionIndicator").gameObject;
        }

        override public void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);
            
            if (bEnable) {
                m_LockToController = m_SketchSurface.IsInFreePaintMode();
                if (m_LockToController) {
                    m_BrushController = InputManager.m_Instance.GetController(InputManager.ControllerName.Brush);
                }

                EatInput();
            }

            m_Armed = false;

            // Make sure our UI reticle isn't active.
            SketchControlsScript.m_Instance.ForceShowUIReticle(false);
        }
        
        float BoundsRadius {
          get {
            return SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
          }
        }

        public override bool InputBlocked()
        {
	        return !m_Armed;
        }

        public override void HideTool(bool bHide)
        {
	        base.HideTool(bHide);
	        _toolDirectionIndicator.SetActive(!bHide);
        }

        override public void UpdateTool() {
            base.UpdateTool();

            //don't start teleporting until the user has released the trigger they pulled to enable the tool!
            if (InputBlocked()) {
	            if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Fly)) {
		            m_Armed = true;
	            } else {
		            return;
	            }
            }


            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 vMovement = m_BrushController.forward;
            
            if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Fly)) {
	            m_Velocity = Vector3.Lerp(m_Velocity, vMovement * m_MaxSpeed, Time.deltaTime * m_DampingUp);

	            AudioManager.m_Instance.WorldGrabLoop(true);
	            AudioManager.m_Instance.WorldGrabbed(m_BrushController.position);
	            AudioManager.m_Instance.ChangeLoopVolume("WorldGrab",
		            Mathf.Clamp((m_Velocity.magnitude / m_MaxSpeed) /
		                        AudioManager.m_Instance.m_WorldGrabLoopAttenuation, 0f,
			            AudioManager.m_Instance.m_WorldGrabLoopMaxVolume));
            } else {
	            m_Velocity = Vector3.Lerp(m_Velocity, Vector3.zero, Time.deltaTime * m_DampingDown);
	            float mag = m_Velocity.magnitude;
	            if (mag < m_StopThresholdSpeed) {
		            AudioManager.m_Instance.WorldGrabLoop(false);
		            m_Velocity = Vector3.zero;
	            } else {
		            AudioManager.m_Instance.ChangeLoopVolume("WorldGrab",
			            Mathf.Clamp((mag / m_MaxSpeed) /
			                        AudioManager.m_Instance.m_WorldGrabLoopAttenuation, 0f,
				            AudioManager.m_Instance.m_WorldGrabLoopMaxVolume));
	            }
            }
            
            PointerManager.m_Instance.SetMainPointerPosition(rAttachPoint.position);
        }

        void ApplyVelocity(Vector3 velocity)
        {
	        TrTransform newScene = App.Scene.Pose;
	        newScene.translation -= velocity;
	        // newScene might have gotten just a little bit invalid.
	        // Enforce the invariant that fly always sends you
	        // to a scene which is MakeValidPose(scene)
	        newScene = SketchControlsScript.MakeValidScenePose(newScene, BoundsRadius);
	        App.Scene.Pose = newScene;
        }

        void Update()
        {
          ApplyVelocity(m_Velocity);
          if (!m_LockToController) {
            // If we're not locking to a controller, update our transforms now, instead of in LateUpdate.
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