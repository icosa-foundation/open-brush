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


namespace TiltBrush.Layers
{
    public class LayerUI_Manager : MonoBehaviour
    {
        public delegate void OnActiveSceneChanged(GameObject widget);
        public static event OnActiveSceneChanged onActiveSceneChanged;

        [SerializeField] private LocalizedString m_MainLayerName;
        [SerializeField] private LocalizedString m_AdditionalLayerName;

        public GameObject mainWidget;
        public List<GameObject> m_Widgets;

        private List<CanvasScript> m_Canvases;

        public Component animationUI_Manager;
        public GameObject layersWidget;

        private void Start()
        {
            ResetUI();
            App.Scene.animationUI_manager.startTimeline();
        }

        private void ResetUI()
        {


            m_Canvases = new List<CanvasScript>();
            var layerCanvases = App.Scene.LayerCanvases.ToArray();


            foreach (GameObject widget in m_Widgets){

                Destroy(widget);
            }
            m_Widgets.Clear();

        

            for (int i = 0; i < layerCanvases.Length; i++)
            {
            var newWidget = Instantiate(layersWidget,this.gameObject.transform,false);
            // newWidget.transform.SetParent(this.gameObject.transform, false);
        


            if (i == 0) newWidget.GetComponentInChildren<DeleteLayerButton>()?.gameObject.SetActive(false);
            if (i == 0) newWidget.GetComponentInChildren<SquashLayerButton>()?.gameObject.SetActive(false);
            newWidget.GetComponentInChildren<FocusLayerButton>().SetButtonActivation(layerCanvases[i] == App.ActiveCanvas);
            newWidget.GetComponentInChildren<TMPro.TextMeshPro>().text = (i == 0) ? $"{m_MainLayerName.GetLocalizedString()}" : $"{m_AdditionalLayerName.GetLocalizedString()} {i}";
                // Active button means hidden layer
            newWidget.GetComponentInChildren<ToggleVisibilityLayerButton>().SetButtonActivation(!layerCanvases[i].isActiveAndEnabled);
             
            Vector3 localPos = mainWidget.transform.localPosition;
            localPos.y -= i*0.2f;


            newWidget.transform.localPosition = localPos;
            m_Widgets.Add(newWidget);

            m_Canvases.Add(layerCanvases[i]);
            }

            // print("START RESET UI");
            // m_Canvases = new List<CanvasScript>();
            // var canvases = App.Scene.LayerCanvases.ToArray();
            // for (int i = 0; i < m_Widgets.Count; i++)
            // {
            //     print("FOR HERE " + i + " " + canvases.Length);
            //     var widget = m_Widgets[i];

             
            //     if (i >= canvases.Length)
            //     {
                    
            //         widget.SetActive(false);
            //         continue;
            //     }
            //     // widget.SetActive(true);
            //      widget.SetActive(false);
            //     var canvas = canvases[i];
            //     if (i == 0) widget.GetComponentInChildren<DeleteLayerButton>()?.gameObject.SetActive(false);
            //     if (i == 0) widget.GetComponentInChildren<SquashLayerButton>()?.gameObject.SetActive(false);
            //     widget.GetComponentInChildren<FocusLayerButton>().SetButtonActivation(canvas == App.ActiveCanvas);
            //     print("BEFORE PRE STRING");
            //     print(" PRE STRINGS 1" + m_MainLayerName.GetLocalizedString() );
            //      print(" PRE STRINGS 2" +  m_AdditionalLayerName.GetLocalizedString());
            //     widget.GetComponentInChildren<TMPro.TextMeshPro>().text = (i == 0) ? $"{m_MainLayerName.GetLocalizedString()}" : $"{m_AdditionalLayerName.GetLocalizedString()} {i}";
            //     // Active button means hidden layer
            //     widget.GetComponentInChildren<ToggleVisibilityLayerButton>().SetButtonActivation(!canvas.isActiveAndEnabled);
            //     m_Canvases.Add(canvas);
            // }

            // print("RESET UI DONE");
            // print(m_Canvases.Count);
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

        public void DeleteLayer(GameObject widget)
        {
            if (GetCanvasFromWidget(widget) == App.Scene.MainCanvas) return; // Don't delete the main canvas
            var layer = GetCanvasFromWidget(widget);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new DeleteLayerCommand(layer));
        }
        public void DeleteLayerGeneral(){
            if ( App.Scene.ActiveCanvas == App.Scene.MainCanvas) return; // Don't delete the main canvas
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new DeleteLayerCommand(App.Scene.ActiveCanvas));
            App.Scene.animationUI_manager.resetTimeline();
        }

        public void SquashLayer(GameObject widget)
        {
            var canvas = GetCanvasFromWidget(widget);
            var index = m_Widgets.IndexOf(widget);

            print("SQUASHING ORIG" + index);

            var prevCanvas = m_Canvases[Mathf.Max(index - 1, 0)];
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new SquashLayerCommand(canvas, prevCanvas)
            );
        }

          public void SquashLayerGeneral()
        {

            var canvas = App.Scene.ActiveCanvas;
            var index = App.Scene.GetLayerNumFromCanvas(App.Scene.ActiveCanvas);
            print("SQUASHING GENERAL" + index);
            var prevCanvas = App.Scene.GetCanvasFromLayerNum(Mathf.Max(index -1, 0));
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new SquashLayerCommand(canvas, prevCanvas)
            );
        }

        public void ClearLayerContents(GameObject widget)
        {
            CanvasScript canvas = GetCanvasFromWidget(widget);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ClearLayerCommand(canvas));
        }

        public void ClearLayerContentsGeneral()
        {
            CanvasScript canvas = App.Scene.ActiveCanvas;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ClearLayerCommand(canvas));
        }

        public void AddLayer()
        {
            print("ADD 1~ LAYER NOW ");
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new AddLayerCommand(true));
            print("ADD 2~  ");
            App.Scene.animationUI_manager.resetTimeline();
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
    }
}
