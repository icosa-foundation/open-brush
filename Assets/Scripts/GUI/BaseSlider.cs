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
        [SerializeField] public GameObject m_Nob;
        [SerializeField] public Renderer m_Mesh;

        [NonSerialized] public Vector3 m_MeshScale;
        protected float m_CurrentValue;
        protected bool m_IsAvailable;

        private Renderer[] m_TintableMeshes;

        public float GetCurrentValue() { return m_CurrentValue; }
        protected bool IsAvailable() { return m_IsAvailable; }

        protected void SetAvailable(bool available)
        {
            m_IsAvailable = available;
            SetDescriptionVisualsAvailable(m_IsAvailable);
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

        public void SetSliderPositionToReflectValue()
        {
            if (m_Nob != null)
            {
                Vector3 vLocalPos = m_Nob.transform.localPosition;
                vLocalPos.x = (m_CurrentValue - 0.5f) * m_Mesh.transform.localScale.x;
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
                print("SLIDER POS = " + rHitInfo.point);
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
                float fScaledBounds = 0.5f *  m_Mesh.transform.localScale.x;
                vLocalPos.x = Mathf.Clamp(vLocalPos.x, -fScaledBounds, fScaledBounds);
                vLocalPos.y = 0.0f;
                vLocalPos.z = 0.0f;
                m_Nob.transform.localPosition = vLocalPos;
                float fValue = (vLocalPos.x /  m_Mesh.transform.localScale.x) + 0.5f;
                UpdateValue(fValue);
                OnPositionSliderNobUpdated();
            }
        }

        // For @Animation
        public void setSliderValue(float fValue){
     
            float newVal = (fValue - 0.5f)* m_Mesh.transform.localScale.x;
     

             print("SLIDING ==" + fValue + "  newval==" + newVal + " Mesh scale==" + m_Mesh.transform.localScale.x);
            Vector3 vLocalPos = m_Nob.transform.localPosition;
            m_Nob.transform.localPosition = new Vector3(newVal,vLocalPos.y,vLocalPos.z);
            // UpdateValue(fValue);
            

        }
        public void setSliderScale(float scaleX){

                    m_Mesh.transform.localScale = new Vector3(
                        scaleX,
                        m_Mesh.transform.localScale.y,
                        m_Mesh.transform.localScale.z
                    );
                    // Collider.transform.localScale = new Vector3(scaleX,localScale.y,localScale.z);
        }

        virtual protected void OnPositionSliderNobUpdated() { }
    }
} // namespace TiltBrush
