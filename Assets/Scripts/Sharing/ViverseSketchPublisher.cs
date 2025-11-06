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

using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Exports and publishes OpenBrush sketches to VIVERSE World
    /// </summary>
    public class ViverseSketchPublisher : MonoBehaviour
    {
        private ViversePublishManager m_PublishManager;
        private string m_TempDirectory;

        public event Action<bool, string> OnPublishComplete;
        public event Action<float> OnExportProgress;
        public event Action<float> OnUploadProgress;

        void Start()
        {
            m_PublishManager = FindObjectOfType<ViversePublishManager>();

            if (m_PublishManager != null)
            {
                m_PublishManager.OnPublishComplete += HandlePublishComplete;
                m_PublishManager.OnUploadProgress += HandleUploadProgress;
            }

            m_TempDirectory = Path.Combine(Application.temporaryCachePath, "ViverseExports");
            if (!Directory.Exists(m_TempDirectory))
            {
                Directory.CreateDirectory(m_TempDirectory);
            }
        }

        /// <summary>
        /// Exports current sketch and publishes to VIVERSE World
        /// </summary>
        public void PublishCurrentSketch(string title, string description = null)
        {
            if (m_PublishManager == null)
            {
                Debug.LogError("[ViverseSketch] ViversePublishManager not found!");
                OnPublishComplete?.Invoke(false, "ViversePublishManager not found");
                return;
            }

            if (!m_PublishManager.IsAuthenticated())
            {
                Debug.LogError("[ViverseSketch] Not authenticated!");
                OnPublishComplete?.Invoke(false, "Please login first");
                return;
            }

            if (SketchMemoryScript.m_Instance.StrokeCount == 0)
            {
                Debug.LogWarning("[ViverseSketch] No strokes to export!");
                OnPublishComplete?.Invoke(false, "No strokes to export. Draw something first!");
                return;
            }

            StartCoroutine(ExportAndPublishCoroutine(title, description));
        }

        private IEnumerator ExportAndPublishCoroutine(string title, string description)
        {
            Debug.Log($"[ViverseSketch] Starting export: {title}");
            Debug.Log($"[ViverseSketch] Stroke count: {SketchMemoryScript.m_Instance.StrokeCount}");
            OnExportProgress?.Invoke(0.1f);

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string fileName = $"sketch_{timestamp}.glb";
            string glbPath = Path.Combine(m_TempDirectory, fileName);
            string zipPath = Path.Combine(m_TempDirectory, $"sketch_{timestamp}.zip");

            Debug.Log($"[ViverseSketch] Export GLB path: {glbPath}");
            Debug.Log($"[ViverseSketch] Export ZIP path: {zipPath}");
            OnExportProgress?.Invoke(0.3f);

            bool exportSuccess = false;
            string exportError = null;

            try
            {
                exportSuccess = ExportSketchAsGLB(glbPath, out exportError);
                OnExportProgress?.Invoke(0.6f);
            }
            catch (Exception ex)
            {
                exportError = ex.Message;
                Debug.LogError($"[ViverseSketch] Export failed: {ex}");
            }

            if (!exportSuccess)
            {
                OnPublishComplete?.Invoke(false, $"Export failed: {exportError}");
                yield break;
            }

            if (!File.Exists(glbPath))
            {
                OnPublishComplete?.Invoke(false, "GLB file not created");
                yield break;
            }

            FileInfo glbInfo = new FileInfo(glbPath);
            Debug.Log($"[ViverseSketch] GLB created: {glbInfo.Length / 1024}KB");

            try
            {
                Debug.Log("[ViverseSketch] Creating ZIP with HTML viewer...");

                string htmlContent = GenerateViewerHTML();
                string htmlPath = Path.Combine(m_TempDirectory, $"index_{timestamp}.html");
                File.WriteAllText(htmlPath, htmlContent);

                using (var zip = new ZipArchive(
                           File.Create(zipPath),
                           ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(htmlPath, "index.html");
                    zip.CreateEntryFromFile(glbPath, "scene.glb");
                }
                OnExportProgress?.Invoke(0.8f);

                File.Delete(glbPath);
                File.Delete(htmlPath);

                FileInfo zipInfo = new FileInfo(zipPath);
                Debug.Log($"[ViverseSketch] ZIP created: {zipInfo.Length / 1024}KB");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViverseSketch] ZIP creation failed: {ex}");
                OnPublishComplete?.Invoke(false, $"ZIP creation failed: {ex.Message}");
                yield break;
            }

            OnExportProgress?.Invoke(1.0f);
            Debug.Log($"[ViverseSketch] Starting upload...");

            if (string.IsNullOrEmpty(description))
            {
                description = $"OpenBrush sketch - {SketchMemoryScript.m_Instance.StrokeCount} strokes";
            }

            m_PublishManager.PublishWorld(title, description, zipPath);

            yield return null;
        }

        private bool ExportSketchAsGLB(string glbPath, out string error)
        {
            error = null;

            try
            {
                Debug.Log("[ViverseSketch] Starting GLB export...");

                ExportGlTF exporter = new ExportGlTF();

                var result = exporter.ExportBrushStrokes(
                    glbPath,
                    AxisConvention.kGltf2,
                    binary: true,
                    doExtras: false,
                    includeLocalMediaContent: true,
                    gltfVersion: 2,
                    selfContained: true
                );

                if (!result.success)
                {
                    error = "Export failed (see console for details)";
                    return false;
                }

                Debug.Log($"[ViverseSketch] GLB export successful! {result.numTris} triangles");
                if (result.exportedFiles != null && result.exportedFiles.Length > 0)
                {
                    Debug.Log($"[ViverseSketch] Exported files: {string.Join(", ", result.exportedFiles)}");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogError($"[ViverseSketch] Export failed: {ex}");
                return false;
            }
        }

        private void HandlePublishComplete(bool success, string message)
        {
            if (success)
            {
                Debug.Log($"[ViverseSketch] Publish successful: {message}");
            }
            else
            {
                Debug.LogError($"[ViverseSketch] Publish failed: {message}");
            }

            OnPublishComplete?.Invoke(success, message);
        }

        private void HandleUploadProgress(float progress)
        {
            OnUploadProgress?.Invoke(progress);
        }

        private string GenerateViewerHTML()
        {
            string viewerContent = @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>icosa viewer v251106 x joserpagil@gmail.com</title>
</head>
<style>
    @font-face {
        font-family: 'MundialRegular';
        src: url('assets/MundialRegular.otf') format('opentype');
        font-weight: normal;
        font-style: normal;
    }
    * { font-family: 'MundialRegular', sans-serif; }
    input { background-color: rgba(0,0,0,0.6); color: white; }
    button { background-color: rgba(0,0,0,0.6); border: none; color: white; }
    .rounded-input { border: 2px solid #ccc; border-radius: 25px; outline: none; transition: border-color 0.3s ease; }
    #virtual_reality_button { border: none; background: transparent; }
    .unselectable { user-select: none; -webkit-user-select: none; -moz-user-select: none; -ms-user-select: none; }
</style>
<body style='touch-action: none; margin: 0; position: relative; width: 100dvw; height: 100dvh; overflow: hidden;'>
<canvas id='nipple_canvas' class='unselectable' style='position: absolute; bottom: 0; left: 0;'></canvas>
<div id='chat_div' style='position: absolute; bottom: 0; background: transparent; color: white;'>
    <div id='msgs_div' style='overflow: hidden; color: gray;'></div>
    <input id='chat_input' type='text' class='rounded-input' style='width: 100%;' placeholder='type message here'/>
</div>
<img id='orientation_img' class='unselectable' style='position: absolute; bottom: 0; background: transparent;' src='assets/orientation_controls.svg' hidden/>
<img id='pc_img' class='unselectable' style='position: absolute; bottom: 0; background: transparent; right: 0;' src='assets/PC_controls.svg' hidden/>
<button id='virtual_reality_button' class='unselectable' style='position: absolute; bottom: 0; background: transparent;' hidden>
    <img id='VR_headset_icon' src='assets/VR_headset_icon.svg'/>
</button>
<script type='importmap'>
{
    'imports': {
        'three': 'https://unpkg.com/three@0.161.0/build/three.module',
        'three/addons/': 'https://unpkg.com/three@0.161.0/examples/jsm/'
    }
}
</script>
<script src='https://cdn.jsdelivr.net/npm/@viveport/sdk@latest/dist/index.umd.cjs'></script>
<script type='module'>
addEventListener('error', event => { if(msgs_div) msgs_div.innerHTML += event.lineno + ' = ' + event.message + '<br>' });
const app = 'syz2h3yf9u';
import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { VRButton } from 'three/addons/webxr/VRButton.js';
import { XRControllerModelFactory } from 'three/addons/webxr/XRControllerModelFactory.js';
import { GLTFGoogleTiltBrushMaterialExtension } from 'https://cdn.jsdelivr.net/npm/three-icosa@0.0.7/dist/three-icosa.module.min.js';
let canvas, renderer, vr_button, scene, camera_group, camera, clock, keys = {}, beep;
let pointer_data = { button: -1, down: { x: 0, y: 0 }, position: { x: 0, y: 0 } };
let nipple_context, touch = false, vr = false, speed = 0.05, angular_speed = Math.PI / 500, delta_threshold = 20;
const create_world = _ => {
    const move = _ => {
        const dir = new THREE.Vector3();
        camera.getWorldDirection(dir);
        const up = new THREE.Vector3(0, 1, 0).applyQuaternion(camera.quaternion).normalize();
        if(keys['w']) camera_group.position.addScaledVector(dir, speed);
        if(keys['s']) camera_group.position.addScaledVector(dir, -speed);
        if(keys['a']) camera_group.rotation.y += angular_speed;
        if(keys['d']) camera_group.rotation.y += -angular_speed;
        if(keys['q']) camera_group.position.addScaledVector(up, speed * 0.5);
        if(keys['e']) camera_group.position.addScaledVector(up, -speed * 0.5);
        if(keys['r']) camera_group.rotation.x += angular_speed;
        if(keys['f']) camera_group.rotation.x += -angular_speed;
    };
    canvas = document.createElement('canvas');
    renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
    renderer.xr.enabled = true;
    document.body.appendChild(renderer.domElement);
    vr_button = VRButton.createButton(renderer);
    const show_vr_interval = setInterval(_ => {
        if(vr_button.textContent == 'ENTER VR') { clearInterval(show_vr_interval); vr = true; resize(); }
    }, 1000);
    if(window.matchMedia('(pointer: coarse)').matches) touch = true;
    nipple_context = nipple_canvas.getContext('2d');
    scene = new THREE.Scene();
    scene.environment = new THREE.PMREMGenerator(renderer).fromScene(new RoomEnvironment(), 0.04).texture;
    camera = new THREE.PerspectiveCamera(50, 1, 0.01, 10000);
    camera_group = new THREE.Group();
    camera_group.position.set(0, 5, 15);
    scene.add(camera_group);
    camera_group.add(camera);
    beep = new Audio();
    clock = new THREE.Clock();
    new THREE.TextureLoader().load('https://raw.githubusercontent.com/mrdoob/three.js/dev/examples/textures/2294472375_24a3b8ef46_o.jpg', texture => {
        const material = new THREE.MeshBasicMaterial({ map: texture, side: THREE.BackSide, color: 'white' });
        material.fog = false; material.toneMapped = false;
        const geometry = new THREE.SphereGeometry(5000, 64, 64);
        const sky_sphere = new THREE.Mesh(geometry, material);
        sky_sphere.name = 'environmentSky';
        scene.add(sky_sphere);
    });
    const gltf_loader = new GLTFLoader();
    gltf_loader.register(parser => new GLTFGoogleTiltBrushMaterialExtension(parser, 'https://cdn.jsdelivr.net/npm/three-icosa@0.0.7/dist/brushes/'));
    gltf_loader.load('./scene.glb', gltf => { const brush = gltf.scene; brush.rotation.y = Math.PI; scene.add(brush); });
    const resize = () => { camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix(); renderer.setSize(innerWidth, innerHeight); };
    resize();
    addEventListener('resize', resize);
    renderer.setAnimationLoop(args => { move(); renderer.render(scene, camera); });
    addEventListener('pointerdown', event => { pointer_data.button = event.button; pointer_data.down = { x: event.clientX, y: event.clientY }; });
    addEventListener('pointermove', event => {
        if(pointer_data.button == -1) return;
        pointer_data.position = { x: event.clientX, y: event.clientY };
        const delta = { x: pointer_data.position.x - pointer_data.down.x, y: pointer_data.position.y - pointer_data.down.y };
        if(touch) {
            if(pointer_data.down.x < innerWidth / 2) {
                keys.a = delta.x < -delta_threshold; keys.d = delta.x > delta_threshold;
                keys.w = delta.y < -delta_threshold; keys.s = delta.y > delta_threshold;
            } else { keys.r = delta.y > delta_threshold; keys.f = delta.y < -delta_threshold; }
        } else {
            keys.a = delta.x < -delta_threshold; keys.d = delta.x > delta_threshold;
            keys.w = delta.y < -delta_threshold; keys.s = delta.y > delta_threshold;
        }
    });
    addEventListener('pointerup', event => {
        pointer_data.button = -1;
        keys.w = false; keys.a = false; keys.s = false; keys.d = false; keys.r = false; keys.f = false;
    });
    addEventListener('keydown', event => {
        if('INPUT' == event.target.tagName) { if('Enter' == event.key) chat_input.value = ''; }
        else keys[event.key.toLowerCase()] = true;
    });
    addEventListener('keyup', event => { if('INPUT' != event.target.tagName) keys[event.key.toLowerCase()] = false; });
};
create_world();
</script>
</body>
</html>";

            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>OpenBrush Sketch</title>
    <style>
        * { margin: 0; padding: 0; }
        body { width: 100vw; height: 100vh; overflow: hidden; }
        iframe { width: 100%; height: 100%; border: none; }
    </style>
</head>
<body>
    <iframe srcdoc=""" + viewerContent.Replace("\"", "&quot;") + @"""></iframe>
</body>
</html>";
        }

        /// <summary>
        /// Removes export files older than 1 hour
        /// </summary>
        public void CleanupOldExports()
        {
            if (!Directory.Exists(m_TempDirectory)) return;

            try
            {
                var files = Directory.GetFiles(m_TempDirectory, "sketch_*.*");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if ((DateTime.Now - fileInfo.CreationTime).TotalHours > 1)
                    {
                        File.Delete(file);
                        Debug.Log($"[ViverseSketch] Cleaned up: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ViverseSketch] Cleanup failed: {ex.Message}");
            }
        }

        void OnDestroy()
        {
            if (m_PublishManager != null)
            {
                m_PublishManager.OnPublishComplete -= HandlePublishComplete;
                m_PublishManager.OnUploadProgress -= HandleUploadProgress;
            }
        }
    }
} // namespace TiltBrush
