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
using Es.InkPainter;
using UnityEngine;
using Math = Es.InkPainter.Math;

namespace TiltBrush
{
    public class TexturePainterManager : MonoBehaviour
    {
        public Texture2D DefaultCanvasTexture;
        public float m_BrushSize = 0.1f;
        public Brush brush;

        [NonSerialized] public static TexturePainterManager m_Instance;

        private bool m_EnableLine;
        private TrTransform m_PointerTransform;



        public float PointerPressure { get; set; }

        void Awake()
        {
            m_Instance = this;
        }

        void Update()
        {
            if (m_EnableLine)
            {
                Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
                Vector3 pos = rAttachPoint.position;
                Vector3 vec = rAttachPoint.forward;

                bool success = true;
                RaycastHit hitInfo;

                LayerMask texturePaintLayer = LayerMask.GetMask("TexturePaint");
                LayerMask mainCanvasLayer = LayerMask.GetMask("MainCanvas");

                if (Physics.Raycast(pos, vec, out hitInfo, 4f, texturePaintLayer))
                {
                    brush.Scale = m_BrushSize * PointerPressure;
                    brush.RotateAngle = Mathf.PerlinNoise(Time.time, 0) * 360f;
                    InkCanvas canvas = hitInfo.transform.GetComponent<InkCanvas>();
                    if (canvas != null)
                    {
                        DebugVisualization.ShowPosition(hitInfo.point);
                        canvas.Paint(brush, hitInfo);
                    }
                    else
                    {
                        Debug.LogWarning($"No paint object on: {hitInfo.transform}");
                    }
                }
                else if (Physics.Raycast(pos, vec, out hitInfo, 10f, mainCanvasLayer))
                {
                    var hitObject = hitInfo.transform.gameObject;

                    // Is this part of a ModelWidget?
                    var modelWidget = hitObject.GetComponentInParent<ModelWidget>();
                    if (modelWidget == null)
                    {
                        Debug.LogWarning($"{hitInfo.transform} is not part of a ModelWidget");
                        return;
                    }

                    var objModelScript = modelWidget.GetComponentInChildren<ObjModelScript>();
                    foreach (var mesh in objModelScript.m_MeshChildren)
                    {
                        InkCanvas canvas = mesh.gameObject.GetComponent<InkCanvas>();
                        if (canvas == null)
                        {
                            mesh.GetComponent<MeshRenderer>().material.mainTexture = DefaultCanvasTexture;

                            var paintSet = new List<InkCanvas.PaintSet>
                            {
                                new(
                                    mainTextureName: "_MainTex",
                                    normalTextureName: "_BumpMap",
                                    heightTextureName: "_HeightMap",
                                    useMainPaint: true,
                                    useNormalPaint: false,
                                    useHeightPaint: false
                                )
                            };
                            mesh.gameObject.AddInkCanvas(paintSet);
                            mesh.gameObject.layer = LayerMask.NameToLayer("TexturePaint");
                        }

                        var collider = mesh.gameObject.GetComponent<MeshCollider>();
                        if (collider == null)
                        {
                            mesh.gameObject.AddComponent<MeshCollider>();
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("No hit");
                }
            }
        }

        public bool EnablePainting(bool enable)
        {
            m_EnableLine = enable;
            brush.Color = PointerManager.m_Instance.m_lastChosenColor;
            return m_EnableLine;
        }

        public void AdjustBrushSize01(float mAdjustSizeScalar)
        {
            m_BrushSize += mAdjustSizeScalar;
        }

        public float GetBrushSize01(InputManager.ControllerName _)
        {
            return m_BrushSize;
        }
    }
}
