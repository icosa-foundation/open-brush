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

using System.Collections.Generic;
using Es.InkPainter;
using UnityEngine;

namespace TiltBrush
{

    public class EnableTexturePaintingCommand : BaseCommand
    {
        private GrabWidget m_Widget;
        private GrabWidget m_OriginalWidget;
        private bool m_Initialized;
        private List<InkCanvas.PaintSet> m_PaintSet;

        public GrabWidget Widget => m_Widget;

        override public bool NeedsSave
        {
            get
            {
                return true;
            }
        }

        public EnableTexturePaintingCommand(
            GrabWidget widget,
            BaseCommand parent = null)
            : base(parent)
        {
            if (widget != null && widget is ModelWidget || widget is ImageWidget || widget is StencilWidget)
            {
                m_Widget = widget;
            }
            m_PaintSet = new List<InkCanvas.PaintSet>
            {
                new(
                    mainTextureName: "_MainTex",
                    normalTextureName: "_BumpMap",
                    heightTextureName: "_HeightMap",
                    useMainPaint: true,
                    useNormalPaint: false,
                    useHeightPaint: false
                )
            };

        }

        protected override void OnRedo()
        {
            if (m_Widget == null) return;
            if (m_OriginalWidget == null)
            {
                m_OriginalWidget = m_Widget.Clone();
            }
            m_OriginalWidget.gameObject.SetActive(false);
            m_OriginalWidget = m_Widget.Clone();
            m_OriginalWidget.gameObject.SetActive(false);

            if (m_Initialized) return;

            switch (m_Widget)
            {
                case ModelWidget:
                    {
                        var objModelScript = m_Widget.GetComponentInChildren<ObjModelScript>();
                        foreach (var mesh in objModelScript.m_MeshChildren)
                        {
                            AddInkCanvas(mesh.gameObject);
                        }
                        break;
                    }

                case ImageWidget:
                    var imageWidget = m_Widget as ImageWidget;
                    AddInkCanvas(imageWidget.gameObject);
                    break;

                case StencilWidget:
                    var stencilWidget = m_Widget as StencilWidget;
                    AddInkCanvas(stencilWidget.gameObject);
                    break;

                default:
                    Debug.LogWarning($"{m_Widget.name} is not a valid paint target");
                    return;
            }
            m_Initialized = true;
        }
        private void AddInkCanvas(GameObject gameObject)
        {
            gameObject.GetComponent<MeshRenderer>().material.mainTexture = TexturePainterManager.m_Instance.DefaultCanvasTexture;
            var canvas = gameObject.GetComponent<InkCanvas>();
            if (canvas == null)
            {
                gameObject.AddInkCanvas(m_PaintSet);
                gameObject.layer = LayerMask.NameToLayer("TexturePaint");
            }
            var collider = gameObject.GetComponent<Collider>();
            if (!(collider is MeshCollider))
            {
                Object.Destroy(collider);
                collider = null;
            }
            if (collider == null)
            {
                gameObject.AddComponent<MeshCollider>();
            }
        }

        protected override void OnUndo()
        {
            if (m_Widget == null) return;
            m_OriginalWidget.gameObject.SetActive(true);
            m_Widget.gameObject.SetActive(false);
        }
    }
} // namespace TiltBrush
