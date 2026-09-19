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
        private readonly List<OptionButton> m_Buttons = new List<OptionButton>();

        private SdfStencil m_Stencil;
        private PopUpWindow m_Popup;
        private int m_ComponentIndex;
        private int m_Page;

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

        internal bool CanHandle(
            SketchControlsScript.GlobalCommands command, int commandParam = -1)
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
                case SketchControlsScript.GlobalCommands.SdfEditorNextPage:
                    return true;
                case SketchControlsScript.GlobalCommands.SdfAddPrimitive:
                    return Enum.IsDefined(typeof(SDFPrimitiveType), commandParam);
                default:
                    return false;
            }
        }

        internal void Handle(
            SketchControlsScript.GlobalCommands command, int commandParam = -1)
        {
            if (!CanHandle(command, commandParam))
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
                case SketchControlsScript.GlobalCommands.SdfEditorNextPage:
                    m_Page = (m_Page + 1) % 3;
                    ConfigurePage();
                    break;
                case SketchControlsScript.GlobalCommands.SdfAddPrimitive:
                    AddPrimitive((SDFPrimitiveType)commandParam);
                    break;
            }
            Refresh();
        }

        private void ConfigureButtons()
        {
            Dictionary<string, OptionButton> buttons =
                GetComponentsInChildren<OptionButton>(true)
                    .ToDictionary(button => button.gameObject.name);

            GameObject removeObject = Instantiate(
                buttons["FlipSelection"].gameObject,
                buttons["FlipSelection"].transform.parent);
            removeObject.name = "RemoveComponent";
            removeObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            OptionButton removeButton = removeObject.GetComponent<OptionButton>();
            removeButton.RegisterComponent();

            m_Buttons.Add(buttons["FlipSelection"]);
            m_Buttons.Add(buttons["SelectAll"]);
            m_Buttons.Add(buttons["Ungroup"]);
            m_Buttons.Add(buttons["Group"]);
            m_Buttons.Add(buttons["InvertSelection"]);
            m_Buttons.Add(removeButton);
            ConfigurePage();
        }

        private void Configure(
            OptionButton button, SketchControlsScript.GlobalCommands command,
            string resourcePath, string description, int commandParam = -1)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            button.SetContextCommand(command, texture ?? button.ButtonTexture, description);
            button.SetCommandParameters(commandParam);
        }

        private void ConfigurePage()
        {
            switch (m_Page)
            {
                case 0:
                    Configure(m_Buttons[0], SketchControlsScript.GlobalCommands.SdfPreviousComponent,
                        "Icons/backwardarrow", "Previous component");
                    Configure(m_Buttons[1], SketchControlsScript.GlobalCommands.SdfNextComponent,
                        "Icons/forwardarrow", "Next component");
                    Configure(m_Buttons[2], SketchControlsScript.GlobalCommands.SdfMoveComponentUp,
                        "Icons/uparrow", "Move component up");
                    Configure(m_Buttons[3], SketchControlsScript.GlobalCommands.SdfMoveComponentDown,
                        "Icons/downarrow", "Move component down");
                    Configure(m_Buttons[4],
                        SketchControlsScript.GlobalCommands.SdfCycleComponentOperation,
                        "Icons/edit", "Change operation");
                    Configure(m_Buttons[5], SketchControlsScript.GlobalCommands.SdfEditorNextPage,
                        "Icons/forwardarrow", "Add components");
                    break;
                case 1:
                    ConfigureAddButton(0, SDFPrimitiveType.Sphere, "Icons/guide_sphere");
                    ConfigureAddButton(1, SDFPrimitiveType.Torus, "Icons/guides_settings");
                    ConfigureAddButton(2, SDFPrimitiveType.Cuboid, "Icons/guide_cube");
                    ConfigureAddButton(3, SDFPrimitiveType.BoxFrame, "Icons/guides_settings");
                    ConfigureAddButton(4, SDFPrimitiveType.Cylinder, "Icons/guides_settings");
                    Configure(m_Buttons[5], SketchControlsScript.GlobalCommands.SdfEditorNextPage,
                        "Icons/forwardarrow", "More component types");
                    break;
                default:
                    ConfigureAddButton(0, SDFPrimitiveType.Capsule, "Icons/guide_capsule");
                    ConfigureAddButton(1, SDFPrimitiveType.Ellipsoid, "Icons/guide_ellipsoid");
                    ConfigureAddButton(2, SDFPrimitiveType.Cone, "Icons/guides_settings");
                    ConfigureAddButton(3, SDFPrimitiveType.Pyramid, "Icons/guides_settings");
                    Configure(m_Buttons[4], SketchControlsScript.GlobalCommands.SdfRemoveComponent,
                        "Icons/Knot_Delete", "Remove component");
                    Configure(m_Buttons[5], SketchControlsScript.GlobalCommands.SdfEditorNextPage,
                        "Icons/backwardarrow", "Component controls");
                    break;
            }
        }

        private void ConfigureAddButton(
            int buttonIndex, SDFPrimitiveType type, string resourcePath)
        {
            Configure(
                m_Buttons[buttonIndex],
                SketchControlsScript.GlobalCommands.SdfAddPrimitive,
                resourcePath,
                $"Add {SdfStencil.PrimitiveTypeName(type)}",
                (int)type);
        }

        private void AddPrimitive(SDFPrimitiveType type)
        {
            Vector4 geometry;
            switch (type)
            {
                case SDFPrimitiveType.Sphere:
                    geometry = new Vector4(0.25f, 0f, 0f, 0f);
                    break;
                case SDFPrimitiveType.Torus:
                    geometry = new Vector4(0.25f, 0.08f, 0f, 0f);
                    break;
                case SDFPrimitiveType.Cuboid:
                    geometry = new Vector4(0.25f, 0.25f, 0.25f, 0f);
                    break;
                case SDFPrimitiveType.BoxFrame:
                    geometry = new Vector4(0.25f, 0.25f, 0.25f, 0.05f);
                    break;
                case SDFPrimitiveType.Cylinder:
                case SDFPrimitiveType.Cone:
                case SDFPrimitiveType.Pyramid:
                    geometry = new Vector4(0.25f, 0.25f, 0f, 0f);
                    break;
                case SDFPrimitiveType.Capsule:
                    geometry = new Vector4(0.15f, 0.25f, 0f, 0f);
                    break;
                case SDFPrimitiveType.Ellipsoid:
                    geometry = new Vector4(0.25f, 0.2f, 0.15f, 0f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            Perform(EditSdfGuideCommand.AddPrimitive(
                m_Stencil, type, geometry, TrTransform.identity));
            m_ComponentIndex = m_Stencil.ComponentCount - 1;
            m_Page = 0;
            ConfigurePage();
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

            foreach (OptionButton button in m_Buttons)
            {
                button.SetButtonAvailable(CanHandle(
                    button.m_Command, button.m_CommandParam));
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
