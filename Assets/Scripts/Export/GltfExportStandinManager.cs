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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace TiltBrush
{
    public class GltfExportStandinManager : MonoBehaviour
    {
        [SerializeField] public Transform m_TemporarySkySphere;
        [NonSerialized] public static GltfExportStandinManager m_Instance;
        private List<GameObject> m_TemporaryCameras;
        private MeshRenderer m_SkyRenderer;
        private Material m_OriginalSkyMaterial;
        private bool m_OriginalSkyActive;
        private bool m_HasSkyState;

        void Awake()
        {
            m_Instance = this;
        }

        public void CreateSkyStandin()
        {
            if (m_HasSkyState) return;
            m_SkyRenderer = m_TemporarySkySphere.GetComponent<MeshRenderer>();
            m_OriginalSkyMaterial = m_SkyRenderer.sharedMaterial;
            m_OriginalSkyActive = m_TemporarySkySphere.gameObject.activeSelf;
            m_HasSkyState = true;
            m_SkyRenderer.sharedMaterial = RenderSettings.skybox;
            m_TemporarySkySphere.gameObject.SetActive(false);
        }

        public void DestroySkyStandin()
        {
            if (!m_HasSkyState) return;
            m_HasSkyState = false;
            if (m_SkyRenderer != null) m_SkyRenderer.sharedMaterial = m_OriginalSkyMaterial;
            if (m_TemporarySkySphere != null)
            {
                m_TemporarySkySphere.gameObject.SetActive(m_OriginalSkyActive);
            }
            m_SkyRenderer = null;
            m_OriginalSkyMaterial = null;
        }
    }
}

