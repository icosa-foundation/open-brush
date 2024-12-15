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

        public enum TexturePaintMode
        {
            Paint,
            Erase,
            Fill
        }

        [NonSerialized] public static TexturePainterManager m_Instance;
        [NonSerialized] public TexturePaintMode m_CurrentMode;

        private bool m_EnableLine;
        private TrTransform m_PointerTransform;

        public float PointerPressure { get; set; }

        void Awake()
        {
            m_Instance = this;
        }

        void Update()
        {
            if (SketchSurfacePanel.m_Instance.ActiveToolType != BaseTool.ToolType.TexturePaintTool)
            {
                return;
            }
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 pos = rAttachPoint.position;
            Vector3 vec = rAttachPoint.forward;
            Quaternion rot = rAttachPoint.rotation;
            // Get the rotation around the forward vector
            float angle = rot.eulerAngles.z;

            bool success = true;
            RaycastHit hitInfo;

            LayerMask texturePaintLayer = LayerMask.GetMask("TexturePaint");
            LayerMask mainCanvasLayer = LayerMask.GetMask("MainCanvas");

            if (Physics.Raycast(pos, vec, out hitInfo, 4f, texturePaintLayer))
            {
                brush.Scale = m_BrushSize * PointerPressure * 0.05f; // Arbitrary scale factor
                brush.ImageAlphaMultiplier = PointerPressure;
                brush.RotateAngle = angle;
                InkCanvas canvas = hitInfo.transform.GetComponent<InkCanvas>();
                if (canvas != null)
                {
                    DebugVisualization.ShowPosition(hitInfo.point, 0.1f);
                    if (m_EnableLine)
                    {
                        switch (m_CurrentMode)
                        {
                            case TexturePaintMode.Paint:
                                canvas.Paint(brush, hitInfo);
                                break;
                            case TexturePaintMode.Erase:
                                canvas.Erase(brush, hitInfo);
                                break;
                            case TexturePaintMode.Fill:
                                canvas.Fill(brush, hitInfo);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
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

        public bool CanBeMadePaintable(GrabWidget grabWidget)
        {
            if (grabWidget == null) return false;
            return grabWidget.GetComponentInChildren<InkCanvas>() == null &&
                (
                    grabWidget is ModelWidget ||
                    grabWidget is StencilWidget
                );
        }
    }
}
