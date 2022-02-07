using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.Layers
{
    public class LayerUIManager : MonoBehaviour
    {
        public GameObject layerPrefab;
        [SerializeField] private GameObject activeLayer;

        public int maxLayers = 5;
        [SerializeField] private List<GameObject> layerPool = new List<GameObject>();

        [SerializeField] private List<GameObject> layers = new List<GameObject>();

        // Positioning
        public Transform origin;
        public float ySpacing;


        private void Start()
        {
            // subscribe to events
            ToggleVisibilityLayerButton.onVisiblityToggle += ToggleVisibility;
            FocusLayerButton.onFocusedLayer += SetActiveLayer;
            DeleteLayerButton.onDeleteLayer += DeleteLayer;


            InitLayerPool();


            // create a layer as default
            if (layers.Count == 0)
            {
                GameObject newLayer = GetLayerFromLayerPool();
                layers.Add(newLayer);
                activeLayer = newLayer;
                UpdateLayersList();
            }
        }

        private void OnDestroy()
        {
            // unsubscribe to events
            ToggleVisibilityLayerButton.onVisiblityToggle -= ToggleVisibility;
            FocusLayerButton.onFocusedLayer -= SetActiveLayer;
            DeleteLayerButton.onDeleteLayer -= DeleteLayer;
        }


        // Object Pooling (Layers)
        private void InitLayerPool()
        {
            for (int i = 0; i < maxLayers; i++)
            {
                GameObject layer = Instantiate(layerPrefab);
                layer.transform.parent = this.transform;
                layer.SetActive(false);
                layerPool.Add(layer);
            }
        }

        public GameObject GetLayerFromLayerPool()
        {
            if (layerPool.Count > 0)
            {
                GameObject layer = layerPool[0];
                layer.SetActive(true);
                layerPool.Remove(layer);
                return layer;
            }
            return null;
        }

        public void PutLayerIntoLayerPool(GameObject layer)
        {
            layerPool.Add(layer);
            layer.SetActive(false);
        }

        
        // List of Layers
        public void UpdateLayersList()
        {
            Vector3 currentPosition = origin.position;

            foreach (GameObject layer in layers)
            {
                // position layers with spacing
                layer.transform.parent = this.transform;
                layer.transform.localScale = new Vector3(1,1,1);
                layer.transform.position = currentPosition;
                layer.transform.eulerAngles = transform.eulerAngles;
                currentPosition.y -= ySpacing;
            }
        }

        public void AddLayer()
        {
            GameObject layer = GetLayerFromLayerPool();
            if (layer) layers.Add(layer);
            UpdateLayersList();
        }

        public void DeleteLayer(GameObject layer)
        {
            if (layers.Count <= 1) return;

            layers.Remove(layer);
            PutLayerIntoLayerPool(layer);
            UpdateLayersList();
        }

        public void SetActiveLayer(GameObject layer)
        {
            activeLayer = layer;
        }

        public void ToggleVisibility(GameObject layer)
        {
            if (layer.activeSelf) layer.SetActive(false);
            else layer.SetActive(true);
        }

        public void ToggleLock(GameObject layer)
        {
            // unimpemented
        }
    }
}
