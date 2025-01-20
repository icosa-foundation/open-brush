// Copyright 2023 The Open Brush Authors
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

using System.Collections.Generic;
using TiltBrush;
using UnityEngine;

namespace OpenBrush
{

    public class MultiplayerAudioSourcesManager : MonoBehaviour
    {
        public static MultiplayerAudioSourcesManager m_Instance;
        private Dictionary<int, AudioSource> sources; // Key: playerId, Value: AudioSource
        private float _previousScale;

        private void Awake()
        {
            sources = new Dictionary<int, AudioSource>();

            if (m_Instance == null) m_Instance = this;
            else Debug.LogWarning("Multiple instances of MultiplayerAudioSourcesManager detected!");

        }

        public void AddAudioSource(int playerId, AudioSource source)
        {
            if (source != null)
                sources[playerId] = source;
        }

        void Update()
        {
            float currentScale = App.Scene.Pose.scale;

            if (!Mathf.Approximately(currentScale, _previousScale))
            {
                _previousScale = currentScale;
                UpdateAudioSources(currentScale);
            }
        }

        private void UpdateAudioSources(float sceneScale)
        {
            // Loop backward to remove invalid AudioSources
            foreach (var kvp in new List<KeyValuePair<int, AudioSource>>(sources))
            {
                var source = kvp.Value;
                if (source != null)
                {
                    float adjustedMaxDistance = CalculateMaxDistance(sceneScale);
                    source.maxDistance = adjustedMaxDistance;
                }
                else sources.Remove(kvp.Key); // Remove invalid AudioSource by playerId
            }
        }

        private float CalculateMaxDistance(float sceneScale)
        {
            // This is based on OpenBrush default scene max radius
            // - At scale 0.1, the mountains diameter is 200 (close range).
            // - At scale 1.0, the mountains diameter is 20000 (far range).
            return Mathf.Lerp(200f, 20000f, Mathf.Clamp01(sceneScale));
        }

        public void MuteAudioSources()
        {
            foreach (var source in sources.Values)
                if (source != null) source.mute = true;
        }

        public void UnmuteAudioSources()
        {
            foreach (var source in sources.Values)
                if (source != null) source.mute = false;
        }

        public void MuteAudioSourcesForPlayer(int playerId)
        { AudioSourcesMuteStateForPlayer(playerId, true); }

        public void UnmuteAudioSourcesForPlayer(int playerId)
        { AudioSourcesMuteStateForPlayer(playerId, false); }

        public void AudioSourcesMuteStateForPlayer(int playerId, bool state)
        {
            sources.TryGetValue(playerId, out AudioSource source);
            if (source == null) return;
            source.mute = state;
        }
    }
}
