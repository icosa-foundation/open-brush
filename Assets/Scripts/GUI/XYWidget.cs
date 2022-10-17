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
using TMPro;
using UnityEngine;

namespace TiltBrush
{

    public class XYWidget : UIComponent
    {
        public int m_Param;
        [SerializeField] public GameObject m_Nob;
        [SerializeField] private Renderer m_Mesh;
        [SerializeField] private TextMeshPro valueText;
        [SerializeField] public sliderEvent onUpdateValue;

        [NonSerialized] public Vector3 m_MeshScale;
        protected Vector2 m_CurrentValue;
        protected bool m_IsAvailable;

        public Vector2 m_InitialValue;

        public Vector2 m_Min = -Vector2.one;
        public Vector2 m_Max = Vector2.one;


        private Renderer[] m_TintableMeshes;

        public Vector2 GetCurrentValue() { return m_CurrentValue; }
        protected bool IsAvailable() { return m_IsAvailable; }

        protected void SetAvailable(bool available)
        {
            m_IsAvailable = available;
            SetDescriptionVisualsAvailable(m_IsAvailable);
        }

        private void _UpdateValueAbsolute(Vector2 fValue)
        {
            valueText.text = FormatValue(fValue);
            onUpdateValue.Invoke(new Vector3(m_Param, fValue.x, fValue.y));
            m_CurrentValue = new Vector2(
                Mathf.InverseLerp(m_Min.x, m_Max.x, fValue.x),
                Mathf.InverseLerp(m_Min.y, m_Max.y, fValue.y)
            );
            SetSliderPositionToReflectValue();
        }

        private string FormatValue(Vector2 val)
        {
            return $"{Mathf.Round(val.x * 10) / 10},{Mathf.Round(val.y * 10) / 10}";
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
            ResetToInitialValues();
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

        float remap(float s, float a1, float a2, float b1, float b2)
        {
            return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
        }

        public void UpdateValue(Vector2 fValue)
        {
            var val = new Vector2(
                remap(fValue.x, 0, 1, m_Min.x, m_Max.x),
                remap(fValue.y, 0, 1, m_Min.y, m_Max.y)
            );
            _UpdateValueAbsolute(val);
        }

        public void SetSliderPositionToReflectValue()
        {
            if (m_Nob != null)
            {
                Vector3 vLocalPos = m_Nob.transform.localPosition;
                vLocalPos.x = (m_CurrentValue.x - 0.5f) * m_MeshScale.x;
                vLocalPos.y = (m_CurrentValue.y - 0.5f) * m_MeshScale.y;
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

        public void ResetToInitialValues()
        {
            _UpdateValueAbsolute(m_InitialValue);
        }

        protected void PositionSliderNob(Vector3 pos_WS)
        {
            if (m_Nob != null)
            {
                m_Nob.transform.position = pos_WS;
                Vector3 vLocalPos = m_Nob.transform.localPosition;
                float fScaledBoundsX = 0.5f * m_MeshScale.x;
                float fScaledBoundsY = 0.5f * m_MeshScale.y;
                vLocalPos.x = Mathf.Clamp(vLocalPos.x, -fScaledBoundsX, fScaledBoundsX);
                vLocalPos.y = Mathf.Clamp(vLocalPos.y, -fScaledBoundsY, fScaledBoundsY);
                vLocalPos.z = 0.0f;
                m_Nob.transform.localPosition = vLocalPos;
                Vector2 fValue = new Vector2(
                    (vLocalPos.x / m_MeshScale.x) + 0.5f,
                    (vLocalPos.y / m_MeshScale.y) + 0.5f
                );
                UpdateValue(fValue);
                OnPositionSliderNobUpdated();
            }
        }

        virtual protected void OnPositionSliderNobUpdated() { }
    }
} // namespace TiltBrush
