using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// @brief Moves the brush pointer along a provided list of world-space points.
    /// This component does not record points, does not modify the point list, and does not draw strokes.
    public class MarkovPenPointFollower : MonoBehaviour
    {
        private List<Vector3> m_Points;
        private int m_CurrentIndex;
        private float m_Timer;
        private bool m_IsRunning;

        [SerializeField] private float m_PointDelay = 0.02f;

        [SerializeField]
        private InputManager.ControllerName m_ControllerName = InputManager.ControllerName.Brush;

        public bool IsRunning
        {
            get { return m_IsRunning; }
        }

        /// @brief Start moving the brush pointer along the provided points.
        /// @param points The world-space points that the pointer should follow.
        public void StartFollowing(List<Vector3> points)
        {
            if (points == null)
            {
                Debug.LogError("MarkovPenPointFollower: points is null.");
                return;
            }

            if (points.Count == 0)
            {
                Debug.LogError("MarkovPenPointFollower: points is empty.");
                return;
            }

            m_Points = points;
            m_CurrentIndex = 0;
            m_Timer = 0f;
            m_IsRunning = true;

            ResetPointer();

            Debug.LogError("MarkovPenPointFollower started. Points: " + m_Points.Count);
        }

        /// @brief Stop moving the brush pointer and reset the follower state.
        public void StopFollowing()
        {
            m_IsRunning = false;
            m_CurrentIndex = 0;
            m_Timer = 0f;
            m_Points = null;

            ResetPointer();

            Debug.LogError("MarkovPenPointFollower stopped.");
        }

        /// @brief Update the pointer position while the follower is running.
        private void Update()
        {
            if (!m_IsRunning)
            {
                return;
            }

            if (PointerManager.m_Instance == null)
            {
                StopFollowing();
                return;
            }

            if (m_Points == null || m_Points.Count == 0)
            {
                StopFollowing();
                return;
            }

            m_Timer += Time.deltaTime;

            if (m_Timer < m_PointDelay)
            {
                return;
            }

            m_Timer = 0f;

            if (m_CurrentIndex >= m_Points.Count)
            {
                StopFollowing();
                return;
            }

            Vector3 point = m_Points[m_CurrentIndex];

            PointerManager.m_Instance.SetPointerTransform(
                m_ControllerName,
                point,
                Quaternion.identity);

            PointerManager.m_Instance.EnableLine(false);
            PointerManager.m_Instance.PointerPressure = 0f;

            Debug.LogError("MarkovPenPointFollower point " + m_CurrentIndex + ": " + point);

            m_CurrentIndex++;
        }

        /// @brief Reset the pointer so it does not draw or keep line input active.
        private void ResetPointer()
        {
            if (PointerManager.m_Instance == null)
            {
                return;
            }

            PointerManager.m_Instance.StraightEdgeModeEnabled = false;
            PointerManager.m_Instance.EnableLine(false);
            PointerManager.m_Instance.PointerPressure = 0f;
            PointerManager.m_Instance.EatLineEnabledInput();
        }
    }
}
