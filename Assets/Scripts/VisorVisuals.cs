using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)

namespace TiltBrush {

  public class VisorVisuals : MonoBehaviour {

    public enum State {
      Idle,
      Hover,
      Grab,
      Drag
    }

    [SerializeField]
    protected GameObject m_depthRipple;

    protected bool m_showVisuals;

    protected State m_CurrentState;

    void OnEnable() {
      SetDesiredState(State.Idle);

      VisorWidget.onRegionEnter += OnRegionEnter;
      VisorWidget.onPress += OnPress;
      VisorWidget.onRelease += OnRelease;
    }

    void OnDisable() {
      VisorWidget.onRegionEnter -= OnRegionEnter;
      VisorWidget.onPress -= OnPress;
      VisorWidget.onRelease -= OnRelease;
    }

    public void OnRegionEnter(VisorWidget.VisorRegion visorRegion) {
      // TODO: Make ambidextrous
      switch (visorRegion) {
        case VisorWidget.VisorRegion.Left:
          switch (m_CurrentState) {
            case State.Idle:
              SetDesiredState(State.Hover);
              break;
            case State.Drag:
              SetDesiredState(State.Grab);
              break;
            default:
              break;
          }
          break;
        case VisorWidget.VisorRegion.Right:
        case VisorWidget.VisorRegion.TopRight:
        case VisorWidget.VisorRegion.BottomRight:
        case VisorWidget.VisorRegion.FrontRight:
          switch (m_CurrentState) {
            case State.Hover:
              SetDesiredState(State.Idle);
              break;
            case State.Grab:
              SetDesiredState(State.Drag);
              break;
            default:
              break;
          }
          break;
        case VisorWidget.VisorRegion.None:
        default:
          switch (m_CurrentState) {
            case State.Hover:
              SetDesiredState(State.Idle);
              break;
            case State.Drag:
              SetDesiredState(State.Grab);
              break;
          }
          break;
      }
    }

    private void ShowVisorHint(string message) {
      // Debug.Log(message);
    }

    public void OnPress(VisorWidget.VisorRegion visorRegion) {
      if (m_CurrentState == State.Hover && visorRegion == VisorWidget.VisorRegion.Left)
        SetDesiredState(State.Grab);
    }
    public void OnRelease(VisorWidget.VisorRegion visorRegion) {
      switch (m_CurrentState) {
        case State.Idle:
        case State.Hover:
          break;
        case State.Grab:
          SetDesiredState(State.Idle);
          break;
        case State.Drag:
          if (
            visorRegion == VisorWidget.VisorRegion.Right ||
            visorRegion == VisorWidget.VisorRegion.TopRight ||
            visorRegion == VisorWidget.VisorRegion.BottomRight ||
            visorRegion == VisorWidget.VisorRegion.FrontRight
            )
            ToggleVisuals();

          SetDesiredState(State.Idle);
          break;
      }
    }

    private void ToggleVisuals() {
      m_showVisuals = !m_showVisuals;

      m_depthRipple.SetActive(m_showVisuals);
    }

    private void SetDesiredState(State desiredState) {
      if (desiredState == m_CurrentState) {
        return;
      }

      OnExitState(m_CurrentState);

      m_CurrentState = desiredState;

      OnEnterState(m_CurrentState);

    }

    private void OnEnterState(State newState) {
      switch (newState) {
        case State.Idle:
          ShowVisorHint(null);
          break;
        case State.Hover:
          ShowVisorHint("Hold trigger to activate");
          break;
        case State.Grab:
          ShowVisorHint("Drag Right");

          break;
        case State.Drag:
          ShowVisorHint("Release to complete");

          break;
        default:
          break;
      }
    }

    private void OnExitState(State oldState) {
      switch (oldState) {
        case State.Idle:
          break;
        case State.Hover:
          break;
        case State.Grab:
          break;
        case State.Drag:
          break;
        default:
          break;
      }
    }

  }
}

#endif