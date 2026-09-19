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
        private const float k_DimensionStep = 0.025f;
        private const float k_MinimumDimension = 0.005f;
        private const float k_BlendStep = 0.025f;

        private readonly List<OptionButton> m_Buttons = new List<OptionButton>();
        private readonly List<SdfComponentHandle> m_ComponentHandles =
            new List<SdfComponentHandle>();

        private SdfStencil m_Stencil;
        private PopUpWindow m_Popup;
        private int m_ComponentIndex;
        private int m_DimensionIndex;
        private int m_Page;
        private bool m_EditComponentHandles;

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
                case SketchControlsScript.GlobalCommands.SdfNextPrimitiveDimension:
                    return SelectedPrimitive.HasValue && DimensionCount > 1;
                case SketchControlsScript.GlobalCommands.SdfAdjustPrimitiveDimension:
                    if (!SelectedPrimitive.HasValue ||
                        (commandParam != -1 && commandParam != 1))
                    {
                        return false;
                    }
                    return commandParam > 0 ||
                        SelectedPrimitive.Value.Geometry[m_DimensionIndex] > k_MinimumDimension;
                case SketchControlsScript.GlobalCommands.SdfAdjustComponentBlend:
                    if (count <= 1 || m_ComponentIndex <= 0 ||
                        (commandParam != -1 && commandParam != 1))
                    {
                        return false;
                    }
                    return commandParam > 0 ||
                        m_Stencil.GetComponentDefinitions()[m_ComponentIndex].Blend > 0f;
                case SketchControlsScript.GlobalCommands.SdfToggleComponentHandles:
                    return count > 0;
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
                    m_DimensionIndex = 0;
                    break;
                case SketchControlsScript.GlobalCommands.SdfNextComponent:
                    ++m_ComponentIndex;
                    m_DimensionIndex = 0;
                    break;
                case SketchControlsScript.GlobalCommands.SdfMoveComponentUp:
                    Perform(EditSdfGuideCommand.MoveComponent(
                        m_Stencil, m_ComponentIndex, m_ComponentIndex - 1));
                    --m_ComponentIndex;
                    m_DimensionIndex = 0;
                    break;
                case SketchControlsScript.GlobalCommands.SdfMoveComponentDown:
                    Perform(EditSdfGuideCommand.MoveComponent(
                        m_Stencil, m_ComponentIndex, m_ComponentIndex + 1));
                    ++m_ComponentIndex;
                    m_DimensionIndex = 0;
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
                    m_Page = (m_Page + 1) % 5;
                    ConfigurePage();
                    break;
                case SketchControlsScript.GlobalCommands.SdfAddPrimitive:
                    AddPrimitive((SDFPrimitiveType)commandParam);
                    break;
                case SketchControlsScript.GlobalCommands.SdfNextPrimitiveDimension:
                    m_DimensionIndex = (m_DimensionIndex + 1) % DimensionCount;
                    ConfigurePage();
                    break;
                case SketchControlsScript.GlobalCommands.SdfAdjustPrimitiveDimension:
                    AdjustPrimitiveDimension(commandParam);
                    break;
                case SketchControlsScript.GlobalCommands.SdfAdjustComponentBlend:
                    AdjustBlend(commandParam);
                    break;
                case SketchControlsScript.GlobalCommands.SdfToggleComponentHandles:
                    m_EditComponentHandles = !m_EditComponentHandles;
                    ConfigurePage();
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
            button.gameObject.SetActive(true);
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            button.SetContextCommand(command, texture ?? button.ButtonTexture, description);
            button.SetCommandParameters(commandParam);
        }

        private static void Disable(OptionButton button)
        {
            button.gameObject.SetActive(false);
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
                        "Icons/rebrush", "Change operation");
                    Configure(m_Buttons[5], SketchControlsScript.GlobalCommands.SdfEditorNextPage,
                        "Icons/forwardarrow", "Edit component values");
                    break;
                case 1:
                    Configure(m_Buttons[0],
                        SketchControlsScript.GlobalCommands.SdfNextPrimitiveDimension,
                        "Icons/settings", $"Next dimension ({DimensionName})");
                    Configure(m_Buttons[1],
                        SketchControlsScript.GlobalCommands.SdfAdjustPrimitiveDimension,
                        "Icons/IcosaCategories/minus-solid", $"Decrease {DimensionName}", -1);
                    Configure(m_Buttons[2],
                        SketchControlsScript.GlobalCommands.SdfAdjustPrimitiveDimension,
                        "Icons/additive", $"Increase {DimensionName}", 1);
                    Configure(m_Buttons[3],
                        SketchControlsScript.GlobalCommands.SdfAdjustComponentBlend,
                        "Icons/IcosaCategories/minus-solid", "Decrease blend", -1);
                    Configure(m_Buttons[4],
                        SketchControlsScript.GlobalCommands.SdfAdjustComponentBlend,
                        "Icons/additive", "Increase blend", 1);
                    Configure(m_Buttons[5], SketchControlsScript.GlobalCommands.SdfEditorNextPage,
                        "Icons/forwardarrow", "Component tools");
                    break;
                case 2:
                    Configure(m_Buttons[0],
                        SketchControlsScript.GlobalCommands.SdfToggleComponentHandles,
                        "Icons/selection",
                        m_EditComponentHandles ? "Hide component handles" : "Edit components");
                    Configure(m_Buttons[1], SketchControlsScript.GlobalCommands.SdfRemoveComponent,
                        "Icons/Knot_Delete", "Remove component");
                    Disable(m_Buttons[2]);
                    Disable(m_Buttons[3]);
                    Disable(m_Buttons[4]);
                    Configure(m_Buttons[5], SketchControlsScript.GlobalCommands.SdfEditorNextPage,
                        "Icons/forwardarrow", "Add components");
                    break;
                case 3:
                    ConfigureAddButton(0, SDFPrimitiveType.Sphere, "Icons/guide_sphere");
                    ConfigureAddButton(1, SDFPrimitiveType.Torus, "Icons/settings");
                    ConfigureAddButton(2, SDFPrimitiveType.Cuboid, "Icons/guide_cube");
                    ConfigureAddButton(3, SDFPrimitiveType.BoxFrame, "Icons/settings");
                    ConfigureAddButton(4, SDFPrimitiveType.Cylinder, "Icons/settings");
                    Configure(m_Buttons[5], SketchControlsScript.GlobalCommands.SdfEditorNextPage,
                        "Icons/forwardarrow", "More component types");
                    break;
                default:
                    ConfigureAddButton(0, SDFPrimitiveType.Capsule, "Icons/guide_capsule");
                    ConfigureAddButton(1, SDFPrimitiveType.Ellipsoid, "Icons/guide_ellipsoid");
                    ConfigureAddButton(2, SDFPrimitiveType.Cone, "Icons/settings");
                    ConfigureAddButton(3, SDFPrimitiveType.Pyramid, "Icons/settings");
                    Disable(m_Buttons[4]);
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
            m_DimensionIndex = 0;
            m_Page = 0;
            ConfigurePage();
        }

        private SdfStencil.PrimitiveDefinition? SelectedPrimitive
        {
            get
            {
                if (m_Stencil == null || m_ComponentIndex < 0 ||
                    m_ComponentIndex >= m_Stencil.ComponentCount)
                {
                    return null;
                }
                return m_Stencil.GetComponentDefinitions()[m_ComponentIndex].Primitive;
            }
        }

        private int DimensionCount
        {
            get
            {
                SdfStencil.PrimitiveDefinition? primitive = SelectedPrimitive;
                if (!primitive.HasValue)
                {
                    return 0;
                }
                return DimensionCountForPrimitive(primitive.Value.Type);
            }
        }

        private static int DimensionCountForPrimitive(SDFPrimitiveType type)
        {
            switch (type)
            {
                case SDFPrimitiveType.Sphere: return 1;
                case SDFPrimitiveType.Torus:
                case SDFPrimitiveType.Cylinder:
                case SDFPrimitiveType.Capsule:
                case SDFPrimitiveType.Cone:
                case SDFPrimitiveType.Pyramid:
                    return 2;
                case SDFPrimitiveType.Cuboid:
                case SDFPrimitiveType.Ellipsoid:
                    return 3;
                case SDFPrimitiveType.BoxFrame: return 4;
                default: return 0;
            }
        }

        private static Vector3 DimensionAxis(int dimensionIndex)
        {
            switch (dimensionIndex)
            {
                case 0: return Vector3.right;
                case 1: return Vector3.up;
                case 2: return Vector3.forward;
                default: return new Vector3(1f, 1f, 1f).normalized;
            }
        }

        private string DimensionName
        {
            get
            {
                SdfStencil.PrimitiveDefinition? primitive = SelectedPrimitive;
                if (!primitive.HasValue)
                {
                    return "dimension";
                }
                string[][] names =
                {
                    new[] { "radius" },
                    new[] { "major radius", "minor radius" },
                    new[] { "width", "height", "depth" },
                    new[] { "width", "height", "depth", "thickness" },
                    new[] { "radius", "half length" },
                    new[] { "radius", "half segment" },
                    new[] { "x radius", "y radius", "z radius" },
                    new[] { "radius", "half height" },
                    new[] { "half width", "half height" },
                };
                return names[(int)primitive.Value.Type][m_DimensionIndex];
            }
        }

        private void AdjustPrimitiveDimension(int direction)
        {
            SdfStencil.PrimitiveDefinition primitive = SelectedPrimitive.Value;
            Vector4 geometry = primitive.Geometry;
            geometry[m_DimensionIndex] = Mathf.Max(
                k_MinimumDimension,
                geometry[m_DimensionIndex] + direction * k_DimensionStep);
            Perform(EditSdfGuideCommand.SetPrimitiveGeometry(
                m_Stencil, m_ComponentIndex, primitive.Type, geometry));
        }

        private void AdjustBlend(int direction)
        {
            SdfStencil.ComponentDefinition component =
                m_Stencil.GetComponentDefinitions()[m_ComponentIndex];
            Perform(EditSdfGuideCommand.SetComponentBlend(
                m_Stencil, m_ComponentIndex,
                Mathf.Max(0f, component.Blend + direction * k_BlendStep)));
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

        internal TrTransform BeginHandleTransform(SdfComponentHandle handle)
        {
            m_ComponentIndex = handle.ComponentIndex;
            m_DimensionIndex = 0;
            Refresh();
            return m_Stencil.GetComponentDefinitions()[m_ComponentIndex].Transform;
        }

        internal Vector4 BeginHandleResize(SdfComponentHandle handle)
        {
            m_ComponentIndex = handle.ComponentIndex;
            m_DimensionIndex = handle.DimensionIndex;
            Refresh();
            return m_Stencil.GetComponentDefinitions()[m_ComponentIndex]
                .Primitive.Value.Geometry;
        }

        internal void PreviewHandleTransform(SdfComponentHandle handle)
        {
            if (!m_EditComponentHandles || handle.ComponentIndex < 0 ||
                handle.ComponentIndex >= m_Stencil.ComponentCount)
            {
                return;
            }

            TrTransform transform = ComponentTransformFromHandle(handle);
            TrTransform current =
                m_Stencil.GetComponentDefinitions()[handle.ComponentIndex].Transform;
            if (!TrTransform.Approximately(current, transform))
            {
                m_Stencil.SetComponentTransform(handle.ComponentIndex, transform);
            }
        }

        internal void CommitHandleTransform(
            SdfComponentHandle handle, TrTransform dragStart)
        {
            if (!m_EditComponentHandles || handle.ComponentIndex < 0 ||
                handle.ComponentIndex >= m_Stencil.ComponentCount)
            {
                return;
            }

            PreviewHandleTransform(handle);
            TrTransform dragEnd =
                m_Stencil.GetComponentDefinitions()[handle.ComponentIndex].Transform;
            if (!TrTransform.Approximately(dragStart, dragEnd))
            {
                m_Stencil.SetComponentTransform(handle.ComponentIndex, dragStart);
                Perform(EditSdfGuideCommand.SetComponentTransform(
                    m_Stencil, handle.ComponentIndex, dragEnd));
            }
            Refresh();
        }

        internal void PreviewHandleResize(SdfComponentHandle handle)
        {
            if (!m_EditComponentHandles || !handle.IsDimensionHandle ||
                handle.ComponentIndex < 0 ||
                handle.ComponentIndex >= m_Stencil.ComponentCount)
            {
                return;
            }

            SdfStencil.ComponentDefinition component =
                m_Stencil.GetComponentDefinitions()[handle.ComponentIndex];
            if (!component.IsPrimitive)
            {
                return;
            }

            TrTransform componentPose_GS =
                TrTransform.FromTransform(m_Stencil.transform) * component.Transform;
            Vector3 axis_GS = componentPose_GS.rotation * handle.DimensionAxis;
            float dimension = Vector3.Dot(
                handle.transform.position - componentPose_GS.translation,
                axis_GS.normalized) / Mathf.Max(k_MinimumDimension, componentPose_GS.scale);
            Vector4 geometry = component.Primitive.Value.Geometry;
            geometry[handle.DimensionIndex] = Mathf.Max(k_MinimumDimension, dimension);
            if (!Mathf.Approximately(
                component.Primitive.Value.Geometry[handle.DimensionIndex],
                geometry[handle.DimensionIndex]))
            {
                m_Stencil.SetComponentPrimitiveGeometry(handle.ComponentIndex, geometry);
            }
        }

        internal void CommitHandleResize(
            SdfComponentHandle handle, Vector4 dragStart)
        {
            if (!m_EditComponentHandles || !handle.IsDimensionHandle ||
                handle.ComponentIndex < 0 ||
                handle.ComponentIndex >= m_Stencil.ComponentCount)
            {
                return;
            }

            PreviewHandleResize(handle);
            SdfStencil.PrimitiveDefinition primitive =
                m_Stencil.GetComponentDefinitions()[handle.ComponentIndex].Primitive.Value;
            Vector4 dragEnd = primitive.Geometry;
            if (dragStart != dragEnd)
            {
                m_Stencil.SetComponentPrimitiveGeometry(handle.ComponentIndex, dragStart);
                Perform(EditSdfGuideCommand.SetPrimitiveGeometry(
                    m_Stencil, handle.ComponentIndex, primitive.Type, dragEnd));
            }
            Refresh();
        }

        private TrTransform ComponentTransformFromHandle(SdfComponentHandle handle)
        {
            TrTransform sdfPose_GS = TrTransform.FromTransform(m_Stencil.transform);
            float componentScale =
                m_Stencil.GetComponentDefinitions()[handle.ComponentIndex].Transform.scale;
            TrTransform handlePose_GS = TrTransform.TRS(
                handle.transform.position,
                handle.transform.rotation,
                sdfPose_GS.scale * componentScale);
            return sdfPose_GS.inverse * handlePose_GS;
        }

        private void RefreshComponentHandles()
        {
            if (!m_EditComponentHandles || m_Stencil == null)
            {
                DestroyComponentHandles();
                return;
            }

            IReadOnlyList<SdfStencil.ComponentDefinition> components =
                m_Stencil.GetComponentDefinitions();
            int expectedHandleCount = components.Sum(component =>
                1 + (component.IsPrimitive
                    ? DimensionCountForPrimitive(component.Primitive.Value.Type)
                    : 0));
            if (m_ComponentHandles.Count != expectedHandleCount)
            {
                DestroyComponentHandles();
                Renderer sourceRenderer =
                    m_Stencil.GetComponentsInChildren<Renderer>(true).FirstOrDefault();
                Material sourceMaterial = sourceRenderer != null
                    ? sourceRenderer.sharedMaterial
                    : null;
                TrTransform sdfPose_GS = TrTransform.FromTransform(m_Stencil.transform);
                for (int i = 0; i < components.Count; ++i)
                {
                    TrTransform componentPose_GS = sdfPose_GS * components[i].Transform;
                    m_ComponentHandles.Add(SdfComponentHandle.Create(
                        this, i, componentPose_GS, sourceMaterial));
                    if (!components[i].IsPrimitive)
                    {
                        continue;
                    }
                    SdfStencil.PrimitiveDefinition primitive = components[i].Primitive.Value;
                    int dimensionCount = DimensionCountForPrimitive(primitive.Type);
                    for (int dimension = 0; dimension < dimensionCount; ++dimension)
                    {
                        Vector3 axis = DimensionAxis(dimension);
                        TrTransform dimensionPose_GS = TrTransform.TRS(
                            componentPose_GS * (axis * primitive.Geometry[dimension]),
                            componentPose_GS.rotation, 1f);
                        m_ComponentHandles.Add(SdfComponentHandle.CreateDimension(
                            this, i, dimension, axis, dimensionPose_GS, sourceMaterial));
                    }
                }
            }

            TrTransform pose_GS = TrTransform.FromTransform(m_Stencil.transform);
            int handleIndex = 0;
            for (int i = 0; i < components.Count; ++i)
            {
                TrTransform componentPose_GS = pose_GS * components[i].Transform;
                SdfComponentHandle handle = m_ComponentHandles[handleIndex++];
                handle.SetComponentIndex(i);
                handle.SetPose(componentPose_GS);
                handle.SetSelected(i == m_ComponentIndex);
                if (!components[i].IsPrimitive)
                {
                    continue;
                }
                SdfStencil.PrimitiveDefinition primitive = components[i].Primitive.Value;
                int dimensionCount = DimensionCountForPrimitive(primitive.Type);
                for (int dimension = 0; dimension < dimensionCount; ++dimension)
                {
                    SdfComponentHandle dimensionHandle = m_ComponentHandles[handleIndex++];
                    Vector3 axis = DimensionAxis(dimension);
                    dimensionHandle.SetComponentIndex(i);
                    dimensionHandle.SetPose(TrTransform.TRS(
                        componentPose_GS * (axis * primitive.Geometry[dimension]),
                        componentPose_GS.rotation, 1f));
                    dimensionHandle.SetSelected(
                        i == m_ComponentIndex && dimension == m_DimensionIndex);
                }
            }
        }

        private void DestroyComponentHandles()
        {
            foreach (SdfComponentHandle handle in m_ComponentHandles)
            {
                if (handle != null)
                {
                    handle.DisposeHandle();
                }
            }
            m_ComponentHandles.Clear();
        }

        private void Refresh()
        {
            int count = m_Stencil != null ? m_Stencil.ComponentCount : 0;
            m_ComponentIndex = count == 0
                ? -1
                : Mathf.Clamp(m_ComponentIndex, 0, count - 1);
            m_DimensionIndex = Mathf.Clamp(
                m_DimensionIndex, 0, Mathf.Max(0, DimensionCount - 1));

            string label = "SDF Components\nEmpty";
            if (m_ComponentIndex >= 0)
            {
                SdfStencil.ComponentDefinition component =
                    m_Stencil.GetComponentDefinitions()[m_ComponentIndex];
                string type = component.IsPrimitive
                    ? SdfStencil.PrimitiveTypeName(component.Primitive.Value.Type)
                    : "mesh";
                string dimension = component.IsPrimitive
                    ? $" · {DimensionName} {component.Primitive.Value.Geometry[m_DimensionIndex]:0.###}"
                    : string.Empty;
                label = $"SDF Component {m_ComponentIndex + 1}/{count}\n" +
                    $"{type} · {SdfStencil.OperationName(component.Operation)}" +
                    $"{dimension} · blend {component.Blend:0.###}";
            }
            m_Popup.SetWindowText(label);

            if (m_Page == 1 || m_Page == 2)
            {
                ConfigurePage();
            }

            RefreshComponentHandles();

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
            DestroyComponentHandles();
            if (Active == this)
            {
                Active = null;
            }
        }
    }
}
