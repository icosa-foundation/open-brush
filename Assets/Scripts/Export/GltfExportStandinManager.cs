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
        private Transform m_SkyStandinOriginalParent;
        private Material m_TemporarySkyMaterial;

        // Name used to identify the temporary sky material in AfterMaterialExport.
        public const string kSkyStandinMaterialName = "ob_sky_standin";

        void Awake()
        {
            m_Instance = this;
        }

        public void CreateSkyStandin()
        {
            var settings = SceneSettings.m_Instance;
            // Include inactive children in case the sphere was previously hidden.
            var renderer = m_TemporarySkySphere.GetComponentInChildren<MeshRenderer>(true);

            if (settings.HasCustomSkybox())
            {
                // Custom panoramic image skybox: embed the image as an unlit sphere texture.
                Texture2D skyTex = RenderSettings.skybox != null
                    ? RenderSettings.skybox.mainTexture as Texture2D
                    : null;
                if (skyTex != null)
                {
                    m_TemporarySkyMaterial = new Material(Shader.Find("Unlit/Texture"));
                    m_TemporarySkyMaterial.mainTexture = skyTex;
                }
                else
                {
                    m_TemporarySkyMaterial = CreateGradientMaterial(settings.SkyColorA, settings.SkyColorB);
                }
            }
            else if (settings.InGradient)
            {
                // Gradient sky: bake the two sky colors into a vertical gradient texture.
                // The gradient runs from SkyColorA (bottom/south pole) to SkyColorB (top/north pole),
                // matching the default LinearGradient shader direction (Vector3.up).
                m_TemporarySkyMaterial = CreateGradientMaterial(settings.SkyColorA, settings.SkyColorB);
            }
            else
            {
                // Preset cubemap environment: cubemaps can't map directly to a GLTF sphere, so
                // approximate with an interpolated color from the environment's sky color pair.
                Color midColor = Color.Lerp(settings.SkyColorA, settings.SkyColorB, 0.5f);
                m_TemporarySkyMaterial = new Material(Shader.Find("Unlit/Color"));
                m_TemporarySkyMaterial.color = midColor;
            }

            m_TemporarySkyMaterial.name = kSkyStandinMaterialName;
            renderer.material = m_TemporarySkyMaterial;

            // Reparent under the main canvas so the UnityGLTF exporter includes the sphere
            // in the exported hierarchy (the exporter only traverses canvas children).
            m_SkyStandinOriginalParent = m_TemporarySkySphere.parent;
            m_TemporarySkySphere.SetParent(App.Scene.MainCanvas.transform, worldPositionStays: true);
            m_TemporarySkySphere.gameObject.SetActive(true);
        }

        private static Material CreateGradientMaterial(Color colorA, Color colorB)
        {
            const int height = 256;
            var tex = new Texture2D(1, height, TextureFormat.RGB24, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++)
            {
                tex.SetPixel(0, y, Color.Lerp(colorA, colorB, y / (float)(height - 1)));
            }
            tex.Apply();
            var mat = new Material(Shader.Find("Unlit/Texture"));
            mat.mainTexture = tex;
            return mat;
        }

        public void DestroySkyStandin()
        {
            m_TemporarySkySphere.gameObject.SetActive(false);
            if (m_SkyStandinOriginalParent != null)
            {
                m_TemporarySkySphere.SetParent(m_SkyStandinOriginalParent, worldPositionStays: true);
                m_SkyStandinOriginalParent = null;
            }
            if (m_TemporarySkyMaterial != null)
            {
                Destroy(m_TemporarySkyMaterial);
                m_TemporarySkyMaterial = null;
            }
        }
    }
}
