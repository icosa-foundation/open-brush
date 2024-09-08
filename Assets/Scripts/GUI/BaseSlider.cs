// Copyright 2020 The Tilt Brush Authors
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

    public class BaseSlider : UIComponent
    {
        [Serializable]
        public enum Orientation
        {
            Horizontal,
            Vertical
        }

        [SerializeField] public GameObject m_Nob;
        [SerializeField] private Renderer m_Mesh;
        [SerializeField] private Orientation m_Orientation;

        [NonSerialized] public Vector3 m_MeshScale;
        protected float m_CurrentValue;
        protected bool m_IsAvailable;

        private Renderer[] m_TintableMeshes;

        public virtual float GetCurrentValue() { return m_CurrentValue; }
        protected bool IsAvailable() { return m_IsAvailable; }

        protected void SetAvailable(bool available)
        {
            m_IsAvailable = available;
            SetDescriptionVisualsAvailable(m_IsAvailable);
            if (m_Orientation == Orientation.Vertical)
            {
                // Fix distortion due to the parent having a non-uniform scale.
                m_Description.transform.localScale = Vector3.one;
            }
        }

        override protected void Awake()
        {
            base.Awake();

            if (m_Mesh != null)
            {
                m_MeshScale = m_Mesh.transform.localScale;
            }
            m_TintableMeshes = GetComponentsInChildren<Renderer>();
            SetAvailable(true);
        }

        override protected void OnDescriptionChanged()
        {
            m_Description.transform.position = m_Nob.transform.position;
            m_Description.transform.rotation = m_Nob.transform.rotation;
            m_Description.transform.parent = m_Nob.transform;
        }

        override public void SetColor(Color color)
        {
            if (!IsAvailable())
            {
                float alpha = color.a;
                color *= kUnavailableTintAmount;
                color.a = alpha;
            }

            base.SetColor(color);
            for (int i = 0; i < m_TintableMeshes.Length; ++i)
            {
                m_TintableMeshes[i].material.color = color;
            }
        }

        virtual public void UpdateValue(float fValue)
        {
            m_CurrentValue = fValue;
        }

        public virtual void SetSliderPositionToReflectValue()
        {
            if (m_Nob != null)
            {
                Vector3 vLocalPos = m_Nob.transform.localPosition;
                switch (m_Orientation)
                {
                    case Orientation.Horizontal:
                        vLocalPos.x = (m_CurrentValue - 0.5f) * m_MeshScale.x;
                        break;
                    case Orientation.Vertical:
                        vLocalPos.y = (m_CurrentValue - 0.5f) * m_MeshScale.x;
                        break;
                }
                m_Nob.transform.localPosition = vLocalPos;
            }
        }

        override public void ButtonPressed(RaycastHit rHitInfo)
        {
            if (IsAvailable())
            {
                PositionSliderNob(rHitInfo.point);
            }
            SetDescriptionActive(true);
        }

        override public void ButtonHeld(RaycastHit rHitInfo)
        {
            if (IsAvailable())
            {
                PositionSliderNob(rHitInfo.point);
            }
            SetDescriptionActive(true);
        }

        override public void GainFocus()
        {
            SetDescriptionActive(true);
        }

        public override void ResetState()
        {
            SetDescriptionActive(false);
        }

        protected void PositionSliderNob(Vector3 pos_WS)
        {
            if (m_Nob != null)
            {
                m_Nob.transform.position = pos_WS;
                Vector3 vLocalPos = m_Nob.transform.localPosition;
                float fScaledBounds;
                float fValue = 0;
                switch (m_Orientation)
                {
                    case Orientation.Horizontal:
                        fScaledBounds = 0.5f * m_MeshScale.x;
                        vLocalPos.x = Mathf.Clamp(vLocalPos.x, -fScaledBounds, fScaledBounds);
                        vLocalPos.y = 0.0f;
                        fValue = (vLocalPos.x / m_MeshScale.x) + 0.5f;
                        break;
                    case Orientation.Vertical:
                        fScaledBounds = 0.5f * m_MeshScale.x;
                        vLocalPos.x = 0.0f;
                        vLocalPos.y = Mathf.Clamp(vLocalPos.y, -fScaledBounds, fScaledBounds);
                        fValue = (vLocalPos.y / m_MeshScale.x) + 0.5f;
                        break;
                }
                vLocalPos.z = 0.0f;
                m_Nob.transform.localPosition = vLocalPos;
                UpdateValue(fValue);
                OnPositionSliderNobUpdated();
            }
        }

        virtual protected void OnPositionSliderNobUpdated() { }
    }
} // namespace TiltBrush
