// Copyright 2024 The Open Brush Authors
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
using System.Collections.Generic;
using System.IO;
using GLTF.Schema;
using TiltBrush;
using UnityEngine;

namespace UnityGLTF.Plugins
{
    /// <summary>
    /// Imports KHR_audio_emitter nodes from GLTF as Open Brush SoundClipWidgets.
    /// Audio data embedded in the GLB buffer is extracted to the sound clip library.
    /// </summary>
    public class OpenBrushAudioImport : GLTFImportPlugin
    {
        public override string DisplayName => "Open Brush Audio Import";
        public override string Description => "Creates SoundClipWidgets from KHR_audio_emitter nodes.";

        public override GLTFImportPluginContext CreateInstance(GLTFImportContext context)
        {
            return new OpenBrushAudioImportContext(context);
        }
    }

    public class OpenBrushAudioImportContext : GLTFImportPluginContext
    {
        private readonly GLTFImportContext _context;
        private KHR_audio_emitter _audioExtension;

        private struct PendingAudioNode
        {
            public GameObject NodeObject;
            public KHR_AudioEmitter Emitter;
        }

        private readonly List<PendingAudioNode> _pendingNodes = new();

        // audio array index → absolute file path in the sound library
        private readonly Dictionary<int, string> _audioFilePaths = new();

        public OpenBrushAudioImportContext(GLTFImportContext context)
        {
            _context = context;
        }

        public override void OnAfterImportRoot(GLTFRoot gltfRoot)
        {
            if (gltfRoot.Extensions == null) return;
            if (gltfRoot.Extensions.TryGetValue(KHR_audio_emitter.ExtensionName, out var ext))
                _audioExtension = ext as KHR_audio_emitter;
        }

        public override void OnAfterImportNode(Node node, int nodeIndex, GameObject nodeObject)
        {
            if (_audioExtension == null) return;
            if (node.Extensions == null) return;
            if (!node.Extensions.TryGetValue(KHR_NodeAudioEmitterRef.ExtensionName, out var ext)) return;

            if (ext is KHR_NodeAudioEmitterRef nodeRef && nodeRef.emitter != null)
            {
                _pendingNodes.Add(new PendingAudioNode
                {
                    NodeObject = nodeObject,
                    Emitter = nodeRef.emitter.Value,
                });
            }
        }

        public override void OnAfterImportScene(GLTFScene scene, int sceneIndex, GameObject sceneObject)
        {
            if (_audioExtension == null || _pendingNodes.Count == 0) return;

            ExtractAudioFiles();
            CreateSoundClipWidgets();
        }

        private void ExtractAudioFiles()
        {
            if (_audioExtension.audio == null) return;

            string importDir = Path.Combine(App.SoundClipLibraryPath(), "GltfImport");
            Directory.CreateDirectory(importDir);

            for (int i = 0; i < _audioExtension.audio.Count; i++)
            {
                var audio = _audioExtension.audio[i];

                if (audio.bufferView != null)
                {
                    var buffer = _context.SceneImporter.GetBufferViewData(audio.bufferView.Value);
                    if (!buffer.IsCreated) continue;

                    string ext = MimeTypeToExtension(audio.mimeType);
                    if (ext == null)
                    {
                        Debug.LogWarning($"[OBAudio] Unsupported mime type '{audio.mimeType}', skipping audio[{i}]");
                        continue;
                    }

                    string filePath = GetUniquePath(importDir, $"audio_{i:D3}{ext}");
                    File.WriteAllBytes(filePath, buffer.ToArray());
                    _audioFilePaths[i] = filePath;
                }
                else if (!string.IsNullOrEmpty(audio.uri))
                {
                    Debug.LogWarning($"[OBAudio] URI-based audio not supported for runtime import (audio[{i}]: {audio.uri}), skipping");
                }
            }
        }

        private void CreateSoundClipWidgets()
        {
            var previousCanvas = App.Scene.ActiveCanvas;
            try
            {
                App.Scene.ActiveCanvas = App.Scene.MainCanvas;
                foreach (var pending in _pendingNodes)
                {
                    CreateWidgetFromPending(pending);
                }
            }
            finally
            {
                App.Scene.ActiveCanvas = previousCanvas;
                SoundClipCatalog.Instance.ForceCatalogScan();
            }
        }

        private void CreateWidgetFromPending(PendingAudioNode pending)
        {
            var emitter = pending.Emitter;
            if (emitter.sources == null || emitter.sources.Count == 0)
            {
                Debug.LogWarning($"[OBAudio] Emitter '{emitter.name}' has no sources, skipping");
                return;
            }

            // Use the first source only (Open Brush SoundClipWidget supports one clip per widget)
            var source = emitter.sources[0].Value;
            if (source.audio == null)
            {
                Debug.LogWarning($"[OBAudio] Source '{source.Name}' has no audio reference, skipping");
                return;
            }

            int audioIndex = source.audio.Id;
            if (!_audioFilePaths.TryGetValue(audioIndex, out var filePath))
            {
                Debug.LogWarning($"[OBAudio] No extracted file for audio index {audioIndex}, skipping");
                return;
            }

            var soundClip = new SoundClip(filePath);

            float gain = (emitter.gain > 0 ? emitter.gain : 1f) * (source.gain ?? 1f);
            bool loop = source.loop ?? true;
            bool isSpatial = emitter.type == "positional";
            float spatialBlend = isSpatial ? 1f : 0f;
            float minDist = emitter.positional?.refDistance ?? 1f;
            float maxDist = emitter.positional?.maxDistance ?? 500f;

            // Capture world transform before the placeholder node is destroyed
            Vector3 position = pending.NodeObject.transform.position;
            Quaternion rotation = pending.NodeObject.transform.rotation;

            var widget = UnityEngine.Object.Instantiate(WidgetManager.m_Instance.SoundClipWidgetPrefab);
            widget.m_LoadingFromSketch = true; // suppress intro animation
            widget.transform.parent = App.Instance.m_CanvasTransform;
            widget.transform.localScale = Vector3.one;
            widget.SetSoundClip(soundClip);
            widget.SetAudioProperties(gain, loop, spatialBlend, minDist, maxDist);
            widget.Show(bShow: true, bPlayAudio: false);
            widget.transform.position = position;
            widget.transform.rotation = rotation;
            widget.SetCanvas(App.Scene.MainCanvas);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(widget.GetTiltMeterCost(), up: true);

            // Remove the empty placeholder node that the GLTF importer created for this entry
            UnityEngine.Object.Destroy(pending.NodeObject);
        }

        private static string MimeTypeToExtension(string mimeType)
        {
            return mimeType switch
            {
                "audio/mpeg" => ".mp3",
                "audio/wav" => ".wav",
                "audio/ogg" => ".ogg",
                _ => null,
            };
        }

        private static string GetUniquePath(string directory, string filename)
        {
            string path = Path.Combine(directory, filename);
            if (!File.Exists(path)) return path;

            string name = Path.GetFileNameWithoutExtension(filename);
            string ext = Path.GetExtension(filename);
            for (int i = 1; i < 1000; i++)
            {
                path = Path.Combine(directory, $"{name}_{i}{ext}");
                if (!File.Exists(path)) return path;
            }
            return Path.Combine(directory, $"{name}_{Guid.NewGuid()}{ext}");
        }
    }
}
