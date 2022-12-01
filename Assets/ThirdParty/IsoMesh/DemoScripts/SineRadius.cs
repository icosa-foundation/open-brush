using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IsoMesh
{
    public class SineRadius : MonoBehaviour
    {
        [SerializeField]
        private SDFPrimitive m_primitive;

        [SerializeField]
        private bool m_absoluteValue;

        [SerializeField]
        private float m_amplitude;

        [SerializeField]
        private float m_period;

        [SerializeField]
        private bool m_isRunning = false;

        private float m_startRadius;
        private float m_t = 0f;

        private void Reset()
        {
            m_startRadius = m_primitive.SphereRadius;
            m_t = 0f;
        }

        private void Start()
        {
            m_startRadius = m_primitive.SphereRadius;
            m_t = 0f;
        }

        private void Update()
        {
            if (!m_isRunning)
                return;

            float val = m_startRadius + Mathf.Cos(Mathf.PI * 2f * m_period * m_t) * m_amplitude;

            if (m_absoluteValue)
                val = Mathf.Abs(val);

            m_primitive.SetSphereRadius(val);
            m_t += Time.smoothDeltaTime;
        }
    }
}