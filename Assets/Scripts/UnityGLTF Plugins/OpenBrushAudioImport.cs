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
    /// Imports KHR_audio_emitter nodes from GLTF by adding GltfAudioSource components
    /// to the relevant nodes in the model hierarchy. Audio plays when the model is active.
    /// SoundClipWidgets are only created when the model is broken apart.
    /// </summary>
    public class OpenBrushAudioImport : GLTFImportPlugin
    {
        public override string DisplayName => "Open Brush Audio Import";
        public override string Description => "Adds GltfAudioSource components from KHR_audio_emitter nodes.";

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

        // audio array index → absolute file path of extracted audio
        private readonly Dictionary<int, string> _audioFilePaths = new();

        /// Set by the import call site so sidecar URI audio can be resolved.
        public string GltfDirectory { get; set; }

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
            SetupAudioComponents();
        }

        private void ExtractAudioFiles()
        {
            if (_audioExtension.audio == null) return;

            // Store outside the sound clip library so the catalog doesn't pick these up.
            string importDir = Path.Combine(Application.persistentDataPath, "GltfAudio");
            Directory.CreateDirectory(importDir);

            for (int i = 0; i < _audioExtension.audio.Count; i++)
            {
                var audio = _audioExtension.audio[i];

                if (audio.bufferView != null)
                {
                    var bvId = audio.bufferView.Id;
                    var bvCount = audio.bufferView.Root?.BufferViews?.Count ?? 0;
                    if (bvId < 0 || bvId >= bvCount)
                    {
                        Debug.LogWarning($"[OBAudio] audio[{i}].bufferView.Id={bvId} is out of range (bufferViews.Count={bvCount}), skipping");
                        continue;
                    }

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
                    if (string.IsNullOrEmpty(GltfDirectory))
                    {
                        Debug.LogWarning($"[OBAudio] Cannot resolve sidecar URI '{audio.uri}': GltfDirectory not set.");
                        continue;
                    }

                    string srcPath = Path.GetFullPath(Path.Combine(GltfDirectory, audio.uri));
                    if (!File.Exists(srcPath))
                    {
                        Debug.LogWarning($"[OBAudio] Audio sidecar file not found: {srcPath}");
                        continue;
                    }

                    string ext = Path.GetExtension(srcPath);
                    string destPath = GetUniquePath(importDir, $"audio_{i:D3}{ext}");
                    File.Copy(srcPath, destPath);
                    _audioFilePaths[i] = destPath;
                }
            }
        }

        private void SetupAudioComponents()
        {
            foreach (var pending in _pendingNodes)
                SetupAudioOnNode(pending);
        }

        private void SetupAudioOnNode(PendingAudioNode pending)
        {
            var emitter = pending.Emitter;
            if (emitter.sources == null || emitter.sources.Count == 0)
            {
                Debug.LogWarning($"[OBAudio] Emitter '{emitter.name}' has no sources, skipping");
                return;
            }

            var source = emitter.sources[0].Value;
            if (source.audio == null)
            {
                Debug.LogWarning($"[OBAudio] Source has no audio reference, skipping");
                return;
            }

            int audioIndex = source.audio.Id;
            if (!_audioFilePaths.TryGetValue(audioIndex, out var filePath))
            {
                Debug.LogWarning($"[OBAudio] No extracted file for audio index {audioIndex}, skipping");
                return;
            }

            bool isSpatial = emitter.type == "positional";
            float gain = (emitter.gain > 0 ? emitter.gain : 1f) * (source.gain ?? 1f);
            bool loop = source.loop ?? true;
            bool autoPlay = source.autoPlay ?? true;

            var audioSource = pending.NodeObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // GltfAudioSource handles playback
            audioSource.spatialBlend = isSpatial ? 1f : 0f;
            audioSource.minDistance = emitter.positional?.refDistance ?? 1f;
            audioSource.maxDistance = emitter.positional?.maxDistance ?? 500f;

            var gltfAudio = pending.NodeObject.AddComponent<GltfAudioSource>();
            gltfAudio.AbsoluteFilePath = filePath;
            gltfAudio.Gain = gain;
            gltfAudio.Loop = loop;
            gltfAudio.SpatialBlend = isSpatial ? 1f : 0f;
            gltfAudio.MinDistance = emitter.positional?.refDistance ?? 1f;
            gltfAudio.MaxDistance = emitter.positional?.maxDistance ?? 500f;
            gltfAudio.AutoPlay = autoPlay;
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
