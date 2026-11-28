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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    // Applies the per-layer post-production spatial offsets for the duration of a render:
    //
    //     using (new PostProductionLayerOffsetScope()) { camera.Render(); }
    //
    // Each layer with an offset has its canvas set to (authored local pose x offset) on
    // construction and put back exactly on Dispose, within the same call on the main
    // thread, so no frame passes and nothing else can see or save the temporary pose.
    // Unity then renders exactly as normal (every brush, transparency sorting, the depth
    // pass), with all layers in one pass so dancers still occlude each other correctly.
    //
    // Deliberately bypasses CanvasScript.LocalPose (which emits PoseChanged and clears
    // transform.hasChanged): sets the transform through Coords.AsLocal directly and
    // restores hasChanged, so no listeners fire and no change is detectable afterwards.
    // The global offset is not applied here; the live CG camera handles it.
    public sealed class PostProductionLayerOffsetScope : IDisposable
    {
        private struct Saved
        {
            public Transform transform;
            public TrTransform localPose;
            public bool hasChanged;
        }

        private readonly List<Saved> m_Saved = new List<Saved>();

        public PostProductionLayerOffsetScope()
        {
            if (App.Scene == null) { return; }
            int index = 0;
            foreach (var canvas in App.Scene.LayerCanvases)
            {
                TrTransform offset = PostProductionSettings.GetLayerSpatialOffset(index);
                index++;
                if (offset == TrTransform.identity || canvas == null) { continue; }
                Transform t = canvas.transform;
                TrTransform authored = Coords.AsLocal[t];
                m_Saved.Add(new Saved { transform = t, localPose = authored, hasChanged = t.hasChanged });
                Coords.AsLocal[t] = authored * offset;
            }
        }

        public void Dispose()
        {
            for (int i = m_Saved.Count - 1; i >= 0; i--)
            {
                var s = m_Saved[i];
                if (s.transform == null) { continue; }
                Coords.AsLocal[s.transform] = s.localPose;
                s.transform.hasChanged = s.hasChanged;
            }
            m_Saved.Clear();
        }
    }
}
