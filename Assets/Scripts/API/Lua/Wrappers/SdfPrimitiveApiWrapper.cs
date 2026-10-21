using System;
using System.Linq;
using IsoMesh;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("An editable primitive in an SDF guide")]
    [MoonSharpUserData]
    public class SdfPrimitiveApiWrapper
    {
        private readonly SdfStencil m_Stencil;
        private readonly Guid m_ComponentIdentity;
        private bool m_Deleted;

        public SdfPrimitiveApiWrapper(SdfStencil stencil, SDFPrimitive primitive)
        {
            m_Stencil = stencil;
            m_ComponentIdentity = stencil.GetComponentIdentity(primitive);
        }

        [LuaDocsDescription("The primitive geometry type: sphere, torus, cuboid, boxframe, or cylinder")]
        public string primitiveType
        {
            get => SdfStencil.PrimitiveTypeName(Primitive.Type);
            set => ApplyEdit(slot => EditSdfGuideCommand.SetPrimitiveGeometry(
                m_Stencil, slot, SdfStencil.ParsePrimitiveType(value), Primitive.Data));
        }

        [LuaDocsDescription("The primitive dimensions")]
        public Vector4ApiWrapper geometry
        {
            get => new Vector4ApiWrapper(Primitive.Data);
            set => ApplyEdit(slot => EditSdfGuideCommand.SetPrimitiveGeometry(
                m_Stencil, slot, Primitive.Type, value._Vector4));
        }

        [LuaDocsDescription("The primitive transform relative to its guide")]
        public TrTransform transform
        {
            get => TrTransform.FromLocalTransform(Primitive.transform);
            set => ApplyEdit(slot => EditSdfGuideCommand.SetComponentTransform(m_Stencil, slot, value));
        }

        [LuaDocsDescription("How this primitive combines with the preceding result: union, subtract, or intersect")]
        public string operation
        {
            get => SdfStencil.OperationName(Primitive.Operation);
            set => ApplyEdit(slot => EditSdfGuideCommand.SetComponentOperation(
                m_Stencil, slot, SdfStencil.ParseOperation(value)));
        }

        [LuaDocsDescription("The non-negative smoothing distance for this primitive's operation")]
        public float blend
        {
            get => Primitive.Smoothing;
            set => ApplyEdit(slot => EditSdfGuideCommand.SetComponentBlend(m_Stencil, slot, value));
        }

        [LuaDocsDescription("Updates the geometry, local transform, operation, and blend in one mesh rebuild")]
        [LuaDocsParameter("primitiveType", "sphere, torus, cuboid, boxframe, or cylinder")]
        [LuaDocsParameter("geometry", "The primitive dimensions")]
        [LuaDocsParameter("transform", "The primitive transform relative to its guide")]
        [LuaDocsParameter("operation", "union, subtract, or intersect")]
        [LuaDocsParameter("blend", "The non-negative smoothing distance")]
        public void Update(
            string primitiveType, Vector4ApiWrapper geometry, TrTransform transform,
            string operation, float blend)
        {
            ApplyEdit(slot => EditSdfGuideCommand.UpdatePrimitive(
                m_Stencil, slot, SdfStencil.ParsePrimitiveType(primitiveType), geometry._Vector4,
                transform, SdfStencil.ParseOperation(operation), blend));
        }

        [LuaDocsDescription("The zero-based index of this primitive in the analytic primitive list")]
        public int index
        {
            get
            {
                var primitives = m_Stencil.GetPrimitives();
                for (int i = 0; i < primitives.Count; ++i)
                {
                    if (primitives[i] == Primitive)
                    {
                        return i;
                    }
                }
                throw new InvalidOperationException("The SDF primitive is no longer part of its guide.");
            }
        }

        [LuaDocsDescription("The zero-based position of this primitive in the complete SDF component evaluation order, including mesh operands")]
        public int componentIndex
        {
            get
            {
                var components = m_Stencil.GetComponents();
                for (int i = 0; i < components.Count; ++i)
                {
                    if (components[i] == Primitive)
                    {
                        return i;
                    }
                }
                throw new InvalidOperationException("The SDF primitive is no longer part of its guide.");
            }
        }

        [LuaDocsDescription("Removes this primitive from its SDF guide")]
        public void Delete()
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                EditSdfGuideCommand.RemoveComponent(m_Stencil, componentIndex));
            m_Deleted = true;
        }

        [LuaDocsDescription("Returns a string representation of the SDF primitive")]
        public override string ToString()
        {
            return $"SDFPrimitive({primitiveType}, {operation}, index {index})";
        }

        private void ApplyEdit(Func<int, EditSdfGuideCommand> createCommand)
        {
            int slot = componentIndex;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand(slot));
        }

        private SDFPrimitive Primitive
        {
            get
            {
                SDFPrimitive primitive = m_Deleted ? null : m_Stencil.GetComponents()
                    .OfType<SDFPrimitive>().FirstOrDefault(component =>
                        m_Stencil.GetComponentIdentity(component) == m_ComponentIdentity);
                if (primitive == null)
                {
                    throw new InvalidOperationException("The SDF primitive has been deleted.");
                }
                return primitive;
            }
        }
    }
}
