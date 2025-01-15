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
using System.Linq;
using TiltBrush;
using UnityEngine;

public class MultiplayerAudioSourcesManager : MonoBehaviour
{
    public static MultiplayerAudioSourcesManager m_Instance;
    private List<AudioSource> sources;
    private float _previousScale;

    public void AddAudioSource(AudioSource source)
    {

        sources.Append(source);

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
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            var source = sources[i];
            if (source != null)
            {
                float adjustedMaxDistance = CalculateMaxDistance(sceneScale);
                source.maxDistance = adjustedMaxDistance;
            }
            else sources.RemoveAt(i);
        }
    }

    private float CalculateMaxDistance(float sceneScale)
    {
        // This is based on OpenBrush default scene max radius
        // - At scale 0.1, the mountains diameter is 200 (close range).
        // - At scale 1.0,the mountains diameter is 20000 (far range).
        return Mathf.Lerp(200f, 20000f, Mathf.Clamp01(sceneScale));
    }

}
