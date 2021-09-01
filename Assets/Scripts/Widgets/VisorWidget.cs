using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)

namespace TiltBrush {

  public class VisorWidget : MonoBehaviour {

    public static VisorWidget m_Instance { get; private set; }

    public GameObject VisorVisualGameObject;

    void Awake() {
      m_Instance = this;

      if (Config.IsExperimental)
        VisorVisualGameObject.SetActive(true);
    }

    public enum State {
      Idle,
      Hovering,
      Grabbed,
      Dragging,
    }

    public enum VisorRegion {
      None,
      Left,
      Right,
      TopLeft,
      TopRight,
      BottomLeft,
      BottomRight,
      FrontLeft,
      FrontRight,
    }

    public enum Action {
      Hold,
      Drag,
      Twist,
      Crank
    }

    public enum Event {
      Press,
      Release,
      CrankBegin,
      CrankEnd,
      TwistBegin,
      TwistEnd,
    }


    // Internal data
    protected State m_CurrentState;
    protected VisorRegion m_CurrentVisorRegion;
    protected VisorRegion m_GrabbedVisorRegion;

    protected bool m_firstStateSet = false;
    protected bool m_UserInteracting = false;
    protected InputManager.ControllerName m_InteractingController;
    [SerializeField] protected BoxCollider m_BoxCollider;
    [SerializeField] protected BoxCollider m_InnerBoxCollider;

    public bool IsInRange(InputManager.ControllerName controllerName) {
      ControllerInfo interactingControllerInfo = InputManager.Controllers[(int)controllerName];

      Vector3 point = interactingControllerInfo.Transform.position;

      Vector3 vInvTransformedPos = m_BoxCollider.transform.InverseTransformPoint(point) - m_BoxCollider.center;
      Vector3 vSize = m_BoxCollider.size * 0.5f;

      return (Mathf.Abs(vInvTransformedPos.x) <= vSize.x &&
        Mathf.Abs(vInvTransformedPos.y) <= vSize.y &&
        Mathf.Abs(vInvTransformedPos.z) <= vSize.z);
    }

    // never returns VisorRegion.None
    protected VisorRegion GetVisorRegion(InputManager.ControllerName controllerName) {
      ControllerInfo interactingControllerInfo = InputManager.Controllers[(int)controllerName];

      Vector3 point = interactingControllerInfo.Transform.position;

      Vector3 vInvTransformedPos = m_InnerBoxCollider.transform.InverseTransformPoint(point) - m_InnerBoxCollider.center;
      Vector3 vSize = m_InnerBoxCollider.size * 0.5f;

      // normalize to size
      Vector3 vLocalNormalizedPos = new Vector3(
        vInvTransformedPos.x / vSize.x,
        vInvTransformedPos.y / vSize.y,
        vInvTransformedPos.z / vSize.z
        );

      if (vLocalNormalizedPos.x < -1)
        return VisorRegion.Left;
      else if (vLocalNormalizedPos.x > 1)
        return VisorRegion.Right;
      else if (vLocalNormalizedPos.y < -1) {
        if (vLocalNormalizedPos.x > 0)
          return VisorRegion.BottomRight;
        else
          return VisorRegion.BottomLeft;
      }
      else if (vLocalNormalizedPos.y > 1) {
        if (vLocalNormalizedPos.x > 0)
          return VisorRegion.TopRight;
        else
          return VisorRegion.TopLeft;
      }
      else if (vLocalNormalizedPos.z > 1) {
        if (vLocalNormalizedPos.x > 0)
          return VisorRegion.FrontRight;
        else
          return VisorRegion.FrontLeft;
      }
      else {
        if (vLocalNormalizedPos.x < 0)
          return VisorRegion.Left;
        else
          return VisorRegion.Right;
      }
    }

    private void Start() {
      SetDesiredState(State.Idle, InputManager.ControllerName.None, VisorRegion.None);
    }

    public void UpdateInput() {
      switch (m_CurrentState) {
        case State.Idle:
          IdleWaitForHover();
          break;
        case State.Hovering:
          HoverWaitForDrag();
          break;
        case State.Grabbed:
          GrabUpdate();
          break;
        case State.Dragging:
          DragUpdate();
          break;
        default:
          break;
      }

      OnUpdate();
    }

    public void IdleWaitForHover() {
      if (!App.Instance.IsInStateThatAllowsAnyGrabbing())
        return;

      VisorRegion visorRegion;
      InputManager.ControllerName controllerName;

      if (IsInRange(InputManager.ControllerName.Wand)) {
        visorRegion = GetVisorRegion(InputManager.ControllerName.Wand);
        if (visorRegion != VisorRegion.None) {
          controllerName = InputManager.ControllerName.Wand;
          SetDesiredState(State.Hovering, controllerName, visorRegion);
          return;
        }
      }

    }

    public void HoverWaitForDrag() {
      if (!App.Instance.IsInStateThatAllowsAnyGrabbing()) {
        SetDesiredState(State.Idle, InputManager.ControllerName.None, VisorRegion.None);
        return;
      }

      if (IsInRange(m_InteractingController)) {
        VisorRegion visorRegion = GetVisorRegion(m_InteractingController);
        if (InputManager.Controllers[(int)m_InteractingController].GetCommandDown(InputManager.SketchCommands.Activate)) {
          SetDesiredState(State.Grabbed, m_InteractingController, visorRegion);
        }
        else
          SetDesiredState(State.Hovering, m_InteractingController, visorRegion);
      }
      else
        SetDesiredState(State.Idle, InputManager.ControllerName.None, VisorRegion.None);
    }

    public void GrabUpdate() {
      // check for drag
      // TODO: twist, crank

      ControllerInfo interactingControllerInfo = InputManager.Controllers[(int)m_InteractingController];
      // release
      if (!interactingControllerInfo.GetCommand(InputManager.SketchCommands.Activate)) {
        SetDesiredState(State.Idle, InputManager.ControllerName.None, VisorRegion.None);
        return;
      }

      // drag
      VisorRegion visorRegion = GetVisorRegion(m_InteractingController);
      if (visorRegion != m_CurrentVisorRegion) {
        SetDesiredState(State.Dragging, m_InteractingController, visorRegion);
      }
    }

    public void DragUpdate() {
      // only update drag region

      ControllerInfo interactingControllerInfo = InputManager.Controllers[(int)m_InteractingController];
      // release
      if (!interactingControllerInfo.GetCommand(InputManager.SketchCommands.Activate)) {
        SetDesiredState(State.Idle, InputManager.ControllerName.None, VisorRegion.None);
        return;
      }

      // drag
      VisorRegion visorRegion = GetVisorRegion(m_InteractingController);
      if (visorRegion != m_CurrentVisorRegion) {
        SetDesiredState(State.Dragging, m_InteractingController, visorRegion);
      }
    }

    public void SetDesiredState(State desiredState, InputManager.ControllerName controllerName, VisorRegion visorRegion) {

      if (desiredState == m_CurrentState && m_firstStateSet) {
        if (visorRegion != m_CurrentVisorRegion) {
          onRegionExit?.Invoke(m_CurrentVisorRegion);
          m_CurrentVisorRegion = visorRegion;
          OnChangeVisorRegion();
          onRegionEnter?.Invoke(m_CurrentVisorRegion);
        }
        return;
      }

      if (m_firstStateSet)
        OnExitState(m_CurrentState, desiredState);

      State oldstate = m_CurrentState;
      m_CurrentState = desiredState;
      m_InteractingController = controllerName;
      m_firstStateSet = true;

      bool switchRegion = visorRegion != m_CurrentVisorRegion;

      if (switchRegion) {
        onRegionExit?.Invoke(m_CurrentVisorRegion);
        m_CurrentVisorRegion = visorRegion;
      }

      OnEnterState(m_CurrentState, oldstate);

      if (switchRegion) {
        OnChangeVisorRegion();
        onRegionEnter?.Invoke(m_CurrentVisorRegion);
      }
      
    }

    private void OnChangeVisorRegion() {
      if (m_CurrentState == State.Grabbed)
        onDrag?.Invoke(m_CurrentVisorRegion);

      if (m_InteractingController != InputManager.ControllerName.None)
        InputManager.m_Instance.TriggerHaptics(m_InteractingController, 0.1f);
    }

    private void OnExitState(State oldState, State newState) {
      switch (oldState) {
        case State.Idle:
          break;
        case State.Hovering:
          break;
        case State.Grabbed:
          if (newState != State.Dragging)
            onRelease?.Invoke(m_CurrentVisorRegion);
          break;
        case State.Dragging:
          if (newState != State.Grabbed)
            onRelease?.Invoke(m_CurrentVisorRegion);
          break;
        default:
          break;
      }
    }

    private void OnEnterState(State newState, State oldState) {
      switch (newState) {
        case State.Idle:
          m_GrabbedVisorRegion = VisorRegion.None;
          break;
        case State.Hovering:
          m_GrabbedVisorRegion = VisorRegion.None;
          break;
        case State.Grabbed:
          m_GrabbedVisorRegion = m_CurrentVisorRegion;
          if (oldState == State.Hovering)
            onPress?.Invoke(m_CurrentVisorRegion);
          break;
        case State.Dragging:
          break;
        default:
          break;
      }
    }

    public delegate void VisorDelegate(VisorRegion visorRegion);

    public static VisorDelegate onRegionEnter;
    public static VisorDelegate onRegionExit;

    public static VisorDelegate onPress;
    public static VisorDelegate onDrag;
    public static VisorDelegate onRelease;

    public static VisorDelegate onCrankBegin;
    public static VisorDelegate onCrankClick;
    public static VisorDelegate onCrankEnd;

    public static VisorDelegate onTwistBegin;
    public static VisorDelegate onTwistClick;
    public static VisorDelegate onTwistEnd;

    public bool IsUserInteracting(InputManager.ControllerName interactionController) {
      return m_UserInteracting && interactionController == m_InteractingController;
    }



    public void UserInteracting(bool interacting,
        InputManager.ControllerName controller = InputManager.ControllerName.None) {
      // Update state before calling OnUserBegin and OnUserEnd so we can use that state in
      // those functions.
      bool prevInteracting = m_UserInteracting;
      m_UserInteracting = interacting;
      m_InteractingController = controller;

      if (prevInteracting != m_UserInteracting) {
        if (interacting) {
          OnUserBeginInteracting();
        }
        else {
          OnUserEndInteracting();
        }
      }

    }

    virtual protected void OnUpdate() { }

    virtual protected void OnUserBeginInteracting() { }

    virtual protected void OnUserEndInteracting() { }


  }

}

#endif