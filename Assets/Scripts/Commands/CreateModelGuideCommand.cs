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
using System.Linq;

namespace TiltBrush
{
    /// Creates a model guide while retaining the source model and swapping the selection.
    internal sealed class CreateModelGuideCommand : BaseCommand
    {
        private readonly CreateWidgetCommand m_CreateCommand;

        internal ModelStencil Result => m_CreateCommand.Widget as ModelStencil;

        internal CreateModelGuideCommand(ModelWidget source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (source.Model == null || !source.Model.m_Valid ||
                !source.GetMeshes().Any(
                    meshFilter => meshFilter != null && meshFilter.sharedMesh != null &&
                        meshFilter.sharedMesh.vertexCount > 0))
            {
                throw new InvalidOperationException(
                    "The selected model is not loaded or has no mesh geometry.");
            }
            if (WidgetManager.m_Instance.ModelStencilPrefab == null)
            {
                throw new InvalidOperationException("The model-guide prefab is unavailable.");
            }

            CanvasScript targetCanvas = source.Canvas == App.Scene.SelectionCanvas
                ? source.m_PreviousCanvas
                : source.Canvas;
            if (targetCanvas == null || App.Scene.IsLayerDeleted(targetCanvas))
            {
                targetCanvas = App.ActiveCanvas;
            }

            TrTransform pose = TrTransform.TRS(
                source.transform.position, source.transform.rotation,
                source.GetSignedWidgetSize());
            m_CreateCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.ModelStencilPrefab,
                pose,
                forceTransform: true,
                parent: this,
                canvas: targetCanvas);
            new ConfigureModelGuideCommand(
                m_CreateCommand, source.Model, source.Group, source.Pinned, this);
            new SelectModelGuideCommand(m_CreateCommand, source, targetCanvas, this);
        }

        protected override void OnUndo()
        {
            SelectionManager.m_Instance.UpdateSelectionWidget();
            App.Switchboard.TriggerSelectionChanged();
        }

        private sealed class ConfigureModelGuideCommand : BaseCommand
        {
            private readonly CreateWidgetCommand m_CreateCommand;
            private readonly Model m_Model;
            private readonly SketchGroupTag m_Group;
            private readonly bool m_Pinned;

            internal ConfigureModelGuideCommand(
                CreateWidgetCommand createCommand, Model model, SketchGroupTag group,
                bool pinned, BaseCommand parent) : base(parent)
            {
                m_CreateCommand = createCommand;
                m_Model = model;
                m_Group = group;
                m_Pinned = pinned;
            }

            protected override void OnRedo()
            {
                if (m_CreateCommand.Widget is not ModelStencil stencil)
                {
                    throw new InvalidOperationException(
                        "The model-guide prefab did not create a model guide.");
                }

                stencil.Model = m_Model;
                stencil.Group = m_Group;
                if (stencil.Pinned != m_Pinned)
                {
                    stencil.SetPinned(m_Pinned, fromSave: true);
                }
            }
        }

        private sealed class SelectModelGuideCommand : BaseCommand
        {
            private readonly CreateWidgetCommand m_CreateCommand;
            private readonly ModelWidget m_Source;
            private readonly CanvasScript m_TargetCanvas;

            internal SelectModelGuideCommand(
                CreateWidgetCommand createCommand, ModelWidget source,
                CanvasScript targetCanvas, BaseCommand parent) : base(parent)
            {
                m_CreateCommand = createCommand;
                m_Source = source;
                m_TargetCanvas = targetCanvas;
            }

            protected override void OnRedo()
            {
                if (m_CreateCommand.Widget is not ModelStencil stencil)
                {
                    throw new InvalidOperationException(
                        "The model-guide prefab did not create a model guide.");
                }

                if (SelectionManager.m_Instance.IsWidgetSelected(m_Source))
                {
                    SelectionManager.m_Instance.DeselectWidget(m_Source, m_TargetCanvas);
                }
                if (!SelectionManager.m_Instance.IsWidgetSelected(stencil))
                {
                    SelectionManager.m_Instance.SelectWidget(stencil);
                }
                SelectionManager.m_Instance.UpdateSelectionWidget();
                App.Switchboard.TriggerSelectionChanged();
            }

            protected override void OnUndo()
            {
                if (m_CreateCommand.Widget is ModelStencil stencil &&
                    SelectionManager.m_Instance.IsWidgetSelected(stencil))
                {
                    SelectionManager.m_Instance.DeselectWidget(stencil, m_TargetCanvas);
                }
                if (m_Source != null && !SelectionManager.m_Instance.IsWidgetSelected(m_Source))
                {
                    SelectionManager.m_Instance.SelectWidget(m_Source);
                }
            }
        }
    }
}
