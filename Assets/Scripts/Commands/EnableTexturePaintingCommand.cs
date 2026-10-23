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

        public GrabWidget Widget => m_Widget != null ? m_Widget : m_OriginalWidget;

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
            if (widget != null &&
                (widget is ModelWidget || widget is ImageWidget || widget is StencilWidget) &&
                widget.GetComponentInChildren<InkCanvas>(true) == null)
            {
                m_OriginalWidget = widget;
            }

        }

        protected override void OnRedo()
        {
            if (m_OriginalWidget == null) return;
            if (m_Widget == null)
            {
                // Earlier commands retain the original widget, so convert a separate instance.
                m_Widget = m_OriginalWidget.Clone();
                m_Widget.Group = m_OriginalWidget.Group;
                if (m_OriginalWidget.Pinned) m_Widget.SetPinned(true, fromSave: true);
            }
            if (m_Initialized)
            {
                SwapVisibleWidget(m_Widget, m_OriginalWidget);
                return;
            }

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
                case StencilWidget:
                    // Widget roots own interaction state; their renderable mesh is a child.
                    foreach (var renderer in m_Widget.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (renderer.GetComponent<MeshFilter>() != null)
                        {
                            AddInkCanvas(renderer.gameObject);
                        }
                    }
                    break;

                default:
                    Debug.LogWarning($"{m_Widget.name} is not a valid paint target");
                    return;
            }
            m_Initialized = true;
            SwapVisibleWidget(m_Widget, m_OriginalWidget);
        }

        private static void SwapVisibleWidget(GrabWidget visible, GrabWidget hidden)
        {
            var selection = SelectionManager.m_Instance;
            if (selection != null && selection.IsWidgetSelected(hidden))
            {
                visible.m_PreviousCanvas = hidden.m_PreviousCanvas;
                SetCanvasAndLayers(visible, App.Scene.SelectionCanvas);
                // Register first so swapping the last selected widget does not reset its pose.
                selection.RegisterWidgetsInSelectionCanvas(new[] { visible });
                selection.DeregisterWidgetsInSelectionCanvas(new[] { hidden });
            }
            else if (visible.Canvas == App.Scene.SelectionCanvas)
            {
                SetCanvasAndLayers(visible, visible.m_PreviousCanvas ?? App.ActiveCanvas);
            }
            if (hidden.Canvas == App.Scene.SelectionCanvas)
            {
                SetCanvasAndLayers(hidden, hidden.m_PreviousCanvas ?? App.ActiveCanvas);
            }
            selection?.ReplaceCachedWidget(hidden, visible);
            SketchControlsScript.m_Instance?.ReplaceLastGrabWidget(hidden, visible);
            hidden.Hide();
            visible.RestoreFromToss();
        }

        private static void SetCanvasAndLayers(GrabWidget widget, CanvasScript canvas)
        {
            widget.SetCanvas(canvas);
            widget.RestoreCanvasLayers(canvas.gameObject.layer);
        }

        private void AddInkCanvas(GameObject gameObject)
        {
            if (gameObject.GetComponent<InkCanvas>() != null) return;

            var materials = gameObject.GetComponent<MeshRenderer>().materials;
            var paintSets = new List<InkCanvas.PaintSet>(materials.Length);
            foreach (var material in materials)
            {
                material.mainTexture = TexturePainterManager.m_Instance.DefaultCanvasTexture;
                // InkCanvas owns and mutates these entries; never share them across meshes.
                paintSets.Add(new InkCanvas.PaintSet(
                    mainTextureName: "_MainTex",
                    normalTextureName: "_BumpMap",
                    heightTextureName: "_HeightMap",
                    useMainPaint: true,
                    useNormalPaint: false,
                    useHeightPaint: false,
                    material: material));
            }

            gameObject.AddInkCanvas(paintSets);
            gameObject.layer = LayerMask.NameToLayer("TexturePaint");
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
            SwapVisibleWidget(m_OriginalWidget, m_Widget);
        }

        protected override void OnDispose()
        {
            // Only the replacement belongs to this command; older commands own the original.
            if (m_Widget != null && !m_Widget.gameObject.activeSelf)
            {
                WidgetManager.m_Instance.UnregisterGrabWidget(m_Widget.gameObject);
                Object.Destroy(m_Widget.gameObject);
            }
        }
    }
} // namespace TiltBrush
