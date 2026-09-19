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
using System.Linq;

namespace TiltBrush
{
    /// Replaces selected guides with one editable SDF as a single undoable operation.
    internal sealed class ConvertGuidesToSdfCommand : BaseCommand
    {
        private readonly CreateWidgetCommand m_CreateCommand;

        internal SdfStencil Result => m_CreateCommand.Widget as SdfStencil;

        internal ConvertGuidesToSdfCommand(
            IReadOnlyList<StencilWidget> guides, CanvasScript targetCanvas)
        {
            if (guides == null)
            {
                throw new ArgumentNullException(nameof(guides));
            }
            if (targetCanvas == null)
            {
                throw new ArgumentNullException(nameof(targetCanvas));
            }

            SdfGuideConversion.Result conversion = SdfGuideConversion.Build(guides, targetCanvas);
            var definitions = new List<SdfStencil.ComponentDefinition>(conversion.Components);
            var sourceWidgets = guides.Cast<GrabWidget>().ToList();

            m_CreateCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.SdfStencilPrefab,
                conversion.Pose_GS,
                forceTransform: true,
                parent: this,
                canvas: targetCanvas);
            new ConfigureCreatedSdfCommand(m_CreateCommand, definitions, this);
            new DeleteSelectionCommand(null, sourceWidgets, this);
            new SelectCreatedSdfCommand(m_CreateCommand, targetCanvas, this);
        }

        private sealed class ConfigureCreatedSdfCommand : BaseCommand
        {
            private readonly CreateWidgetCommand m_CreateCommand;
            private readonly IReadOnlyList<SdfStencil.ComponentDefinition> m_Definitions;

            internal ConfigureCreatedSdfCommand(
                CreateWidgetCommand createCommand,
                IReadOnlyList<SdfStencil.ComponentDefinition> definitions,
                BaseCommand parent) : base(parent)
            {
                m_CreateCommand = createCommand;
                m_Definitions = definitions;
            }

            protected override void OnRedo()
            {
                var stencil = m_CreateCommand.Widget as SdfStencil;
                if (stencil == null)
                {
                    throw new InvalidOperationException(
                        "The configured SDF guide prefab did not create an SDF guide.");
                }
                stencil.ReplaceComponents(m_Definitions);
            }
        }

        private sealed class SelectCreatedSdfCommand : BaseCommand
        {
            private readonly CreateWidgetCommand m_CreateCommand;
            private readonly CanvasScript m_TargetCanvas;

            internal SelectCreatedSdfCommand(
                CreateWidgetCommand createCommand,
                CanvasScript targetCanvas,
                BaseCommand parent) : base(parent)
            {
                m_CreateCommand = createCommand;
                m_TargetCanvas = targetCanvas;
            }

            protected override void OnRedo()
            {
                if (!(m_CreateCommand.Widget is SdfStencil stencil))
                {
                    throw new InvalidOperationException(
                        "The configured SDF guide prefab did not create an SDF guide.");
                }
                if (!SelectionManager.m_Instance.IsWidgetSelected(stencil))
                {
                    SelectionManager.m_Instance.SelectWidget(stencil);
                }
            }

            protected override void OnUndo()
            {
                if (m_CreateCommand.Widget is SdfStencil stencil &&
                    SelectionManager.m_Instance.IsWidgetSelected(stencil))
                {
                    SelectionManager.m_Instance.DeselectWidget(stencil, m_TargetCanvas);
                }
            }
        }
    }
}
