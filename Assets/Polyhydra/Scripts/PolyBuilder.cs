using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public struct PolyDefinition
    {
        public GeneratorTypes GeneratorType;
        public UniformTypes UniformPolyType;
        public RadialSolids.RadialPolyType RadialPolyType;
        public VariousSolidTypes VariousSolidsType;
        public ShapeTypes ShapeType;
        public GridEnums.GridTypes GridType;
        public GridEnums.GridShapes GridShape;
        public int Param1Int;
        public int Param2Int;
        public int Param3Int;
        public float Param1Float;
        public float Param2Float;
        public float Param3Float;
        public List<PreviewPolyhedron.OpDefinition> Operators;
    }

    public static class PolyBuilder
    {
        public static (PolyMesh, PolyMesh.MeshData) BuildFromPolyDef(PolyDefinition p)
        {
            PolyMesh poly = null;

            switch (p.GeneratorType)
            {
                case GeneratorTypes.Uniform:

                    var wythoff = new WythoffPoly(p.UniformPolyType);
                    poly = wythoff.Build();
                    poly = poly.SitLevel();
                    EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                    {
                        { "type", p.UniformPolyType },
                    };
                    poly.ScalingFactor = 0.864f;
                    break;
                case GeneratorTypes.Waterman:
                    poly = WatermanPoly.Build(root: p.Param1Int, c: p.Param2Int, mergeFaces: true);
                    EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                    {
                        { "root", p.Param1Int },
                        { "c", p.Param2Int },
                    };
                    break;
                case GeneratorTypes.Grid:
                    poly = Grids.Build(p.GridType, p.GridShape, p.Param1Int, p.Param2Int);
                    EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                    {
                        { "type", p.GridType },
                        { "shape", p.GridShape },
                        { "x", p.Param1Int },
                        { "y", p.Param2Int },
                    };
                    poly.ScalingFactor = Mathf.Sqrt(2f) / 2f;
                    break;
                case GeneratorTypes.Radial:
                    p.Param1Int = Mathf.Max(p.Param1Int, 3);
                    float height, capHeight;
                    switch (p.RadialPolyType)
                    {
                        case RadialSolids.RadialPolyType.Prism:
                        case RadialSolids.RadialPolyType.Antiprism:
                        case RadialSolids.RadialPolyType.Pyramid:
                        case RadialSolids.RadialPolyType.Dipyramid:
                        case RadialSolids.RadialPolyType.OrthoBicupola:
                        case RadialSolids.RadialPolyType.GyroBicupola:
                        case RadialSolids.RadialPolyType.Cupola:
                            height = p.Param2Float;
                            capHeight = p.Param2Float;
                            break;
                        default:
                            height = p.Param2Float;
                            capHeight = p.Param3Float;
                            break;
                    }

                    poly = RadialSolids.Build(p.RadialPolyType, p.Param1Int, height, capHeight);
                    EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                    {
                        { "type", p.RadialPolyType },
                        { "sides", p.Param1Int },
                        { "height", height },
                        { "capheight", capHeight },
                    };
                    poly.ScalingFactor = Mathf.Sqrt(2f) / 2f;
                    break;
                case GeneratorTypes.Shapes:
                    switch (p.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            p.Param1Int = Mathf.Max(p.Param1Int, 3);
                            poly = Shapes.Build(ShapeTypes.Polygon, p.Param1Int);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.Polygon },
                                { "sides", p.Param1Int },
                            };
                            // Intentionally different to radial scaling.
                            // Set so side lengths will match for any polygon
                            poly.ScalingFactor = 1f / (2f * Mathf.Sin(Mathf.PI / p.Param1Int));
                            break;
                        case ShapeTypes.Star:
                            p.Param1Int = Mathf.Max(p.Param1Int, 3);
                            poly = Shapes.Build(ShapeTypes.Star, p.Param1Int, p.Param2Float);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.Star },
                                { "sides", p.Param1Int },
                                { "sharpness", p.Param2Float },
                            };
                            poly.ScalingFactor = 1f / (2f * Mathf.Sin(Mathf.PI / p.Param1Int));
                            break;
                        case ShapeTypes.L_Shape:
                            poly = Shapes.Build(ShapeTypes.L_Shape, p.Param1Float, p.Param2Float, p.Param3Float, Shapes.Method.Convex);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.L_Shape },
                                { "a", p.Param1Float },
                                { "b", p.Param2Float },
                                { "c", p.Param3Float },
                            };
                            break;
                        case ShapeTypes.C_Shape:
                            poly = Shapes.Build(ShapeTypes.C_Shape, p.Param1Float, p.Param2Float, p.Param3Float, Shapes.Method.Convex);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.C_Shape },
                                { "a", p.Param1Float },
                                { "b", p.Param2Float },
                                { "c", p.Param3Float },
                            };
                            break;
                        case ShapeTypes.H_Shape:
                            poly = Shapes.Build(ShapeTypes.H_Shape, p.Param1Float, p.Param2Float, p.Param3Float, Shapes.Method.Convex);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.H_Shape },
                                { "a", p.Param1Float },
                                { "b", p.Param2Float },
                                { "c", p.Param3Float },
                            };
                            break;
                    }
                    break;
                case GeneratorTypes.Various:
                    switch (p.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            poly = VariousSolids.Box(p.Param1Int, p.Param2Int, p.Param3Int);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.Box },
                                { "x", p.Param1Int },
                                { "y", p.Param2Int },
                                { "z", p.Param3Int },
                            };
                            poly.ScalingFactor = 1f / Mathf.Sqrt(2f);
                            break;
                        case VariousSolidTypes.UvSphere:
                            poly = VariousSolids.UvSphere(p.Param1Int, p.Param2Int);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.UvSphere },
                                { "x", p.Param1Int },
                                { "y", p.Param2Int },
                            };
                            poly.ScalingFactor = 0.5f;
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            poly = VariousSolids.UvHemisphere(p.Param1Int, p.Param2Int);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.UvHemisphere },
                                { "x", p.Param1Int },
                                { "y", p.Param2Int },
                            };
                            poly.ScalingFactor = 0.5f;
                            break;
                        case VariousSolidTypes.Torus:
                            poly = VariousSolids.Torus(p.Param1Int, p.Param2Int, p.Param3Float);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.Torus },
                                { "x", p.Param1Int },
                                { "y", p.Param2Int },
                                { "z", p.Param3Float },
                            };
                            poly.ScalingFactor = 1f / Mathf.Sqrt(2f);
                            break;
                        case VariousSolidTypes.Stairs:
                            poly = VariousSolids.Stairs(p.Param1Int, p.Param2Float, p.Param3Float);
                            EditableModelManager.CurrentModel.GeneratorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.Stairs },
                                { "x", p.Param1Int },
                                { "y", p.Param2Float },
                                { "z", p.Param3Float },
                            };
                            poly.ScalingFactor = 1f / Mathf.Sqrt(2f);
                            break;
                    }
                    break;
            }

            if (poly == null) Debug.LogError($"No initial poly generated for: GeneratorType: {p.GeneratorType}");

            EditableModelManager.CurrentModel.Operations = new List<Dictionary<string, object>>();
            if (p.Operators != null)
            {
                foreach (var op in p.Operators.ToList())
                {
                    EditableModelManager.CurrentModel.Operations.Add(new Dictionary<string, object>
                    {
                        { "operation", op.opType },
                        { "param1", op.amount },
                        { "param1Randomize", op.amountRandomize },
                        { "param2", op.amount2 },
                        { "param2Randomize", op.amount2Randomize },
                        { "paramColor", op.paramColor },
                        { "disabled", op.disabled },
                        { "filterType", op.filterType },
                        { "filterParamFloat", op.filterParamFloat },
                        { "filterParamInt", op.filterParamInt },
                        { "filterNot", op.filterNot },
                    });
                    if (op.disabled || op.opType == PolyMesh.Operation.Identity) continue;
                    poly = ApplyOp(poly, op);
                }
            }
            PolyMesh.MeshData meshData = poly.BuildMeshData(false, EditableModelManager.CurrentModel.Colors, EditableModelManager.CurrentModel.ColorMethod);
            return (poly, meshData);
        }

        public static PolyMesh ApplyOp(PolyMesh poly, PreviewPolyhedron.OpDefinition op)
        {
            // Store the previous scaling factor to reapply afterwards
            float previousScalingFactor = poly.ScalingFactor;

            var _random = new System.Random();
            var filter = Filter.GetFilter(op.filterType, op.filterParamFloat, op.filterParamInt, op.filterNot);

            var opFunc1 = new OpFunc(_ => Mathf.Lerp(0, op.amount, (float)_random.NextDouble()));
            var opFunc2 = new OpFunc(_ => Mathf.Lerp(0, op.amount2, (float)_random.NextDouble()));

            OpParams opParams = (op.amountRandomize, op.amount2Randomize) switch
            {
                (false, false) => new OpParams(
                    op.amount,
                    op.amount2,
                    $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                    filter
                ),
                (true, false) => new OpParams(
                    opFunc1,
                    op.amount2,
                    $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                    filter
                ),
                (false, true) => new OpParams(
                    op.amount,
                    opFunc2,
                    $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                    filter
                ),
                (true, true) => new OpParams(
                    opFunc1,
                    opFunc2,
                    $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                    filter
                ),
            };

            poly = poly.AppyOperation(op.opType, opParams);

            // Reapply the original scaling factor
            poly.ScalingFactor = previousScalingFactor;

            return poly;
        }

    }
}
