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

using UnityEngine;
using UnityEditor;

namespace TiltBrush
{
    public static class InstantiateSaveCamera
    {
        [MenuItem("Open Brush/Debug/Instantiate Save Camera")]
        public static void InstantiateSaveCameraInScene()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("'Instantiate Save Camera' only works when Unity is in 'Play' mode.");
                return;
            }

            if (SaveLoadScript.m_Instance == null)
            {
                Debug.LogError("SaveLoadScript.m_Instance is null. Cannot get camera state.");
                return;
            }

            // ReasonableThumbnail_SS returns the saved camera transform in Scene Space
            // This is the camera position from the loaded sketch file
            TrTransform cameraTr_Scene = SaveLoadScript.m_Instance.ReasonableThumbnail_SS;

            // Create a new GameObject with a Camera component
            GameObject cameraObj = new GameObject("SaveCamera_Preview");
            Camera cam = cameraObj.AddComponent<Camera>();

            // Set camera properties to match typical GLTF export
            cam.fieldOfView = 60.0f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000.0f;

            // Convert from Scene space to World space using App.Scene.AsScene
            // This properly accounts for the scene's transform
            App.Scene.AsScene[cameraObj.transform] = cameraTr_Scene;

            // Add a visual indicator so it's easy to see in the scene
            // Create a simple mesh to show camera frustum
            GameObject frustumHelper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frustumHelper.name = "CameraIndicator";
            frustumHelper.transform.SetParent(cameraObj.transform);
            frustumHelper.transform.localPosition = new Vector3(0, 0, 0.5f);
            frustumHelper.transform.localScale = new Vector3(0.1f, 0.1f, 1.0f);

            // Make it semi-transparent and colorful
            var renderer = frustumHelper.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(1f, 0.5f, 0f, 0.5f);
            material.SetFloat("_Mode", 3); // Transparent mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            renderer.material = material;

            // Select the new camera object
            Selection.activeGameObject = cameraObj;

            Debug.Log($"Created SaveCamera in Scene space at position {cameraTr_Scene.translation}, rotation {cameraTr_Scene.rotation.eulerAngles}");
        }

        [MenuItem("Open Brush/Debug/Instantiate Save Camera", true)]
        public static bool ValidateInstantiateSaveCameraInScene()
        {
            return Application.isPlaying;
        }
    }
}
