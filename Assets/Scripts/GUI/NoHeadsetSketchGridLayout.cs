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

using UnityEngine;
using UnityEngine.UI;

namespace TiltBrush
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public class NoHeadsetSketchGridLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform m_Viewport;
        [SerializeField] private float m_MinCellWidth = 78f;
        [SerializeField] private float m_MaxCellWidth = 132f;
        [SerializeField] private float m_CellAspect = 1.32f;
        [SerializeField] private Vector2 m_Spacing = new Vector2(8f, 8f);
        [SerializeField] private int m_MinColumns = 2;
        [SerializeField] private int m_MaxColumns = 5;

        private GridLayoutGroup m_Grid;
        private Vector2 m_LastViewportSize;

        private void Awake()
        {
            m_Grid = GetComponent<GridLayoutGroup>();
            RefreshLayout();
        }

        private void Update()
        {
            if (m_Viewport == null)
            {
                return;
            }

            Vector2 size = m_Viewport.rect.size;
            if (size == m_LastViewportSize)
            {
                return;
            }
            m_LastViewportSize = size;
            RefreshLayout(size);
        }

        public void SetViewport(RectTransform viewport)
        {
            m_Viewport = viewport;
            RefreshLayout();
        }

        public void SetCellAspect(float cellAspect)
        {
            if (cellAspect <= 0f || Mathf.Approximately(m_CellAspect, cellAspect))
            {
                return;
            }

            m_CellAspect = cellAspect;
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (m_Grid == null)
            {
                m_Grid = GetComponent<GridLayoutGroup>();
            }
            if (m_Viewport == null)
            {
                return;
            }

            RefreshLayout(m_Viewport.rect.size);
        }

        private void RefreshLayout(Vector2 viewportSize)
        {
            float width = Mathf.Max(1f, viewportSize.x);
            int columns = Mathf.FloorToInt((width + m_Spacing.x) / (m_MinCellWidth + m_Spacing.x));
            if (columns < m_MinColumns)
            {
                float minColumnsWidth = m_MinColumns * m_MinCellWidth
                    + (m_MinColumns - 1) * m_Spacing.x;
                columns = width >= minColumnsWidth ? m_MinColumns : 1;
            }
            columns = Mathf.Clamp(columns, 1, m_MaxColumns);

            float totalSpacing = m_Spacing.x * (columns - 1);
            float cellWidth = Mathf.Min((width - totalSpacing) / columns, m_MaxCellWidth);
            float cellHeight = cellWidth / m_CellAspect;

            m_Grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            m_Grid.constraintCount = columns;
            m_Grid.spacing = m_Spacing;
            m_Grid.cellSize = new Vector2(cellWidth, cellHeight);
            m_Grid.childAlignment = TextAnchor.UpperCenter;
        }
    }
}
