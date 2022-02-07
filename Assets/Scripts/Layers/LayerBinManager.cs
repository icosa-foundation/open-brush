using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


namespace TiltBrush.Layers
{
    /**
     * To use the Layers feature, 
     * You'll create all new gameobjects (like paint from a brush) as children of the activeLayer 
     * You can get the current active layer with the static property, activeLayer.
     * 
     * Buttons on the UI send events to this manager and the manager (this) performs functions on the layerMap dictionary.
     * Which is made by zipping the LayerUis and LayerBins
     * 
     * At the start, the layerBins (empty GameObjects) automagically created from the LayerUis.
     * then the dictionary is created mapping LayerUis to LayerBins
     **/

    public class LayerBinManager : MonoBehaviour
    {
        public bool debug;

        //! the layer that is actively used to add new objects into
        private KeyValuePair<GameObject, GameObject> _activeLayer;
        public KeyValuePair<GameObject, GameObject> activeLayer
        {
            get
            {       
                return _activeLayer;
            } 
            private set
            {
                _activeLayer = value;

                Debug.Log("The active Layer has been changed");
            }
        }

        public GameObject layerBinPrefab;
        public Transform layerBinParent;

        //! list of UI elements used to interact with the layer bins
        [SerializeField] protected List<GameObject> layerUis = new List<GameObject>();
        //! list of empty objects used to place children objects into
        [SerializeField] readonly public List<GameObject> layerBins = new List<GameObject>();
        //! The zipped up dictionary made by the LayerUi and LayerBin lists
        [SerializeField] public static Dictionary<GameObject, GameObject> layerMap;

        
        private void Start()
        {
            //! subscribe to layer UI events
            FocusLayerButton.onFocusedLayer += SetActiveLayer;
            ToggleVisibilityLayerButton.onVisiblityToggle += ToggleVisibility;
            DeleteLayerButton.onDeleteLayer += DeleteLayerContent;
            // lock event

            InitLayerBins();
        }

        //! Creates the layer bin objects (amount of layerUI = amount of layer Bins), then creates a dict with the keys = layerUis and the values = layerBins
        private void InitLayerBins()
        {
            //! create the layer bins
            for (int i = 0; i < layerUis.Count; i++)
            {
                GameObject bin;
                if (layerBinPrefab)
                {
                    bin = Instantiate(layerBinPrefab);
                }
                else
                {
                    bin = new GameObject();
                }

                bin.name = "Layer " + i.ToString();
                if (layerBinParent) bin.transform.parent = layerBinParent;

                layerBins.Add(bin);
            }

            //! create a new dictionary everytime we init the layer bins
            layerMap = new Dictionary<GameObject, GameObject>();

            //! zip up the two lists into a dictionary
            for (int i = 0; i < layerUis.Count; i++)
            {
                layerMap.Add(layerUis[i], layerBins[i]);

                //! check to see if the dict is zipping up, since unity is unable to easily display this in the inspector
                if (debug)
                {
                    foreach (GameObject key in layerMap.Keys)
                        Debug.Log("Keys" + key.name);
                    foreach (GameObject value in layerMap.Values)
                        Debug.Log("Values " + value.name);
                }
            }

            //! if there isnt a active layer, make the first layer in the dict it
            if (activeLayer.Equals(null))
                activeLayer = layerMap.First();
        }

        //! Gets the corresponding bin by using the layer's UI object as a key, then toggles the bins active state.... muahahahah
        private void ToggleVisibility(GameObject layerUi)
        {
            if (GetBin(layerUi).activeSelf) GetBin(layerUi).SetActive(false);
            else GetBin(layerUi).SetActive(true);
        }

        //! Gets the corresponding bin by using the layer's UI object as a key, then destroys all its children.... muahahahah
        private void DeleteLayerContent(GameObject layerUi)
        {
            //! delete the children of this layer, excluding the parent layer (that's why int i starts at 1)
            Transform[] gameObjects = GetBin(layerUi)?.GetComponentsInChildren<Transform>(true);
            for (int i = 1; i < gameObjects.Length; i++)
                Destroy(gameObjects[i].gameObject);
        }

        //! Not Implemented
        private void ToggleLockLayer(GameObject layerUi)
        {
            // make the layer un editable
        }

        //! The layer's UI representation comes in an sets the KeyValue pair with the corresponding bin object
        private void SetActiveLayer(GameObject layerUi)
        {
            KeyValuePair<GameObject, GameObject> layer = new KeyValuePair<GameObject, GameObject>(layerUi, GetBin(layerUi));
            activeLayer = layer;

            if (debug) Debug.Log("The New active layer is " + activeLayer.Key + " : " + activeLayer.Value);
        }

        //! Gets the bin by searching the keys with the layer's UI
        public GameObject GetBin(GameObject layerUi)
        {
            try
            {
                GameObject bin;
                layerMap.TryGetValue(layerUi, out bin);
                return bin;
            }
            catch (NullReferenceException)
            {
                if (debug) Debug.Log("Could find a key with the layerUi game object");
                return null;
            }
        }
    }
}
