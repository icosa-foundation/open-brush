/********************************************************************************//**
\file      HandPose.cs
\brief     Stores pose-specific data such as the animation id and allowing gestures.
\copyright Copyright 2015 Oculus VR, LLC All Rights reserved.
************************************************************************************/

using UnityEngine;

namespace OVRTouchSample
{
    public enum HandPoseId
    {
        Default,
        Generic,
        PingPongBall,
        Controller
    }

    public class HandPose : MonoBehaviour
    {
        [SerializeField]
        private bool m_allowPointing = false;
        [SerializeField]
        private bool m_allowThumbsUp = false;
        [SerializeField]
        private HandPoseId m_poseId = HandPoseId.Default;

        public bool AllowPointing
        {
            get { return m_allowPointing; }
        }

        public bool AllowThumbsUp
        {
            get { return m_allowThumbsUp; }
        }

        public HandPoseId PoseId
        {
            get { return m_poseId; }
        }
    }
}
