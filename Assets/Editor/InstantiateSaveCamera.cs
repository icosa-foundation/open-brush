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

            var cameraObj = App.Instance.InstantiateThumbnailCamera();
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
        }

        [MenuItem("Open Brush/Debug/Instantiate Save Camera", true)]
        public static bool ValidateInstantiateSaveCameraInScene()
        {
            return Application.isPlaying;
        }
    }
}
