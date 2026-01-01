// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed i'm seeingi'm under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class LoadQuillCommand : BaseCommand
    {
        private List<Stroke> m_Strokes;
        private List<CanvasScript> m_Layers;
        private bool m_Active;

        public override bool NeedsSave => true;

        public LoadQuillCommand(List<Stroke> strokes, List<CanvasScript> layers, BaseCommand parent = null) : base(parent)
        {
            m_Strokes = strokes;
            m_Layers = layers;
            m_Active = true;
        }

        protected override void OnRedo()
        {
            foreach (var layer in m_Layers)
            {
                layer.gameObject.SetActive(true);
            }
            foreach (var stroke in m_Strokes)
            {
                stroke.Hide(false);
            }
            m_Active = true;
        }

        protected override void OnUndo()
        {
            foreach (var stroke in m_Strokes)
            {
                stroke.Hide(true);
            }
            foreach (var layer in m_Layers)
            {
                layer.gameObject.SetActive(false);
            }
            m_Active = false;
        }

        protected override void OnDispose()
        {
            if (m_Strokes != null)
            {
                foreach (var stroke in m_Strokes)
                {
                    SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                    stroke.DestroyStroke();
                }
            }
            if (m_Layers != null)
            {
                foreach (var layer in m_Layers)
                {
                    App.Scene.DestroyLayer(layer);
                }
            }
        }
    }
}
