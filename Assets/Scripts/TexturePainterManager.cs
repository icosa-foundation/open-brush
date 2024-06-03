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
using Es.InkPainter;
using UnityEngine;

namespace TiltBrush
{
    public class TexturePainterManager : MonoBehaviour, ITextureBrushController
    {
        public Texture2D DefaultCanvasTexture;
        public float m_BrushSize = 0.5f;
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
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 pos = rAttachPoint.position;
            Vector3 vec = rAttachPoint.forward;

            bool success = true;
            RaycastHit hitInfo;

            LayerMask texturePaintLayer = LayerMask.GetMask("TexturePaint");
            LayerMask mainCanvasLayer = LayerMask.GetMask("MainCanvas");

            if (Physics.Raycast(pos, vec, out hitInfo, 4f, texturePaintLayer))
            {
                brush.Scale = m_BrushSize * 0.05f; // Arbitrary scale factor
                brush.ImageAlphaMultiplier = PointerPressure;
                //brush.RotateAngle = Mathf.PerlinNoise(Time.time, 0) * 360f;
                InkCanvas canvas = hitInfo.transform.GetComponent<InkCanvas>();
                if (canvas != null)
                {
                    DebugVisualization.ShowPosition(hitInfo.point);
                    if (m_EnableLine)
                    {
                        canvas.Paint(brush, hitInfo);
                    }
                }
                else
                {
                    Debug.LogWarning($"No paint object on: {hitInfo.transform}");
                }
            }
            // else if (Physics.Raycast(pos, vec, out hitInfo, 10f, mainCanvasLayer))
            // {
            //     var hitObject = hitInfo.transform.gameObject;
            //     var widget = hitObject.GetComponentInParent<GrabWidget>();
            //     var cmd = new EnableTexturePaintingCommand(widget);
            //     if (cmd.Widget != null)
            //     {
            //         SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
            //     }
            //     else
            //     {
            //         Debug.LogWarning($"{hitInfo.transform} is not a valid paint target");
            //     }
            // }
            else
            {
                Debug.LogWarning("No hit");
            }
        }

        public bool EnablePainting(bool enable)
        {
            if (!enable)
            {
                brush.ResetSpacingCalculation();
            }
            m_EnableLine = enable;
            brush.Color = PointerManager.m_Instance.PointerColor;
            return m_EnableLine;
        }

        public void AdjustBrushSize01(float mAdjustSizeScalar)
        {
            m_BrushSize += mAdjustSizeScalar;
            m_BrushSize = Mathf.Clamp(m_BrushSize, 0.1f, 1f);
        }

        public float GetBrushSize01(InputManager.ControllerName _)
        {
            return m_BrushSize;
        }

        public void SetBrushTexture(RenderTexture brushTexture)
        {
            brush.BrushTexture = brushTexture;
        }
    }
}
