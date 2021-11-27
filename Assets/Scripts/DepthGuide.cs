// Copyright 2021 The Tilt Brush Authors
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class DepthGuide : MonoBehaviour
    {

        [SerializeField]
        public Transform m_MainCanvas;
        [SerializeField]
        protected Transform m_Gimbal;
        [SerializeField]
        protected Renderer m_meshRenderer;

        [SerializeField]
        protected float m_MaxRadius = 2;
        protected float m_lerpRadius;

        public static DepthGuide m_instance { get; private set; }

        void Awake()
        {
            m_instance = this;
        }

        private void OnEnable()
        {
            enabled = false;
            return;
        }

        private void OnDisable()
        {
            m_meshRenderer.enabled = false;
        }

        private bool ShouldShowDepthGuide()
        {

            return InputManager.Brush.IsTrackedObjectValid && !SketchControlsScript.m_Instance.IsPinCushionShowing() && !SketchControlsScript.m_Instance.IsUserInteractingWithUI();
        }

        private void FollowBrushController()
        {
            if (!InputManager.Brush.IsTrackedObjectValid)
            {
                m_meshRenderer.enabled = false;
                return;
            }

            if (!m_meshRenderer.enabled)
            {
                m_meshRenderer.enabled = true;
                m_lerpRadius = 0;
            }

            float headDist = (transform.parent.position - transform.position).magnitude;


            if (!ShouldShowDepthGuide())
            {
                headDist = 0;
            }

            float headLerpTarget = Mathf.InverseLerp(0, m_MaxRadius, headDist);

            m_lerpRadius = Mathf.Lerp(m_lerpRadius, headLerpTarget, Time.deltaTime * 5);
            transform.localScale = Vector3.one * Mathf.Lerp(0, 0.1f * m_MaxRadius, m_lerpRadius);
            transform.position = InputManager.m_Instance.GetBrushControllerAttachPoint().position;
            m_Gimbal.rotation = m_MainCanvas.rotation;

            m_meshRenderer.material.SetFloat("_MaxDistance", headDist);
            m_meshRenderer.material.SetFloat("_Radius", m_lerpRadius * m_MaxRadius);
            m_meshRenderer.material.SetMatrix("_SceneMatrix", m_MainCanvas.worldToLocalMatrix);
            m_meshRenderer.material.SetFloat("_GridScale", m_MainCanvas.lossyScale.x);


            m_meshRenderer.material.SetVector("_WorldSpaceCursorPos", InputManager.m_Instance.GetBrushControllerAttachPoint().position);
        }

        // LateUpdate is called just before rendering
        void LateUpdate()
        {
            FollowBrushController();
        }
    }

}
