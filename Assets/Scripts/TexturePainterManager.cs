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
    public class TexturePainterManager : MonoBehaviour
    {
        public Brush brush;

        [NonSerialized] public static TexturePainterManager m_Instance;
        [NonSerialized] public TexturePaintTool m_Tool;
        [NonSerialized] public Quaternion m_OrientationAdjust;

        private bool m_EnableLine;
        private TrTransform m_PointerTransform;
        private float m_BrushSize = 0.1f;

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
                Quaternion rot = rAttachPoint.rotation * m_OrientationAdjust;

                var ray = new Ray(pos, rot * Vector3.forward);
                //DebugVisualization.ShowDirection(ray.origin, ray.direction);
                bool success = true;
                RaycastHit hitInfo;
                if (Physics.Raycast(ray, out hitInfo))
                {
                    var paintObject = hitInfo.transform.GetComponent<InkCanvas>();
                    brush.Scale = m_BrushSize * PointerPressure;
                    Debug.Log($"Brush size: {brush.Scale} painter: {paintObject} hitInfo: {hitInfo}");
                    paintObject.Paint(brush, hitInfo);
                }
                else
                {
                    Debug.Log("No hit");
                }
            }
        }

        public bool EnableLine(bool enable)
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
