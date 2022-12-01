using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IsoMesh
{
    public class Rotator : MonoBehaviour
    {
        [SerializeField]
        private Vector3 m_rotationSpeeds = Vector3.zero;

        Quaternion m_rotation;

        private void OnValidate() => m_rotation = Quaternion.Euler(m_rotationSpeeds * Time.deltaTime);
        private void Reset() => m_rotation = Quaternion.Euler(m_rotationSpeeds * Time.deltaTime);
        private void Start() => m_rotation = Quaternion.Euler(m_rotationSpeeds * Time.deltaTime);

        private void Update()
        {
            transform.localRotation = m_rotation * transform.localRotation;
        }
    }
}

