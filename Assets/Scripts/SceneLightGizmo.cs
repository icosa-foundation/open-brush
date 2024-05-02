// Copyright 2024 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using UnityEngine;

namespace TiltBrush
{
    public class SceneLightGizmo : MonoBehaviour
    {
        [SerializeField] private Transform m_ConeMesh;
        [SerializeField] private Transform m_SphereMesh;
        [SerializeField] private Transform m_CylinderMesh;
        [SerializeField] private Transform m_Icon;

        private Vector3 initialLocalScale;

        public Transform ActiveMeshTransform
        {
            get
            {
                if (m_ConeMesh.gameObject.activeSelf) return m_ConeMesh;
                if (m_SphereMesh.gameObject.activeSelf) return m_SphereMesh;
                if (m_CylinderMesh.gameObject.activeSelf) return m_CylinderMesh;
                return null;
            }
        }

        private void Awake()
        {
            initialLocalScale = transform.localScale;
            Coords.CanvasPoseChanged += OnCanvasPoseChanged;
            var modelWidget = GetComponentInParent<ModelWidget>();
            if (modelWidget != null) modelWidget.ScaleChanged += OnScaleChanged;
        }

        private void OnDestroy()
        {
            Coords.CanvasPoseChanged -= OnCanvasPoseChanged;
            var modelWidget = GetComponentInParent<ModelWidget>();
            if (modelWidget != null) modelWidget.ScaleChanged -= OnScaleChanged;
        }

        private void OnCanvasPoseChanged(TrTransform prev, TrTransform current)
        {
            transform.localScale = initialLocalScale;
        }

        private void OnScaleChanged()
        {
            transform.localScale = initialLocalScale;
        }

        public void SetupLightGizmos(Light light)
        {
            SetColor(light.color, light.intensity);
            switch (light.type)
            {
                case LightType.Directional:
                    m_ConeMesh.gameObject.SetActive(false);
                    m_SphereMesh.gameObject.SetActive(false);
                    m_CylinderMesh.gameObject.SetActive(true);
                    break;
                case LightType.Point:
                    m_ConeMesh.gameObject.SetActive(false);
                    m_SphereMesh.gameObject.SetActive(true);
                    m_CylinderMesh.gameObject.SetActive(false);
                    break;
                case LightType.Spot:
                    m_ConeMesh.gameObject.SetActive(true);
                    m_SphereMesh.gameObject.SetActive(false);
                    m_CylinderMesh.gameObject.SetActive(false);
                    SetCone(light.spotAngle);
                    break;
            }
        }

        public void SetCone(float angle)
        {
            // TODO visualize range?
            var scale = CalculateScale(2, 2, angle);
            m_ConeMesh.localScale = new Vector3(scale, scale, 1f);
        }

        public void SetColor(Color color, float intensity)
        {
            foreach (var mr in m_Icon.GetComponentsInChildren<MeshRenderer>())
            {
                mr.material.color = color * intensity;
            }
        }

        float CalculateScale(float baseWidth, float height, float targetAngle)
        {
            float targetAngleRadians = targetAngle * Mathf.PI / 180f;

            // Calculate the length of one of the equal sides of the original isosceles triangle
            float originalSideLength = Mathf.Sqrt(Mathf.Pow(height, 2) + Mathf.Pow(baseWidth / 2, 2));

            // Calculate the new base length required for the target base angles in the isosceles triangle
            float newBaseLength = 2f * (originalSideLength * Mathf.Sin(targetAngleRadians / 2f));
            return newBaseLength / baseWidth;
        }

        public Bounds GetBoundsForLight(Light light)
        {
            // This is called on the prefab reference - not on an instantiated object
            // Therefore we need to calculate any scaling that would be applied to the mesh
            switch (light.type)
            {
                case LightType.Directional:
                    return m_CylinderMesh.GetComponent<MeshFilter>().sharedMesh.bounds;
                case LightType.Point:
                    return m_SphereMesh.GetComponent<MeshFilter>().sharedMesh.bounds;
                case LightType.Spot:
                    var scale = CalculateScale(2, 2, light.spotAngle);
                    var bounds = m_ConeMesh.GetComponent<MeshFilter>().sharedMesh.bounds;
                    var size = new Vector3(bounds.size.x * scale, bounds.size.y * scale, bounds.size.z);
                    return new Bounds(bounds.center, size);
                default:
                    return new Bounds();
            }
        }
    }
}
