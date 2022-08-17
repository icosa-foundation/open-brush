using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public struct PolyRecipe
    {
        // Used for polymeshes that use GeneratorType FileSystem or GeometryData
        public List<Vector3> Vertices;
        public List<List<int>> Faces;
        public List<int> FaceRoles;
        public List<int> VertexRoles;
        public List<HashSet<string>> FaceTags;

        // Used for polymeshes that use any other GeneratorType
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

        // Used for all polymeshes
        public List<PreviewPolyhedron.OpDefinition> Operators;
        public int MaterialIndex;
        public ColorMethods ColorMethod;

        public Color[] Colors
        {
            get => _colors;
            set => _colors = (Color[])value.Clone();
        }
        private Color[] _colors;

        public Material CurrentMaterial => EditableModelManager.m_Instance.m_Materials[MaterialIndex];

        public static PolyRecipe FromDef(EditableModelDefinition emd)
        {
            var recipe = new PolyRecipe
            {
                GeneratorType = emd.GeneratorType
            };

            var p = emd.GeneratorParameters;

            switch (emd.GeneratorType)
            {
                // case GeneratorTypes.GeometryData:
                //     // TODO simplify face tag data structure
                //     var faceTags = new List<HashSet<string>>();
                //     foreach (var t in emd.FaceTags)
                //     {
                //         var tagSet = new HashSet<string>(t);
                //         faceTags.Add(tagSet);
                //     }
                //     def = new PolyMesh(emd.Vertices, emd.Faces, emd.FaceRoles, emd.VertexRoles, faceTags);
                //     break;
                // case GeneratorTypes.Johnson:
                //     def.JohnsonPolyType = Convert.ToInt32(p["type"]);
                //     break;
                case GeneratorTypes.Shapes:
                    recipe.ShapeType = (ShapeTypes)Convert.ToInt32(p["type"]);
                    recipe.Param1Float = Convert.ToSingle(p["a"]);
                    recipe.Param2Float = Convert.ToSingle(p["b"]);
                    recipe.Param3Float = Convert.ToSingle(p["c"]);
                    break;
                case GeneratorTypes.Radial:
                    recipe.RadialPolyType = (RadialSolids.RadialPolyType)Convert.ToInt32(p["type"]);
                    recipe.Param1Int = Convert.ToInt32(p["sides"]);
                    switch (recipe.RadialPolyType)
                    {
                        case RadialSolids.RadialPolyType.Prism:
                        case RadialSolids.RadialPolyType.Antiprism:
                        case RadialSolids.RadialPolyType.Pyramid:
                        case RadialSolids.RadialPolyType.Dipyramid:
                        case RadialSolids.RadialPolyType.OrthoBicupola:
                        case RadialSolids.RadialPolyType.GyroBicupola:
                        case RadialSolids.RadialPolyType.Cupola:
                            recipe.Param2Float = Convert.ToSingle(p["height"]);
                            recipe.Param2Float = Convert.ToSingle(p["capheight"]);
                            break;
                        default:
                            recipe.Param2Float = Convert.ToSingle(p["height"]);
                            recipe.Param3Float = Convert.ToSingle(p["capheight"]);
                            break;
                    }
                    break;
                case GeneratorTypes.Uniform:
                    recipe.UniformPolyType = (UniformTypes)Convert.ToInt32(p["type"]);
                    break;
                case GeneratorTypes.Waterman:
                    recipe.Param1Int = Convert.ToInt32(p["root"]);
                    recipe.Param2Int = Convert.ToInt32(p["c"]);
                    break;
                case GeneratorTypes.Grid:
                    recipe.GridType = (GridEnums.GridTypes)Convert.ToInt32(p["type"]);
                    recipe.GridShape = (GridEnums.GridShapes)Convert.ToInt32(p["shape"]);
                    recipe.Param1Int = Convert.ToInt32(p["x"]);
                    recipe.Param2Int = Convert.ToInt32(p["y"]);
                    break;
                case GeneratorTypes.Various:
                    recipe.VariousSolidsType = (VariousSolidTypes)Convert.ToInt32(p["type"]);
                    switch (recipe.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            recipe.Param1Int = Convert.ToInt32(p["x"]);
                            recipe.Param2Int = Convert.ToInt32(p["y"]);
                            recipe.Param3Int = Convert.ToInt32(p["z"]);
                            break;
                        case VariousSolidTypes.Torus:
                            recipe.Param1Int = Convert.ToInt32(p["x"]);
                            recipe.Param2Int = Convert.ToInt32(p["y"]);
                            recipe.Param1Float = Convert.ToSingle(p["z"]);
                            break;
                        case VariousSolidTypes.Stairs:
                            recipe.Param1Int = Convert.ToInt32(p["x"]);
                            recipe.Param1Float = Convert.ToSingle(p["y"]);
                            recipe.Param2Float = Convert.ToSingle(p["z"]);
                            break;
                        case VariousSolidTypes.UvSphere:
                        case VariousSolidTypes.UvHemisphere:
                            recipe.Param1Int = Convert.ToInt32(p["x"]);
                            recipe.Param2Int = Convert.ToInt32(p["y"]);
                            break;
                    }
                    break;
            }

            recipe.Operators = new List<PreviewPolyhedron.OpDefinition>();
            if (emd.Operations != null)
            {
                foreach (var opDict in emd.Operations)
                {
                    bool disabled = Convert.ToBoolean(opDict["disabled"]);
                    PolyMesh.Operation opType = (PolyMesh.Operation)Convert.ToInt32(opDict["operation"]);
                    float amount = Convert.ToSingle(opDict["param1"]);
                    float amount2 = Convert.ToSingle(opDict["param2"]);
                    Color paramColor = Color.white;
                    if (opDict.ContainsKey("paramColor"))
                    {
                        var colorData = (opDict["paramColor"] as JArray);
                        paramColor = new Color(
                            colorData[0].Value<float>(),
                            colorData[1].Value<float>(),
                            colorData[2].Value<float>()
                        );
                    }

                    // Filter filterType = PreviewPolyhedron.OpDefinition.MakeFilterFromDict(opDict);
                    // OpParams parameters = new OpParams(param1, param2, $"#{ColorUtility.ToHtmlStringRGB(paramColor)}", filter);
                    FilterTypes filterType = (FilterTypes)Convert.ToInt32(opDict["filterType"]);
                    bool amountRandomize = Convert.ToBoolean(opDict["param1Randomize"]);
                    bool amount2Randomize = Convert.ToBoolean(opDict["param2Randomize"]);
                    float filterParamFloat = Convert.ToSingle(opDict["filterParamFloat"]);
                    int filterParamInt = Convert.ToInt32(opDict["filterParamInt"]);
                    bool filterNot = Convert.ToBoolean(opDict["filterNot"]);

                    var opDef = new PreviewPolyhedron.OpDefinition
                    {
                        opType = opType,
                        amount = amount,
                        amountRandomize = amountRandomize,
                        amount2 = amount2,
                        amount2Randomize = amount2Randomize,
                        disabled = disabled,
                        filterType = filterType,
                        filterParamFloat = filterParamFloat,
                        filterParamInt = filterParamInt,
                        paramColor = paramColor,
                        filterNot = filterNot,
                    };
                    recipe.Operators.Add(opDef);
                }
            }

            recipe.Colors = (Color[])emd.Colors.Clone();
            recipe.MaterialIndex = emd.MaterialIndex;
            recipe.ColorMethod = emd.ColorMethod;
            return recipe;
        }
    }

    public static class PolyBuilder
    {
        public static (PolyMesh, PolyMesh.MeshData) BuildFromPolyDef(PolyRecipe p)
        {
            PolyMesh poly = null;

            switch (p.GeneratorType)
            {
                case GeneratorTypes.Uniform:

                    var wythoff = new WythoffPoly(p.UniformPolyType);
                    poly = wythoff.Build();
                    poly = poly.SitLevel();
                    poly.ScalingFactor = 0.864f;
                    break;
                case GeneratorTypes.Waterman:
                    poly = WatermanPoly.Build(root: p.Param1Int, c: p.Param2Int, mergeFaces: true);
                    break;
                case GeneratorTypes.Grid:
                    poly = Grids.Build(p.GridType, p.GridShape, p.Param1Int, p.Param2Int);
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
                    poly.ScalingFactor = Mathf.Sqrt(2f) / 2f;
                    break;
                case GeneratorTypes.Shapes:
                    switch (p.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            p.Param1Int = Mathf.Max(p.Param1Int, 3);
                            poly = Shapes.Build(ShapeTypes.Polygon, p.Param1Int);
                            // Intentionally different to radial scaling.
                            // Set so side lengths will match for any polygon
                            poly.ScalingFactor = 1f / (2f * Mathf.Sin(Mathf.PI / p.Param1Int));
                            break;
                        case ShapeTypes.Star:
                            p.Param1Int = Mathf.Max(p.Param1Int, 3);
                            poly = Shapes.Build(ShapeTypes.Star, p.Param1Int, p.Param2Float);
                            poly.ScalingFactor = 1f / (2f * Mathf.Sin(Mathf.PI / p.Param1Int));
                            break;
                        case ShapeTypes.L_Shape:
                            poly = Shapes.Build(ShapeTypes.L_Shape, p.Param1Float, p.Param2Float, p.Param3Float, Shapes.Method.Convex);
                            break;
                        case ShapeTypes.C_Shape:
                            poly = Shapes.Build(ShapeTypes.C_Shape, p.Param1Float, p.Param2Float, p.Param3Float, Shapes.Method.Convex);
                            break;
                        case ShapeTypes.H_Shape:
                            poly = Shapes.Build(ShapeTypes.H_Shape, p.Param1Float, p.Param2Float, p.Param3Float, Shapes.Method.Convex);
                            break;
                        case ShapeTypes.Arc:
                            poly = Shapes.Build(ShapeTypes.Arc, p.Param1Int, p.Param2Float, p.Param3Float, Shapes.Method.Convex);
                            break;
                        case ShapeTypes.Arch:
                            poly = Shapes.Build(ShapeTypes.Arch, p.Param1Int, p.Param2Float, p.Param3Float, Shapes.Method.Convex);
                            break;
                    }
                    break;
                case GeneratorTypes.Various:
                    switch (p.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            poly = VariousSolids.Box(p.Param1Int, p.Param2Int, p.Param3Int);
                            poly.ScalingFactor = 1f / Mathf.Sqrt(2f);
                            break;
                        case VariousSolidTypes.UvSphere:
                            poly = VariousSolids.UvSphere(p.Param1Int, p.Param2Int);
                            poly.ScalingFactor = 0.5f;
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            poly = VariousSolids.UvHemisphere(p.Param1Int, p.Param2Int);
                            poly.ScalingFactor = 0.5f;
                            break;
                        case VariousSolidTypes.Torus:
                            poly = VariousSolids.Torus(p.Param1Int, p.Param2Int, p.Param3Float);
                            poly.ScalingFactor = 1f / Mathf.Sqrt(2f);
                            break;
                        case VariousSolidTypes.Stairs:
                            poly = VariousSolids.Stairs(p.Param1Int, p.Param2Float, p.Param3Float);
                            poly.ScalingFactor = 1f / Mathf.Sqrt(2f);
                            break;
                    }
                    break;
            }

            if (poly == null) Debug.LogError($"No initial poly generated for: GeneratorType: {p.GeneratorType}");

            if (p.Operators != null)
            {
                foreach (var op in p.Operators.ToList())
                {
                    if (op.disabled || op.opType == PolyMesh.Operation.Identity) continue;
                    poly = ApplyOp(poly, op);
                }
            }
            PolyMesh.MeshData meshData = poly.BuildMeshData(false, p.Colors, p.ColorMethod);
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
