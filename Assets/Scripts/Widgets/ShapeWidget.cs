// Copyright 2026 The Open Brush Authors
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

using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public abstract class ShapeWidget : GrabWidget
    {
        [SerializeField] protected float m_MinSize_CS;
        [SerializeField] protected float m_MaxSize_CS;

        protected Collider m_Collider;
        protected float m_Size = 1.0f;
        protected bool m_SkipIntroAnim;
        protected float m_PreviousShowRatio;

        protected virtual IWidgetShape Shape => null;

        public override float GetActivationScore(Vector3 vControllerPos, InputManager.ControllerName name)
        {
            return Shape != null
                ? Shape.GetActivationScore(transform, m_Collider, GetSignedWidgetSize(), m_MaxSize_CS, vControllerPos)
                : base.GetActivationScore(vControllerPos, name);
        }

        public override Bounds GetBounds_SelectionCanvasSpace()
        {
            return Shape != null
                ? Shape.GetSelectionCanvasBounds(m_Collider, base.GetBounds_SelectionCanvasSpace())
                : base.GetBounds_SelectionCanvasSpace();
        }

        protected override void Awake()
        {
            base.Awake();

            // Use transform.localScale.x because prefabs have scales != Vector3.one.
            m_Size = transform.localScale.x / Coords.CanvasPose.scale;

            // Awake() is called before the transform is parented, so apply Canvas scale manually.
            var sizeRange = GetWidgetSizeRange();
            if (m_Size < sizeRange.x)
            {
                m_Size = sizeRange.x;
                transform.localScale = m_Size * Vector3.one * Coords.CanvasPose.scale;
            }
            if (m_Size > sizeRange.y)
            {
                m_Size = sizeRange.y;
                transform.localScale = m_Size * Vector3.one * Coords.CanvasPose.scale;
            }

            m_Collider = GetComponentInChildren<Collider>();
            InitSnapGhost(m_Collider.transform, transform);
            m_HighlightMeshFilters = m_TintableMeshes.Select(x => x.GetComponent<MeshFilter>()).ToArray();
        }

        protected override void OnShow()
        {
            base.OnShow();

            if (!m_SkipIntroAnim)
            {
                m_IntroAnimState = IntroAnimState.In;
                Debug.Assert(!IsMoving(), "Shouldn't have velocity!");
                ClearVelocities();
                m_IntroAnimValue = 0.0f;
                UpdateIntroAnim();
            }
            else
            {
                m_IntroAnimState = IntroAnimState.On;
            }

            UpdateMaterialScale();
            SpoofScaleForShowAnim(GetShowRatio());
        }

        public override void RestoreFromToss()
        {
            m_SkipIntroAnim = true;
            base.RestoreFromToss();
        }

        public override Vector2 GetWidgetSizeRange()
        {
            return new Vector2(m_MinSize_CS, m_MaxSize_CS);
        }

        protected virtual void UpdateScale()
        {
            transform.localScale = m_Size * Vector3.one;
            UpdateMaterialScale();
        }

        public override float GetSignedWidgetSize()
        {
            return m_Size;
        }

        protected override void SetWidgetSizeInternal(float fScale)
        {
            m_Size = fScale;
            UpdateScale();
        }

        protected void UpdateMaterialScale()
        {
            Vector3 Mul(Vector3 a, Vector3 b) => Vector3.Scale(a, b);

            if (m_TintableMeshes != null)
            {
                Vector3 parentScale = transform.parent == null ? Vector3.one : transform.parent.localScale;
                parentScale.x = 1;

                foreach (Renderer renderer in m_TintableMeshes)
                {
                    renderer.material.SetVector(
                        "_LocalScale",
                        Mul(parentScale, Mul(transform.localScale, renderer.transform.localScale)));
                }
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            float showRatio = GetShowRatio();
            if (m_PreviousShowRatio != showRatio)
            {
                SpoofScaleForShowAnim(showRatio);
                m_PreviousShowRatio = showRatio;
            }
        }

        protected virtual void SpoofScaleForShowAnim(float showRatio)
        {
            transform.localScale = m_Size * showRatio * Vector3.one;
        }
    }
} // namespace TiltBrush
