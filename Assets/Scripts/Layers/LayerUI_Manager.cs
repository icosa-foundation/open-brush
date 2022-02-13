using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;


namespace TiltBrush.Layers
{
    public class LayerUI_Manager : MonoBehaviour
    {

        public delegate void OnActiveSceneChanged(GameObject widget);
        public static event OnActiveSceneChanged onActiveSceneChanged;

        [FormerlySerializedAs("layerPrefab")] [SerializeField] private GameObject m_LayerUiPrefab;
        [SerializeField] private bool m_CreateLayersOnStart = true;
        [SerializeField] private int m_InitialLayers = 2;
        [SerializeField] private int m_MaxLayers = 5;
        
        private List<GameObject> m_Widgets;
        private List<CanvasScript> m_Canvases;

        private void Start()
        {
            m_Widgets = new List<GameObject>();
            m_Canvases = new List<CanvasScript>();

            // Pair mainCanvas to layer prefab
            CanvasScript mainCanvas = App.Scene.MainCanvas;
            GameObject mainLayerWidget = Instantiate(m_LayerUiPrefab, transform);
            mainLayerWidget.GetComponentInChildren<DeleteLayerButton>().gameObject.SetActive(false);
            mainLayerWidget.GetComponentInChildren<FocusLayerButton>().ParentIsActiveLayerToggleActivation(mainLayerWidget);
            m_Widgets.Add(mainLayerWidget);
            m_Canvases.Add(mainCanvas);
            var mainLayerName = mainCanvas.name;
            mainLayerWidget.GetComponentInChildren<TMPro.TMP_Text>().text = mainLayerName;
            
            // Create initial layers
            if (m_CreateLayersOnStart)
            {
                for (int i = 0; i < m_InitialLayers - 1; i++) App.Scene.AddLayer();
            }
        }
        
        
        
        private void ResetUI()
        {
            if (m_Widgets != null)
            {
                foreach (var widget in m_Widgets)
                {
                    Destroy(widget);
                }
                
                m_Widgets = new List<GameObject>();
                m_Canvases = new List<CanvasScript>();
                
                var canvases = App.Scene.LayerCanvases.ToArray();
                for (uint i=0; i < canvases.Length; i++)
                {
                    var canvas = canvases[i];
                    GameObject widget = Instantiate(m_LayerUiPrefab, transform);
                    if (i==0) widget.GetComponentInChildren<DeleteLayerButton>().gameObject.SetActive(false);
                    m_Widgets.Add(widget);
                    m_Canvases.Add(canvas);
                }
            }
        }
        
        private void LayerAdded(CanvasScript layer)
        {
            if (GetWidgetFromLayer(layer)==null)
            {
                AddLayerToUI(layer);
            }
        }
        
        private void OnLayerCanvasesUpdate()
        {
            ResetUI();
        }

        // Subscribes to events
        private void OnEnable()
        {
            AddLayerButton.onAddLayer += AddLayer;
            ClearLayerContentsButton.onClearLayerContents += ClearLayerContentsContents;
            DeleteLayerButton.onDeleteLayer += DeleteLayer;
            FocusLayerButton.onFocusedLayer += SetActiveLayer;
            ToggleVisibilityLayerButton.onVisiblityToggle += ToggleVisibility;

            App.Scene.LayerCanvasAdded += LayerAdded;
            App.Scene.ActiveCanvasChanged += ActiveSceneChanged;
            App.Scene.LayerCanvasesUpdate += OnLayerCanvasesUpdate;
        }

        // Unsubscribes to events
        private void OnDisable()
        {
            AddLayerButton.onAddLayer -= AddLayer;
            ClearLayerContentsButton.onClearLayerContents -= ClearLayerContentsContents;
            DeleteLayerButton.onDeleteLayer -= DeleteLayer;
            FocusLayerButton.onFocusedLayer -= SetActiveLayer;
            ToggleVisibilityLayerButton.onVisiblityToggle -= ToggleVisibility;

            App.Scene.LayerCanvasAdded -= LayerAdded;
            App.Scene.ActiveCanvasChanged -= ActiveSceneChanged;
            App.Scene.LayerCanvasesUpdate -= OnLayerCanvasesUpdate;
        }
        
        // Instantiates layer UI prefab, then zips it and the layer CanvasScript
        // together in a dictionary with the Layer Ui as the Key and the Canvas a its value.
        public void AddLayerToUI(CanvasScript newLayer)
        {
            if (m_Widgets.Count >= m_MaxLayers) return;

            GameObject widget = Instantiate(m_LayerUiPrefab, transform);

            m_Widgets.Add(widget);
            m_Canvases.Add(newLayer);

            // set the layer name on the ui
            if (GetCanvasFromWidget(widget))
                widget.GetComponentInChildren<TMPro.TMP_Text>().text = GetCanvasFromWidget(widget).name;
        }

        public void DeleteLayer(GameObject widget)
        {
            if (!GetCanvasFromWidget(widget)) return; // Ensure that the canvas exists
            if (GetCanvasFromWidget(widget) == App.Scene.MainCanvas) return; // Don't delete the main canvas
            var layer = GetCanvasFromWidget(widget);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new DeleteLayerCommand(layer));
            
            // Remove from the dict
            // m_LayerWidgetToCanvasMap.Remove(widget);
            // Destroy(widget);
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

        // Resets the pools of the canvas, clearing all paint within it
        public void ClearLayerContentsContents(GameObject widget)
        {
            if (!GetCanvasFromWidget(widget)) return;

            CanvasScript canvas = GetCanvasFromWidget(widget);
            //canvas.BatchManager.ResetPools();

            // Clear layer command
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new ClearLayerCommand(canvas.BatchManager));
        }
        
        private void AddLayer()
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new AddLayerCommand(true));
        }
        
        // Toggles the visibility of the Canvas
        public void ToggleVisibility(GameObject widget)
        {
            if (!GetCanvasFromWidget(widget)) return;
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
            onActiveSceneChanged?.Invoke(GetWidgetFromLayer(current));
        }

        // Returns the canvas value of a layer UI key
        private CanvasScript GetCanvasFromWidget(GameObject widget)
        {      
            try
            {
                return m_Canvases[m_Widgets.IndexOf(widget)];
            }
            catch (KeyNotFoundException e)
            {
                return null;
            }
        }
        
        private GameObject GetWidgetFromLayer(CanvasScript canvas)
        {
            // TODO: Not sure why we need this here
            // Something odd happening with the dict not being initialised when a sketch is loaded
            if (m_Widgets == null)
            {
                m_Canvases = new List<CanvasScript>();
                m_Widgets = new List<GameObject>();
            }
            var index = m_Canvases.IndexOf(canvas);
            return index >= 0 ? m_Widgets[index]: null;

        }
    }
}
