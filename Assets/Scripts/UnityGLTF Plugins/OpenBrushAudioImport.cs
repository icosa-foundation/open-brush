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
using System.Security.Cryptography;
using GLTF.Schema;
using Newtonsoft.Json.Linq;
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
            GLTFProperty.RegisterExtension(new OpenBrushAudioEmitterFactory());
            return new OpenBrushAudioImportContext(context);
        }
    }

    internal class OpenBrushAudioEmitterExtension : KHR_audio_emitter
    {
        public readonly HashSet<KHR_AudioEmitter> EmittersWithExplicitGain = new();
    }

    internal class OpenBrushAudioEmitterFactory : ExtensionFactory
    {
        private readonly KHR_audio_emitterFactory _defaultFactory = new();

        public OpenBrushAudioEmitterFactory()
        {
            ExtensionName = KHR_audio_emitter.ExtensionName;
        }

        public override IExtension Deserialize(GLTFRoot root, JProperty extensionToken)
        {
            IExtension parsedExtension = _defaultFactory.Deserialize(root, extensionToken);
            if (parsedExtension is not KHR_audio_emitter parsedAudioExtension)
            {
                return parsedExtension;
            }

            var audioExtension = new OpenBrushAudioEmitterExtension
            {
                audio = parsedAudioExtension.audio,
                sources = parsedAudioExtension.sources,
                emitters = parsedAudioExtension.emitters,
            };

            if (extensionToken.Value[nameof(KHR_audio_emitter.emitters)] is JArray emitterTokens)
            {
                int emitterCount = Math.Min(emitterTokens.Count, audioExtension.emitters.Count);
                for (int i = 0; i < emitterCount; i++)
                {
                    if (emitterTokens[i]?[nameof(KHR_AudioEmitter.gain)] != null)
                    {
                        audioExtension.EmittersWithExplicitGain.Add(audioExtension.emitters[i]);
                    }
                }
            }

            return audioExtension;
        }
    }

    public class OpenBrushAudioImportContext : GLTFImportPluginContext
    {
        private readonly GLTFImportContext _context;
        private KHR_audio_emitter _audioExtension;
        private readonly HashSet<KHR_AudioEmitter> _emittersWithExplicitGain = new();

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
            {
                _audioExtension = ext as KHR_audio_emitter;
                if (ext is OpenBrushAudioEmitterExtension openBrushAudioExtension)
                {
                    _emittersWithExplicitGain.UnionWith(
                        openBrushAudioExtension.EmittersWithExplicitGain);
                }
            }
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

            // Store outside the sound clip library so the catalog doesn't pick these up. This is a
            // content-addressed cache: repeated imports reuse files, and the OS may purge them.
            string importDir = Path.Combine(Application.temporaryCachePath, "GltfAudio");
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

                    byte[] contents = buffer.ToArray();
                    string filePath = GetCachePath(importDir, contents, ext);
                    if (!File.Exists(filePath))
                    {
                        File.WriteAllBytes(filePath, contents);
                    }
                    _audioFilePaths[i] = filePath;
                }
                else if (!string.IsNullOrEmpty(audio.uri))
                {
                    if (string.IsNullOrEmpty(GltfDirectory))
                    {
                        Debug.LogWarning($"[OBAudio] Cannot resolve sidecar URI '{audio.uri}': GltfDirectory not set.");
                        continue;
                    }

                    string srcPath = ResolveSidecarPath(audio.uri);
                    if (srcPath == null)
                    {
                        Debug.LogWarning($"[OBAudio] Audio sidecar file not found for uri '{audio.uri}' in {GltfDirectory}");
                        continue;
                    }

                    string ext = Path.GetExtension(srcPath);
                    string destPath = GetCachePath(importDir, srcPath, ext);
                    if (!File.Exists(destPath))
                    {
                        File.Copy(srcPath, destPath);
                    }
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

            foreach (var sourceId in emitter.sources)
            {
                if (sourceId != null)
                {
                    SetupAudioSourceOnNode(pending.NodeObject, emitter, sourceId.Value);
                }
            }
        }

        private void SetupAudioSourceOnNode(
            GameObject nodeObject, KHR_AudioEmitter emitter, KHR_AudioSource source)
        {
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
            float emitterGain = _emittersWithExplicitGain.Contains(emitter) ? emitter.gain : 1f;
            float gain = emitterGain * (source.gain ?? 1f);
            bool loop = source.loop ?? true;
            bool autoPlay = source.autoPlay ?? true;

            var audioSource = nodeObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // GltfAudioSource handles playback
            audioSource.spatialBlend = isSpatial ? 1f : 0f;
            audioSource.minDistance = emitter.positional?.refDistance ?? 1f;
            audioSource.maxDistance = emitter.positional?.maxDistance ?? 500f;

            var gltfAudio = nodeObject.AddComponent<GltfAudioSource>();
            gltfAudio.SetAudioSource(audioSource);
            gltfAudio.AbsoluteFilePath = filePath;
            gltfAudio.Gain = gain;
            gltfAudio.Loop = loop;
            gltfAudio.SpatialBlend = isSpatial ? 1f : 0f;
            gltfAudio.MinDistance = emitter.positional?.refDistance ?? 1f;
            gltfAudio.MaxDistance = emitter.positional?.maxDistance ?? 500f;
            gltfAudio.AutoPlay = autoPlay;
        }

        /// glTF uris are percent-encoded, so "My%20Track.ogg" has to be unescaped before it can be
        /// used as a file name. Falls back to the raw uri for files whose names really do contain
        /// escape-like sequences.
        private string ResolveSidecarPath(string uri)
        {
            string decoded = uri;
            try
            {
                decoded = Uri.UnescapeDataString(uri);
            }
            catch (UriFormatException)
            {
                // Leave the uri as-is and let the existence checks below decide.
            }

            foreach (string candidate in new[] { decoded, uri })
            {
                string path;
                try
                {
                    path = Path.GetFullPath(Path.Combine(GltfDirectory, candidate));
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        private static string GetCachePath(string directory, byte[] contents, string extension)
        {
            using var sha256 = SHA256.Create();
            return Path.Combine(directory, $"{FormatHash(sha256.ComputeHash(contents))}{extension}");
        }

        private static string GetCachePath(string directory, string sourcePath, string extension)
        {
            using var stream = File.OpenRead(sourcePath);
            using var sha256 = SHA256.Create();
            return Path.Combine(directory, $"{FormatHash(sha256.ComputeHash(stream))}{extension}");
        }

        private static string FormatHash(byte[] hash) =>
            BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

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

    }
}
