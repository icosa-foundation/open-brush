using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IsoMesh.Physics
{
    /// <summary>
    /// This class sort of emulates a child transform except only updates position at fixed intervals.
    /// </summary>
    [ExecuteInEditMode]
    public class StaggeredFollower : MonoBehaviour
    {
        [SerializeField]
        private bool m_lockRotation = true;

        [SerializeField]
        private Transform m_followTransform;

        [SerializeField]
        private float m_rounding = 0.5f;

        private void OnValidate()
        {
            m_rounding = Mathf.Max(m_rounding, 0.0001f);
        }

        private Vector3 m_currentRoundedPos;

        private void LateUpdate() => Follow();
        private void FixedUpdate() => Follow();
        
        private void Follow()
        {
            TryUpdateRoundedPos();

            if (m_lockRotation)
                transform.rotation = Quaternion.identity;
        }

        private void TryUpdateRoundedPos()
        {
            if (!m_followTransform)
                return;

            Vector3 followPos = m_followTransform.transform.position;
            Vector3 roundedPos = new Vector3(Utils.RoundToNearest(followPos.x, m_rounding), Utils.RoundToNearest(followPos.y, m_rounding), Utils.RoundToNearest(followPos.z, m_rounding));

            if ((roundedPos - m_currentRoundedPos).sqrMagnitude > 0.1f)
            {
                m_currentRoundedPos = roundedPos;
                transform.position = m_currentRoundedPos;
            }
        }
    }
}


