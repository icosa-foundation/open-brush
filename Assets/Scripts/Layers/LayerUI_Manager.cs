using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace TiltBrush.Layers
{
    public class LayerUI_Manager : MonoBehaviour
    {
        public delegate void OnActiveSceneChanged(GameObject widget);
        public static event OnActiveSceneChanged onActiveSceneChanged;

        public List<GameObject> m_Widgets;
        private List<CanvasScript> m_Canvases;

        private void Start()
        {
            m_Canvases = new List<CanvasScript>();
            ResetUI();
        }
        
        private void ResetUI()
        {
            m_Canvases = new List<CanvasScript>();
            var canvases = App.Scene.LayerCanvases.ToArray();
            for (int i=0; i < m_Widgets.Count; i++)
            {
                var widget = m_Widgets[i];
                if (i >= canvases.Length)
                {
                    widget.SetActive(false);
                    continue;
                }
                widget.SetActive(true);
                var canvas = canvases[i];
                if (i==0) widget.GetComponentInChildren<DeleteLayerButton>()?.gameObject.SetActive(false);
                if (i==0) widget.GetComponentInChildren<SquashLayerButton>()?.gameObject.SetActive(false);
                widget.GetComponentInChildren<FocusLayerButton>().SetButtonActivation(canvas==App.ActiveCanvas);
                widget.GetComponentInChildren<TMPro.TextMeshPro>().text = (i == 0) ? "Main Layer" : $"Layer {i}";
                // Active button means hidden layer
                widget.GetComponentInChildren<ToggleVisibilityLayerButton>().SetButtonActivation(!canvas.isActiveAndEnabled);
                m_Canvases.Add(canvas);
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
        
        public void DeleteLayer(GameObject widget)
        {
            if (GetCanvasFromWidget(widget) == App.Scene.MainCanvas) return; // Don't delete the main canvas
            var layer = GetCanvasFromWidget(widget);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new DeleteLayerCommand(layer));
        }
        
        public void SquashLayer(GameObject widget)
        {
            var canvas = GetCanvasFromWidget(widget);
            var index = m_Widgets.IndexOf(widget);
            var prevCanvas = m_Canvases[Mathf.Max(index - 1, 0)];
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new SquashLayerCommand(canvas, prevCanvas)
            ); 
        }

        public void ClearLayerContents(GameObject widget)
        {
            CanvasScript canvas = GetCanvasFromWidget(widget);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ClearLayerCommand(canvas.BatchManager));
        }
        
        private void AddLayer()
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
            var layer = GetCanvasFromWidget(widget);
            if (!layer) return;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ActivateLayerCommand(layer));
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
            return index >= 0 ? m_Widgets[index]: null;
        }
    }
}
