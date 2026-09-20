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
using IsoMesh;

namespace TiltBrush
{
    /// Applies an ordered SDF component edit as one undoable operation.
    internal sealed class EditSdfGuideCommand : BaseCommand
    {
        private readonly SdfStencil m_Stencil;
        private readonly IReadOnlyList<SdfStencil.ComponentDefinition> m_Before;
        private readonly IReadOnlyList<SdfStencil.ComponentDefinition> m_After;

        internal EditSdfGuideCommand(
            SdfStencil stencil,
            IReadOnlyList<SdfStencil.ComponentDefinition> after,
            BaseCommand parent = null) : base(parent)
        {
            m_Stencil = stencil != null
                ? stencil
                : throw new ArgumentNullException(nameof(stencil));
            m_Before = new List<SdfStencil.ComponentDefinition>(
                stencil.GetComponentDefinitions());
            m_After = new List<SdfStencil.ComponentDefinition>(
                after ?? throw new ArgumentNullException(nameof(after)));
            SdfStencil.ValidateComponentDefinitions(m_After);
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            m_Stencil.ReplaceComponents(m_After);
        }

        protected override void OnUndo()
        {
            m_Stencil.ReplaceComponents(m_Before);
        }

        internal static EditSdfGuideCommand AddPrimitive(
            SdfStencil stencil, SDFPrimitiveType type, UnityEngine.Vector4 geometry,
            TrTransform transform, int index = -1)
        {
            var components = Snapshot(stencil);
            int insertionIndex = index < 0 ? components.Count : index;
            if (insertionIndex < 0 || insertionIndex > components.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            components.Insert(insertionIndex, new SdfStencil.ComponentDefinition(
                new SdfStencil.PrimitiveDefinition(
                    type, geometry, transform, SDFCombineType.SmoothUnion, 0f, false)));
            NormalizeFirstOperation(components);
            return new EditSdfGuideCommand(stencil, components);
        }

        internal static EditSdfGuideCommand RemoveComponent(SdfStencil stencil, int index)
        {
            var components = Snapshot(stencil);
            ValidateIndex(components, index);
            components.RemoveAt(index);
            NormalizeFirstOperation(components);
            return new EditSdfGuideCommand(stencil, components);
        }

        internal static EditSdfGuideCommand DuplicateComponent(
            SdfStencil stencil, int index)
        {
            var components = Snapshot(stencil);
            ValidateIndex(components, index);
            components.Insert(index + 1, Copy(components[index]));
            return new EditSdfGuideCommand(stencil, components);
        }

        internal static EditSdfGuideCommand MoveComponent(
            SdfStencil stencil, int fromIndex, int toIndex)
        {
            var components = Snapshot(stencil);
            ValidateIndex(components, fromIndex);
            ValidateIndex(components, toIndex);
            SdfStencil.ComponentDefinition component = components[fromIndex];
            components.RemoveAt(fromIndex);
            components.Insert(toIndex, component);
            NormalizeFirstOperation(components);
            return new EditSdfGuideCommand(stencil, components);
        }

        internal static EditSdfGuideCommand SetComponentOperation(
            SdfStencil stencil, int index, SDFCombineType operation)
        {
            var components = Snapshot(stencil);
            ValidateIndex(components, index);
            if (index == 0 && operation != SDFCombineType.SmoothUnion)
            {
                throw new ArgumentException(
                    "The first SDF component must use the union operation.",
                    nameof(operation));
            }
            components[index] = Copy(components[index], operation: operation);
            return new EditSdfGuideCommand(stencil, components);
        }

        internal static EditSdfGuideCommand SetComponentBlend(
            SdfStencil stencil, int index, float blend)
        {
            var components = Snapshot(stencil);
            ValidateIndex(components, index);
            components[index] = Copy(components[index], blend: blend);
            return new EditSdfGuideCommand(stencil, components);
        }

        internal static EditSdfGuideCommand SetComponentTransform(
            SdfStencil stencil, int index, TrTransform transform)
        {
            var components = Snapshot(stencil);
            ValidateIndex(components, index);
            components[index] = Copy(components[index], transform: transform);
            return new EditSdfGuideCommand(stencil, components);
        }

        internal static EditSdfGuideCommand SetComponentFlip(
            SdfStencil stencil, int index, bool flip)
        {
            var components = Snapshot(stencil);
            ValidateIndex(components, index);
            components[index] = Copy(components[index], flip: flip);
            return new EditSdfGuideCommand(stencil, components);
        }

        internal static EditSdfGuideCommand SetPrimitiveGeometry(
            SdfStencil stencil, int index, SDFPrimitiveType type,
            UnityEngine.Vector4 geometry)
        {
            var components = Snapshot(stencil);
            ValidateIndex(components, index);
            SdfStencil.ComponentDefinition component = components[index];
            if (!component.IsPrimitive)
            {
                throw new ArgumentException($"SDF component {index} is a mesh operand.");
            }
            SdfStencil.PrimitiveDefinition primitive = component.Primitive.Value;
            components[index] = new SdfStencil.ComponentDefinition(
                new SdfStencil.PrimitiveDefinition(
                    type, geometry, primitive.Transform, primitive.Operation,
                    primitive.Blend, primitive.Flip));
            return new EditSdfGuideCommand(stencil, components);
        }

        private static List<SdfStencil.ComponentDefinition> Snapshot(SdfStencil stencil)
        {
            if (stencil == null)
            {
                throw new ArgumentNullException(nameof(stencil));
            }
            return new List<SdfStencil.ComponentDefinition>(stencil.GetComponentDefinitions());
        }

        private static SdfStencil.ComponentDefinition Copy(
            SdfStencil.ComponentDefinition component,
            TrTransform? transform = null,
            SDFCombineType? operation = null,
            float? blend = null,
            bool? flip = null)
        {
            TrTransform updatedTransform = transform ?? component.Transform;
            SDFCombineType updatedOperation = operation ?? component.Operation;
            float updatedBlend = blend ?? component.Blend;
            bool updatedFlip = flip ?? component.Flip;
            if (!component.IsPrimitive)
            {
                return new SdfStencil.ComponentDefinition(
                    component.MeshAsset, updatedTransform, updatedOperation,
                    updatedBlend, updatedFlip);
            }

            SdfStencil.PrimitiveDefinition primitive = component.Primitive.Value;
            return new SdfStencil.ComponentDefinition(new SdfStencil.PrimitiveDefinition(
                primitive.Type, primitive.Geometry, updatedTransform, updatedOperation,
                updatedBlend, updatedFlip));
        }

        private static void NormalizeFirstOperation(
            List<SdfStencil.ComponentDefinition> components)
        {
            if (components.Count > 0 &&
                components[0].Operation != SDFCombineType.SmoothUnion)
            {
                components[0] = Copy(
                    components[0], operation: SDFCombineType.SmoothUnion);
            }
        }

        private static void ValidateIndex(
            IReadOnlyList<SdfStencil.ComponentDefinition> components, int index)
        {
            if (index < 0 || index >= components.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index,
                    $"SDF component index must be between 0 and {components.Count - 1}.");
            }
        }
    }
}
