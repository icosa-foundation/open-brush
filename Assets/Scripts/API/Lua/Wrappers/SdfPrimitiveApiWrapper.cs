using System;
using IsoMesh;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("An editable primitive in an SDF guide")]
    [MoonSharpUserData]
    public class SdfPrimitiveApiWrapper
    {
        private readonly SdfStencil m_Stencil;
        private SDFPrimitive m_Primitive;

        public SdfPrimitiveApiWrapper(SdfStencil stencil, SDFPrimitive primitive)
        {
            m_Stencil = stencil;
            m_Primitive = primitive;
        }

        [LuaDocsDescription("The primitive geometry type: sphere, torus, cuboid, boxframe, or cylinder")]
        public string primitiveType
        {
            get => SdfStencil.PrimitiveTypeName(Primitive.Type);
            set => m_Stencil.SetPrimitiveGeometry(
                Primitive, SdfStencil.ParsePrimitiveType(value), Primitive.Data);
        }

        [LuaDocsDescription("The primitive dimensions")]
        public Vector4ApiWrapper geometry
        {
            get => new Vector4ApiWrapper(Primitive.Data);
            set => m_Stencil.SetPrimitiveGeometry(Primitive, Primitive.Type, value._Vector4);
        }

        [LuaDocsDescription("The primitive transform relative to its guide")]
        public TrTransform transform
        {
            get => TrTransform.FromLocalTransform(Primitive.transform);
            set => m_Stencil.SetPrimitiveTransform(Primitive, value);
        }

        [LuaDocsDescription("How this primitive combines with the preceding result: union, subtract, or intersect")]
        public string operation
        {
            get => SdfStencil.OperationName(Primitive.Operation);
            set => m_Stencil.SetPrimitiveOperation(Primitive, SdfStencil.ParseOperation(value));
        }

        [LuaDocsDescription("The non-negative smoothing distance for this primitive's operation")]
        public float blend
        {
            get => Primitive.Smoothing;
            set => m_Stencil.SetPrimitiveBlend(Primitive, value);
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
            m_Stencil.UpdatePrimitive(
                Primitive, SdfStencil.ParsePrimitiveType(primitiveType), geometry._Vector4,
                transform, SdfStencil.ParseOperation(operation), blend);
        }

        [LuaDocsDescription("The zero-based position of this primitive in the SDF evaluation order")]
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

        [LuaDocsDescription("Removes this primitive from its SDF guide")]
        public void Delete()
        {
            m_Stencil.RemovePrimitive(Primitive);
            m_Primitive = null;
        }

        [LuaDocsDescription("Returns a string representation of the SDF primitive")]
        public override string ToString()
        {
            return $"SDFPrimitive({primitiveType}, {operation}, index {index})";
        }

        private SDFPrimitive Primitive
        {
            get
            {
                if (m_Primitive == null)
                {
                    throw new InvalidOperationException("The SDF primitive has been deleted.");
                }
                return m_Primitive;
            }
        }
    }
}
