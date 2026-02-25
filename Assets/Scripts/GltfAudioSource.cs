// Copyright 2026 The Open Brush Authors
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

using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{
    /// <summary>
    /// Attached to audio-emitter nodes imported from GLTF KHR_audio_emitter.
    /// Loads and plays audio when the model is active in the scene, and provides
    /// the data needed to create a SoundClipWidget when the model is broken apart.
    /// </summary>
    public class GltfAudioSource : MonoBehaviour
    {
        public string AbsoluteFilePath;
        public float Gain = 1f;
        public bool Loop = true;
        public float SpatialBlend = 0f;
        public float MinDistance = 1f;
        public float MaxDistance = 500f;
        public bool AutoPlay = true;

        private AudioSource _audioSource;
        private AudioClip _loadedClip;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (GetComponentInParent<ModelWidget>() == null) return;

            if (_loadedClip != null)
            {
                _audioSource.clip = _loadedClip;
                if (AutoPlay) _audioSource.Play();
            }
            else if (!string.IsNullOrEmpty(AbsoluteFilePath) && File.Exists(AbsoluteFilePath))
            {
                StartCoroutine(LoadAndPlay());
            }
        }

        private void OnDisable()
        {
            _audioSource.Stop();
            StopAllCoroutines();
        }

        private IEnumerator LoadAndPlay()
        {
            string url = $"file:///{AbsoluteFilePath.Replace('\\', '/')}";
            AudioType audioType = GetAudioType(AbsoluteFilePath);
            using var request = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                _loadedClip = DownloadHandlerAudioClip.GetContent(request);
                _audioSource.clip = _loadedClip;
                _audioSource.volume = Gain;
                _audioSource.loop = Loop;
                _audioSource.spatialBlend = SpatialBlend;
                _audioSource.minDistance = MinDistance;
                _audioSource.maxDistance = MaxDistance;
                if (AutoPlay) _audioSource.Play();
            }
            else
            {
                Debug.LogWarning($"[GltfAudio] Failed to load audio from {AbsoluteFilePath}: {request.error}");
            }
        }

        private static AudioType GetAudioType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".mp3" => AudioType.MPEG,
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                _ => AudioType.UNKNOWN,
            };
        }
    }
}
