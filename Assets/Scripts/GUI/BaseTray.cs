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

using System.Collections;
using UnityEngine;

namespace TiltBrush
{

    public abstract class BaseTray : UIComponent
    {
        [SerializeField] protected GameObject m_Mesh;
        [SerializeField] private Renderer m_Border;
        [SerializeField] private float m_AnimateSpeed;
        [SerializeField] protected Vector2 m_AnimateRange;
        [Tooltip("Horizontal spacing used when more than one tray on this panel is showing.")]
        [SerializeField] private float m_TrayWidth = 0.45f;

        private UIComponentManager m_UIComponentManager;
        private Coroutine m_AnimationCoroutine;
        private Coroutine m_SlideCoroutine;
        private float m_BaseLocalX;
        protected bool m_AnimateIn;
        private bool m_AnimateWhenEnabled;
        public BaseTool.ToolType m_ShowOnToolType;

        override protected void Awake()
        {
            base.Awake();
            m_UIComponentManager = GetComponent<UIComponentManager>();
            // Cache before anything can shift us into a different column.
            m_BaseLocalX = transform.localPosition.x;
            App.Switchboard.ToolChanged += OnToolChanged;
            App.Switchboard.SelectionChanged += OnSelectionChanged;
        }

        override protected void Start()
        {
            base.Start();

            // Begin disabled. Do this in Start() instead of Awake() so that button descriptions have a
            // chance to instantiate at the right position.
            m_AnimateIn = false;
            Vector3 localScale = transform.localScale;
            localScale.x = m_AnimateRange.x;
            transform.localScale = localScale;
            m_Mesh.SetActive(false);
            m_Collider.enabled = false;
        }

        override protected void OnDestroy()
        {
            base.OnDestroy();
            App.Switchboard.ToolChanged -= OnToolChanged;
            App.Switchboard.SelectionChanged -= OnSelectionChanged;
        }

        private void OnEnable()
        {
            if (m_AnimateWhenEnabled)
            {
                m_AnimationCoroutine = StartCoroutine(Animate());
                m_AnimateWhenEnabled = false;
            }
        }

        override protected void OnDisable()
        {
            if (m_SlideCoroutine != null)
            {
                StopCoroutine(m_SlideCoroutine);
                m_SlideCoroutine = null;
                SetLocalX(ColumnLocalX());
            }
            if (m_AnimationCoroutine != null)
            {
                // Skip to the end of animation
                StopCoroutine(m_AnimationCoroutine);
                Vector3 localScale = transform.localScale;
                localScale.x = m_AnimateIn ? m_AnimateRange.y : m_AnimateRange.x;
                transform.localScale = localScale;
                m_AnimationCoroutine = null;
            }
        }

        override public void SetColor(Color color)
        {
            base.SetColor(color);
            m_UIComponentManager.SetColor(color);
            m_Border.material.SetColor("_Color", color);
        }

        override public void UpdateVisuals()
        {
            base.UpdateVisuals();
            m_UIComponentManager.UpdateVisuals();
        }

        override public bool UpdateStateWithInput(bool inputValid, Ray inputRay,
                                                  GameObject parentActiveObject, Collider parentCollider)
        {
            if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider))
            {
                if (parentActiveObject == null || parentActiveObject == gameObject)
                {
                    if (BasePanel.DoesRayHitCollider(inputRay, GetCollider()))
                    {
                        m_UIComponentManager.UpdateUIComponents(inputRay, inputValid, parentCollider);
                        return true;
                    }
                }
            }
            return false;
        }

        override public void ResetState()
        {
            base.ResetState();
            m_UIComponentManager.Deactivate();
        }

        override public bool RaycastAgainstCustomCollider(Ray ray,
                                                          out RaycastHit hitInfo, float dist)
        {
            return BasePanel.DoesRayHitCollider(ray, GetCollider(), out hitInfo);
        }

        /// UnityEvent target for trays that are driven by a toggle button rather than by
        /// tool state.
        public void ToggleTray(ActionToggleButton btn)
        {
            EnableTray(btn.ToggleState);
        }

        public void EnableTray(bool activate)
        {
            if (activate != m_AnimateIn)
            {
                DoAnimateIn();
            }
        }

        protected virtual void OnToolChanged()
        {
            bool isLinkedTool = SketchSurfacePanel.m_Instance.GetCurrentToolType() ==
                m_ShowOnToolType;
            EnableTray(isLinkedTool);
        }

        public void DoAnimateIn()
        {
            if (m_AnimationCoroutine != null)
            {
                StopCoroutine(m_AnimationCoroutine);
            }
            m_AnimateIn = !m_AnimateIn;

            // Our showing state just changed, so every tray on this panel may need to move.
            RefreshSiblingColumns();

            // If we get a callback that our tool changed while we're inactive, don't try to
            // start our coroutine until we've been enabled.
            if (isActiveAndEnabled)
            {
                m_AnimationCoroutine = StartCoroutine(Animate());
            }
            else
            {
                m_AnimateWhenEnabled = true;
            }
        }

        protected virtual void OnSelectionChanged()
        {
        }

        /// Trays share a column on the panel, so a tray that is showing while trays before it
        /// are also showing has to step aside. Ordering comes from sibling index, so the layout
        /// depends only on which trays are open, never on the order they were opened.
        private void RefreshSiblingColumns()
        {
            Transform parent = transform.parent;
            if (parent == null)
            {
                RefreshColumn();
                return;
            }
            for (int i = 0; i < parent.childCount; ++i)
            {
                BaseTray tray = parent.GetChild(i).GetComponent<BaseTray>();
                if (tray != null)
                {
                    tray.RefreshColumn();
                }
            }
        }

        private void RefreshColumn()
        {
            SlideTo(ColumnLocalX());
        }

        private float ColumnLocalX()
        {
            int column = 0;
            Transform parent = transform.parent;
            if (parent != null)
            {
                for (int i = 0; i < parent.childCount; ++i)
                {
                    Transform child = parent.GetChild(i);
                    if (child == transform)
                    {
                        break;
                    }
                    BaseTray tray = child.GetComponent<BaseTray>();
                    if (tray != null && tray.m_AnimateIn)
                    {
                        ++column;
                    }
                }
            }
            return m_BaseLocalX + column * m_TrayWidth;
        }

        private void SlideTo(float targetX)
        {
            if (m_SlideCoroutine != null)
            {
                StopCoroutine(m_SlideCoroutine);
                m_SlideCoroutine = null;
            }
            // Nothing to animate if we're hidden or off; just be in the right place.
            if (!isActiveAndEnabled || !m_AnimateIn)
            {
                SetLocalX(targetX);
                return;
            }
            m_SlideCoroutine = StartCoroutine(Slide(targetX));
        }

        private void SetLocalX(float x)
        {
            Vector3 localPos = transform.localPosition;
            localPos.x = x;
            transform.localPosition = localPos;
        }

        IEnumerator Slide(float targetX)
        {
            while (transform.localPosition.x != targetX)
            {
                SetLocalX(Mathf.MoveTowards(
                    transform.localPosition.x, targetX, Time.deltaTime * m_AnimateSpeed));
                yield return null;
            }
            m_SlideCoroutine = null;
        }

        IEnumerator Animate()
        {
            Vector3 localScale = transform.localScale;
            if (m_AnimateIn)
            {
                // Enable right away for animating in.
                m_Mesh.SetActive(true);
                m_Collider.enabled = true;

                float x = localScale.x;
                while (x < m_AnimateRange.y)
                {
                    x += Time.deltaTime * m_AnimateSpeed;
                    if (x >= m_AnimateRange.y)
                    {
                        x = m_AnimateRange.y;
                    }
                    localScale.x = x;
                    transform.localScale = localScale;
                    yield return null;
                }
            }
            else
            {
                // Disable collider immediately so we can't select something.
                m_Collider.enabled = false;

                float x = localScale.x;
                while (x > m_AnimateRange.x)
                {
                    x -= Time.deltaTime * m_AnimateSpeed;
                    if (x <= m_AnimateRange.x)
                    {
                        x = m_AnimateRange.x;
                        // Disable mesh after animation for animating out.
                        m_Mesh.SetActive(false);
                    }
                    localScale.x = x;
                    transform.localScale = localScale;
                    yield return null;
                }
            }
            m_AnimationCoroutine = null;
        }
    }

} // namespace TiltBrush
