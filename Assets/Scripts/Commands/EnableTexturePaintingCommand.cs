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
                            InkCanvas canvas = mesh.gameObject.GetComponent<InkCanvas>();
                            if (canvas == null)
                            {
                                mesh.GetComponent<MeshRenderer>().material.mainTexture = TexturePainterManager.m_Instance.DefaultCanvasTexture;

                                var paintSet = new List<InkCanvas.PaintSet>
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
                                mesh.gameObject.AddInkCanvas(paintSet);
                                mesh.gameObject.layer = LayerMask.NameToLayer("TexturePaint");
                            }

                            var collider = mesh.gameObject.GetComponent<MeshCollider>();
                            if (collider == null)
                            {
                                mesh.gameObject.AddComponent<MeshCollider>();
                            }
                        }
                        break;
                    }
                case ImageWidget:
                    // TODO
                    break;
                case StencilWidget:
                    // TODO
                    break;
                default:
                    Debug.LogWarning($"{m_Widget.name} is not a valid paint target");
                    return;
            }
            m_Initialized = true;
        }

        protected override void OnUndo()
        {
            if (m_Widget == null) return;
            m_OriginalWidget.gameObject.SetActive(true);
            m_Widget.gameObject.SetActive(false);
        }

    }
} // namespace TiltBrush
