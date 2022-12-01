using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IsoMesh
{
    public class SineMove : MonoBehaviour
    {
        [SerializeField]
        private float m_amplitude;

        [SerializeField]
        private float m_period;

        [SerializeField]
        private bool m_isRunning = false;

        private Vector3 m_startPos;
        private Vector3 m_startUp;
        private float m_t = 0f;

        private void Reset()
        {
            m_startPos = transform.position;
            m_startUp = transform.up;
            m_t = 0f;
        }

        private void Start()
        {
            m_startPos = transform.position;
            m_startUp = transform.up;
            m_t = 0f;
        }

        private void Update()
        {
            if (!m_isRunning)
                return;

            transform.position = m_startPos + m_startUp * Mathf.Cos(Mathf.PI * 2f * m_period * m_t) * m_amplitude;
            m_t += Time.smoothDeltaTime;
        }
    }
}