using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush.Layers
{
    public class LayerUI_Manager : MonoBehaviour
    {
        public bool debug = false;

        public delegate void OnActiveSceneChanged(GameObject layer);
        public static event OnActiveSceneChanged onActiveSceneChanged;

        [SerializeField] private SceneScript sceneScript;
        [SerializeField] private GameObject layerPrefab;

        private List<GameObject> layerObjects = new List<GameObject>();
        private Dictionary<GameObject, CanvasScript> layerMap = new Dictionary<GameObject, CanvasScript>();

        [SerializeField] private bool createLayersOnStart = true;
        [SerializeField] private int howManyLayers = 4;
        [SerializeField] private int maxLayers = 5;


        private void Start()
        {
            sceneScript = FindObjectOfType<SceneScript>();

            // create main canvas layer ui
            if (sceneScript)
            {
                // pair maincanvas to layer prefab
                CanvasScript mainCanvas = sceneScript.MainCanvas;
                GameObject mainLayer = Instantiate(layerPrefab, this.transform);

                if (debug) Debug.Log("Creating Layer " + mainLayer.name);

                // put it into the dict
                layerMap.Add(mainLayer, mainCanvas);
                layerObjects.Add(mainLayer);

                // set the name in the ui
                mainLayer.GetComponentInChildren<TMPro.TMP_Text>().text = GetLayerCanvas(mainLayer).name;
            }

            // create layer on start
            if (createLayersOnStart)
                for (int i = 0; i < howManyLayers; i++)
                    CreateLayer();            
        }

        //! Subscribes to events
        private void OnEnable()
        {
            sceneScript = FindObjectOfType<SceneScript>();

            AddLayerButton.onAddLayer += CreateLayer;
            ClearLayerButton.onClearLayer += ClearLayer;
            DeleteLayerButton.onDeleteLayer += RemoveLayer;
            FocusLayerButton.onFocusedLayer += SetActiveLayer;
            ToggleVisibilityLayerButton.onVisiblityToggle += ToggleVisibility;

            App.Scene.ActiveCanvasChanged += ActiveSceneChanged;
        }
        //! UnSubsricbes to events
        private void OnDisable()
        {
            AddLayerButton.onAddLayer -= CreateLayer;
            ClearLayerButton.onClearLayer -= ClearLayer;
            DeleteLayerButton.onDeleteLayer -= RemoveLayer;
            FocusLayerButton.onFocusedLayer -= SetActiveLayer;
            ToggleVisibilityLayerButton.onVisiblityToggle -= ToggleVisibility;

            App.Scene.ActiveCanvasChanged -= ActiveSceneChanged;
        }


        //! Create a Canvas from the SceneScript reference, and instantiates layer UI prefab, then zips them together in a dictionary with the Layer Ui as the Key and the Canvas a its value.
        public void CreateLayer()
        {
            if (layerMap.Count >= maxLayers) return;

            CanvasScript canvas = sceneScript.AddLayer();
            GameObject layer = Instantiate(layerPrefab, this.transform);

            if (debug) Debug.Log("Creating Layer " + layer.name);

            // put it into the dict
            layerMap.Add(layer, canvas);
            layerObjects.Add(layer);

            // set the layer name on the ui
            if(GetLayerCanvas(layer))
                layer.GetComponentInChildren<TMPro.TMP_Text>().text = GetLayerCanvas(layer).name;

            // create layer command
        }

        //! Deletes the canvas from the SceneScript reference. Removes the KyeValuePair from the Dictionary and destroys the layer Ui object
        public void RemoveLayer(GameObject layer)
        {
            if (!GetLayerCanvas(layer)) return; // ensure that the canvas exists
            if (GetLayerCanvas(layer) == sceneScript.MainCanvas) return; // Dont delete the main canvas

            if (debug) Debug.Log("Removed Layer " + layer.name);

            sceneScript.DeleteLayer(GetLayerCanvas(layer));

            // remove from the dict
            layerMap.Remove(layer);
            layerObjects.Remove(layer);

            Destroy(layer);

            // Remove Layer Command
        }

        //! Resets the pools of the canvas, clearing all paint within it
        public void ClearLayer(GameObject layer)
        {
            if (!GetLayerCanvas(layer)) return;

            CanvasScript canvas = GetLayerCanvas(layer);
            //canvas.BatchManager.ResetPools();

            // clear layer command
            new ClearLayerCommand(canvas.BatchManager);
        }

        //! Toggles the visibility of the Canvas
        public void ToggleVisibility(GameObject layer)
        {
            if (!GetLayerCanvas(layer)) return;

            if (debug) Debug.Log("Toggled Layer Visibility of " + layer.name);

            CanvasScript canvasScript = GetLayerCanvas(layer);
            if (canvasScript.gameObject.activeSelf) canvasScript.gameObject.SetActive(false);
            else canvasScript.gameObject.SetActive(true);
        }

        //! Changes the active layer on the SceneScript reference
        public void SetActiveLayer(GameObject layer)
        {
            if (!GetLayerCanvas(layer)) return;

            sceneScript.ActiveCanvas = GetLayerCanvas(layer);

            if (debug) Debug.Log("Set Active Layer to " + sceneScript.ActiveCanvas);

            // set active layer command
        }

        //! Utitlity to print out the dictionary to view its contents
        public void PrintDictionary()
        {
            foreach (var layer in layerMap)
            {
                if (debug) Debug.Log("Key: " + layer.Key + "Value: " + layer.Value);
                if (debug) Debug.Log("Key's HashCode: " + layer.Key.GetHashCode());
            }
        }

        //! Looks through the values of the layerMap dictionary to find its key 
        private void ActiveSceneChanged(CanvasScript prev, CanvasScript current)
        {
            // unOptimized code.... searched trhough the dictionary to find a value and return a key, invoke a message with that key as its parameter
            foreach (var layer in layerMap)
                if (layer.Value == current)
                    onActiveSceneChanged?.Invoke(layer.Key);
        }

        // Utils
        //! Returns the canvas value of a layer Ui key
        [SerializeField] 
        private CanvasScript GetLayerCanvas(GameObject layer)
        {      
            try
            {
                if (debug) Debug.Log("Canvas Value: " + layerMap[layer]);

                return layerMap[layer];
            }
            catch (KeyNotFoundException e)
            {
                if (debug) Debug.LogException(e);
                return null;
            }
            
        }
    }
}
