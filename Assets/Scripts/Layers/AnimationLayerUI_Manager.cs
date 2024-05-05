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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using System;

namespace TiltBrush.Layers
{
    public class AnimationLayerUI_Manager : MonoBehaviour
    {
        public delegate void OnActiveSceneChanged(GameObject widget);
        public static event OnActiveSceneChanged onActiveSceneChanged;

        [SerializeField] private LocalizedString m_MainLayerName;
        [SerializeField] private LocalizedString m_AdditionalLayerName;

        [SerializeField] public GameObject modeltrackWidget;

        public GameObject mainWidget;
        public List<GameObject> m_Widgets;
        public int scrollOffset = 0;
        public float scrollHeight = 0.2f; // Height of each element in scroll zone
        private List<CanvasScript> m_Canvases;

        public GameObject layersWidget;

        public GameObject scrollUpButton;
        public GameObject scrollDownButton;

        private void Start()
        {
            ResetUI();
            initScroll();
            if (hasAnimationComponent())
            {
                App.Scene.animationUI_manager.StartTimeline();
            }
        }

        private bool hasAnimationComponent()
        {
            return this.gameObject.GetComponent<TiltBrush.FrameAnimation.AnimationUI_Manager>() != null;
        }
        private bool isAnimationChanging()
        {
            return App.Scene.animationUI_manager != null && App.Scene.animationUI_manager.GetChanging();
        }

        public void ResetUI()
        {
            if (isAnimationChanging()) return;
            m_Canvases = new List<CanvasScript>();
            var layerCanvases = App.Scene.LayerCanvases.ToArray();

            foreach (GameObject widget in m_Widgets)
            {
                if (widget.GetComponentInChildren<ModelWidget>() != null)
                {
                    Destroy(widget.GetComponentInChildren<ModelWidget>().gameObject);
                }
                Destroy(widget);
            }
            m_Widgets.Clear();

            for (int i = 0; i < layerCanvases.Length; i++)
            {
                var newWidget = Instantiate(layersWidget, this.gameObject.transform, false);
                newWidget.GetComponentInChildren<TMPro.TextMeshPro>().text = layerCanvases[i].name;
                if (i == 0)
                {
                    newWidget.GetComponentInChildren<TMPro.TextMeshPro>().text = $"{m_MainLayerName.GetLocalizedStringAsync().Result}";
                    newWidget.GetComponentInChildren<DeleteLayerButton>()?.gameObject.SetActive(false);
                    newWidget.GetComponentInChildren<LayerPopupButton>()?.gameObject.SetActive(false);
                    newWidget.GetComponentInChildren<SquashLayerButton>()?.gameObject.SetActive(false);
                    newWidget.GetComponentInChildren<RenameLayerButton>()?.gameObject.SetActive(false);
                }

                if (layerCanvases[i] == App.ActiveCanvas)
                {
                    newWidget.GetComponentInChildren<FocusLayerButton>().SetButtonActivation(layerCanvases[i] == App.ActiveCanvas);
                }

                // Active button means hidden layer
                newWidget.GetComponentInChildren<ToggleVisibilityLayerButton>().SetButtonActivation(!layerCanvases[i].isActiveAndEnabled);

                foreach (var btn in newWidget.GetComponentsInChildren<OptionButton>())
                {
                    btn.m_CommandParam = i;
                }

                Vector3 localPos = mainWidget.transform.localPosition;
                localPos.y -= i * scrollHeight;
                localPos.y -= scrollOffset;
                newWidget.transform.localPosition = localPos;
                m_Widgets.Add(newWidget);
                m_Canvases.Add(layerCanvases[i]);
            }
            UpdateScroll();
        }

        private void initScroll()
        {
            scrollOffset = 0;
            scrollHeight = 0.2f;
        }

        private void UpdateScroll()
        {
            for (int i = 0; i < m_Widgets.Count; i++)
            {
                Vector3 localPos = mainWidget.transform.localPosition;
                float subtractingVal = i * scrollHeight + scrollOffset * scrollHeight;
                localPos.y -= subtractingVal;
                m_Widgets[i].transform.localPosition = localPos;

                int thisWidgetOffset = i + scrollOffset;
                if (thisWidgetOffset >= 7 || thisWidgetOffset < 0)
                {
                    m_Widgets[i].SetActive(false);
                }
                else
                {
                    m_Widgets[i].SetActive(true);
                }
            }

            scrollUpButton.SetActive(scrollOffset != 0);
            scrollDownButton.SetActive(scrollOffset + m_Widgets.Count > 7);

            if (hasAnimationComponent())
            {
                App.Scene.animationUI_manager.UpdateTrackScroll(scrollOffset, scrollHeight);
            }
        }

        public void scrollDirection(bool upDirection)
        {
            if (scrollOffset == 0 && upDirection) return;
            if (scrollOffset + m_Widgets.Count <= 7 && !upDirection) return;
            scrollOffset += (Convert.ToInt32(upDirection) * 2 - 1);
            UpdateScroll();
        }

        private void OnLayerCanvasesUpdate()
        {
            ResetUI();
        }

        // Subscribes to events
        public void OnEnable()
        {
            App.Scene.ActiveCanvasChanged += ActiveSceneChanged;
            App.Scene.LayerCanvasesUpdate += OnLayerCanvasesUpdate;
        }

        // Unsubscribes to events
        public void OnDisable()
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

        public void DeleteLayerGeneral()
        {
            if (App.Scene.ActiveCanvas == App.Scene.MainCanvas) return; // Don't delete the main canvas
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new DeleteLayerCommand(App.Scene.ActiveCanvas));
            App.Scene.animationUI_manager.ResetTimeline();
        }

        public void SquashLayer(int index)
        {
            var canvas = m_Canvases[index];
            var prevCanvas = m_Canvases[Mathf.Max(index - 1, 0)];
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new SquashLayerCommand(canvas, prevCanvas)
            );
        }

        public void SquashLayerGeneral()
        {
            var canvas = App.Scene.ActiveCanvas;
            var index = App.Scene.GetLayerNumFromCanvas(App.Scene.ActiveCanvas);
            var prevCanvas = App.Scene.GetCanvasFromLayerNum(Mathf.Max(index - 1, 0));
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new SquashLayerCommand(canvas, prevCanvas)
            );
        }

        public void ClearLayerContents(int index)
        {
            var canvas = m_Canvases[index];
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ClearLayerCommand(canvas));
        }

        public void ClearLayerContentsGeneral()
        {
            CanvasScript canvas = App.Scene.ActiveCanvas;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ClearLayerCommand(canvas));
        }

        public void AddLayer()
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new AddLayerCommand(true));
            App.Scene.animationUI_manager.ResetTimeline();
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
            ResetUI();
        }

        private void ActiveSceneChanged(CanvasScript prev, CanvasScript current)
        {
            onActiveSceneChanged?.Invoke(GetWidgetFromCanvas(current));
        }

        private CanvasScript GetCanvasFromWidget(GameObject widget)
        {
            return m_Canvases[m_Widgets.IndexOf(widget)];
        }

        private GameObject GetWidgetFromCanvas(CanvasScript canvas)
        {
            var index = m_Canvases.IndexOf(canvas);
            return index >= 0 ? m_Widgets[index] : null;
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
    }
}
