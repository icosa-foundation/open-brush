// Copyright 2022 The Open Brush Authors
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
using UnityEngine.Localization;

namespace TiltBrush.Layers
{
    public class LayerUI_Manager : MonoBehaviour
    {
        public delegate void OnActiveSceneChanged(GameObject widget);
        public static event OnActiveSceneChanged onActiveSceneChanged;

        [SerializeField] private LocalizedString m_MainLayerName;
        [SerializeField] private LocalizedString m_AdditionalLayerName;
        [SerializeField] private NavButton m_PreviousPageButton;
        [SerializeField] private NavButton m_NextPageButton;

        public List<GameObject> m_Widgets;
        private List<CanvasScript> m_Canvases;
        private int m_StartingCanvasIndex;
        private bool m_RefreshNavButtons;

        private int WidgetsPerPage => m_Widgets.Count;
        private int LastPageIndex => (m_Canvases.Count + WidgetsPerPage - 1) / WidgetsPerPage - 1;
        private int CurrentPageIndex => m_StartingCanvasIndex / WidgetsPerPage;

        private void Start()
        {
            m_StartingCanvasIndex = 0;
            ResetUI();
            m_RefreshNavButtons = true;
        }

        private void ResetUI()
        {
            m_Canvases = App.Scene.LayerCanvases.ToList();

            for (int i = 0; i < m_Widgets.Count; i++)
            {
                var widget = m_Widgets[i];
                int canvasIndex = i + m_StartingCanvasIndex;
                if (canvasIndex >= m_Canvases.Count)
                {
                    widget.SetActive(false);
                    continue;
                }
                widget.SetActive(true);

                var canvas = m_Canvases[canvasIndex];

                string layerName = canvasIndex > 0 ? canvas.name : $"{m_MainLayerName.GetLocalizedStringAsync().Result}";
                widget.GetComponentInChildren<TMPro.TextMeshPro>().text = layerName;

                // Active button means hidden layer
                widget.GetComponentInChildren<ToggleVisibilityLayerButton>().SetButtonActivation(!canvas.isActiveAndEnabled);
                widget.GetComponentInChildren<FocusLayerButton>().SetButtonActivation(canvas == App.ActiveCanvas);

                widget.GetComponentInChildren<SquashLayerButton>(includeInactive: true).gameObject.SetActive(canvasIndex != 0);
                widget.GetComponentInChildren<LayerPopupButton>(includeInactive: true).gameObject.SetActive(canvasIndex != 0);

                foreach (var btn in widget.GetComponentsInChildren<OptionButton>())
                {
                    btn.m_CommandParam = canvasIndex;
                }
            }
        }

        private void LateUpdate()
        {
            if (m_RefreshNavButtons)
            {
                // Can't do this in RefreshUI because the it doesn't take effect if the button is being interacted with
                m_PreviousPageButton.SetButtonAvailable(CurrentPageIndex > 0);
                m_NextPageButton.SetButtonAvailable(CurrentPageIndex < LastPageIndex);
                m_RefreshNavButtons = false;
            }
        }

        private void OnLayerCanvasesUpdate()
        {
            ResetUI();
        }

        // Subscribes to events
        private void OnEnable()
        {
            App.Scene.ActiveCanvasChanged += ActiveSceneChanged;
            App.Scene.LayerCanvasesUpdate += OnLayerCanvasesUpdate;
        }

        // Unsubscribes to events
        private void OnDisable()
        {
            App.Scene.ActiveCanvasChanged -= ActiveSceneChanged;
            App.Scene.LayerCanvasesUpdate -= OnLayerCanvasesUpdate;
        }

        public void DeleteLayer(int index)
        {
            var canvas = m_Canvases[index];
            if (canvas == App.Scene.MainCanvas) return; // Don't delete the main canvas
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new DeleteLayerCommand(canvas));
        }

        public void SquashLayer(int index)
        {
            var canvas = m_Canvases[index];
            var prevCanvas = m_Canvases[Mathf.Max(index - 1, 0)];
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new SquashLayerCommand(canvas, prevCanvas)
            );
        }

        public void ClearLayerContents(int index)
        {
            var canvas = m_Canvases[index];
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ClearLayerCommand(canvas));
        }

        public void AddLayer()
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new AddLayerCommand(true));
        }

        public void ToggleVisibility(GameObject widget)
        {
            CanvasScript canvas = GetCanvasFromWidget(widget);
            App.Scene.ToggleLayerVisibility(canvas);
        }

        public void SetActiveLayer(GameObject widget)
        {
            var newActiveCanvas = GetCanvasFromWidget(widget);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ActivateLayerCommand(newActiveCanvas));
        }

        private void ActiveSceneChanged(CanvasScript prev, CanvasScript current)
        {
            int canvasIndex = m_Canvases.IndexOf(current);
            int widgetIndex = canvasIndex - m_StartingCanvasIndex;
            if (widgetIndex > 0 && widgetIndex < m_Widgets.Count)
            {
                onActiveSceneChanged?.Invoke(m_Widgets[widgetIndex]);
            }
            var desiredPageIndex = canvasIndex / WidgetsPerPage;
            GotoPage(desiredPageIndex);
        }

        private CanvasScript GetCanvasFromWidget(GameObject widget)
        {
            return m_Canvases[m_Widgets.IndexOf(widget) + m_StartingCanvasIndex];
        }

        public void HandleCopySelectionToCurrentLayer()
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new DuplicateSelectionCommand(SelectionManager.m_Instance.SelectionTransform)
            );
        }

        public void HandleMoveSelectionToCurrentLayer()
        {
            var strokes = SelectionManager.m_Instance.SelectedStrokes.ToList();
            var widgets = SelectionManager.m_Instance.SelectedWidgets.ToList();
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new SelectCommand(strokes, widgets,
                    SelectionManager.m_Instance.SelectionTransform,
                    true,
                    targetCanvas: App.ActiveCanvas
                )
            );
        }

        public void GotoPage(int iIndex)
        {
            m_StartingCanvasIndex = Mathf.Clamp(iIndex, 0, LastPageIndex) * WidgetsPerPage;
            ResetUI();
            m_RefreshNavButtons = true;
        }

        public void AdvancePage(int iAmount)
        {
            GotoPage(CurrentPageIndex + iAmount);
        }
    }
}
