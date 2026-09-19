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
using IsoMesh;
using UnityEngine;

namespace TiltBrush
{
    /// Configures the reusable selection-options popup as a persistent SDF component editor.
    public class SdfEditorPopup : MonoBehaviour
    {
        private readonly Dictionary<SketchControlsScript.GlobalCommands, OptionButton> m_Buttons =
            new Dictionary<SketchControlsScript.GlobalCommands, OptionButton>();

        private SdfStencil m_Stencil;
        private PopUpWindow m_Popup;
        private int m_ComponentIndex;

        internal static SdfEditorPopup Active { get; private set; }

        internal void Initialize(SdfStencil stencil)
        {
            m_Stencil = stencil != null
                ? stencil
                : throw new ArgumentNullException(nameof(stencil));
            m_Popup = GetComponent<PopUpWindow>();
            if (m_Popup == null)
            {
                throw new InvalidOperationException("The SDF editor requires a popup window.");
            }

            m_Popup.SetPersistent(true);
            ConfigureButtons();
            Active = this;
            App.Switchboard.SelectionChanged += OnSelectionChanged;
            Refresh();
        }

        internal bool CanHandle(SketchControlsScript.GlobalCommands command)
        {
            int count = m_Stencil != null ? m_Stencil.ComponentCount : 0;
            switch (command)
            {
                case SketchControlsScript.GlobalCommands.SdfPreviousComponent:
                    return count > 1 && m_ComponentIndex > 0;
                case SketchControlsScript.GlobalCommands.SdfNextComponent:
                    return count > 1 && m_ComponentIndex < count - 1;
                case SketchControlsScript.GlobalCommands.SdfMoveComponentUp:
                    return count > 1 && m_ComponentIndex > 0;
                case SketchControlsScript.GlobalCommands.SdfMoveComponentDown:
                    return count > 1 && m_ComponentIndex < count - 1;
                case SketchControlsScript.GlobalCommands.SdfCycleComponentOperation:
                    return count > 1 && m_ComponentIndex > 0;
                case SketchControlsScript.GlobalCommands.SdfRemoveComponent:
                    return count > 0;
                default:
                    return false;
            }
        }

        internal void Handle(SketchControlsScript.GlobalCommands command)
        {
            if (!CanHandle(command))
            {
                return;
            }

            switch (command)
            {
                case SketchControlsScript.GlobalCommands.SdfPreviousComponent:
                    --m_ComponentIndex;
                    break;
                case SketchControlsScript.GlobalCommands.SdfNextComponent:
                    ++m_ComponentIndex;
                    break;
                case SketchControlsScript.GlobalCommands.SdfMoveComponentUp:
                    Perform(EditSdfGuideCommand.MoveComponent(
                        m_Stencil, m_ComponentIndex, m_ComponentIndex - 1));
                    --m_ComponentIndex;
                    break;
                case SketchControlsScript.GlobalCommands.SdfMoveComponentDown:
                    Perform(EditSdfGuideCommand.MoveComponent(
                        m_Stencil, m_ComponentIndex, m_ComponentIndex + 1));
                    ++m_ComponentIndex;
                    break;
                case SketchControlsScript.GlobalCommands.SdfCycleComponentOperation:
                    CycleOperation();
                    break;
                case SketchControlsScript.GlobalCommands.SdfRemoveComponent:
                    Perform(EditSdfGuideCommand.RemoveComponent(
                        m_Stencil, m_ComponentIndex));
                    m_ComponentIndex = Mathf.Min(
                        m_ComponentIndex, m_Stencil.ComponentCount - 1);
                    break;
            }
            Refresh();
        }

        private void ConfigureButtons()
        {
            Dictionary<string, OptionButton> buttons =
                GetComponentsInChildren<OptionButton>(true)
                    .ToDictionary(button => button.gameObject.name);

            Configure(
                buttons["FlipSelection"],
                SketchControlsScript.GlobalCommands.SdfPreviousComponent,
                "Icons/backwardarrow", "Previous component");
            Configure(
                buttons["SelectAll"],
                SketchControlsScript.GlobalCommands.SdfNextComponent,
                "Icons/forwardarrow", "Next component");
            Configure(
                buttons["Ungroup"],
                SketchControlsScript.GlobalCommands.SdfMoveComponentUp,
                "Icons/uparrow", "Move component up");
            Configure(
                buttons["Group"],
                SketchControlsScript.GlobalCommands.SdfMoveComponentDown,
                "Icons/downarrow", "Move component down");
            Configure(
                buttons["InvertSelection"],
                SketchControlsScript.GlobalCommands.SdfCycleComponentOperation,
                "Icons/edit", "Change operation");

            GameObject removeObject = Instantiate(
                buttons["FlipSelection"].gameObject,
                buttons["FlipSelection"].transform.parent);
            removeObject.name = "RemoveComponent";
            removeObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            OptionButton removeButton = removeObject.GetComponent<OptionButton>();
            removeButton.RegisterComponent();
            Configure(
                removeButton,
                SketchControlsScript.GlobalCommands.SdfRemoveComponent,
                "Icons/Knot_Delete", "Remove component");
        }

        private void Configure(
            OptionButton button, SketchControlsScript.GlobalCommands command,
            string resourcePath, string description)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            button.SetContextCommand(command, texture ?? button.ButtonTexture, description);
            m_Buttons[command] = button;
        }

        private void CycleOperation()
        {
            SdfStencil.ComponentDefinition component =
                m_Stencil.GetComponentDefinitions()[m_ComponentIndex];
            SDFCombineType operation;
            switch (component.Operation)
            {
                case SDFCombineType.SmoothUnion:
                    operation = SDFCombineType.SmoothSubtract;
                    break;
                case SDFCombineType.SmoothSubtract:
                    operation = SDFCombineType.SmoothIntersect;
                    break;
                default:
                    operation = SDFCombineType.SmoothUnion;
                    break;
            }
            Perform(EditSdfGuideCommand.SetComponentOperation(
                m_Stencil, m_ComponentIndex, operation));
        }

        private static void Perform(BaseCommand command)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
        }

        private void Refresh()
        {
            int count = m_Stencil != null ? m_Stencil.ComponentCount : 0;
            m_ComponentIndex = count == 0
                ? -1
                : Mathf.Clamp(m_ComponentIndex, 0, count - 1);

            string label = "SDF Components\nEmpty";
            if (m_ComponentIndex >= 0)
            {
                SdfStencil.ComponentDefinition component =
                    m_Stencil.GetComponentDefinitions()[m_ComponentIndex];
                string type = component.IsPrimitive
                    ? SdfStencil.PrimitiveTypeName(component.Primitive.Value.Type)
                    : "mesh";
                label = $"SDF Component {m_ComponentIndex + 1}/{count}\n" +
                    $"{type} · {SdfStencil.OperationName(component.Operation)}";
            }
            m_Popup.SetWindowText(label);

            foreach (var pair in m_Buttons)
            {
                pair.Value.SetButtonAvailable(CanHandle(pair.Key));
            }
        }

        private void OnSelectionChanged()
        {
            if (SelectionManager.m_Instance.SelectedSdfGuide != m_Stencil)
            {
                m_Popup.GetParentPanel()?.CloseActivePopUp(true);
            }
        }

        private void OnDestroy()
        {
            App.Switchboard.SelectionChanged -= OnSelectionChanged;
            if (Active == this)
            {
                Active = null;
            }
        }
    }
}
