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
        // TODO Clone on assignment?
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
        public List<PreviewPolyhedron.OpDefinition> Operators;

        // Used for all polymeshes
        public int MaterialIndex;
        public ColorMethods ColorMethod;
        public Color[] Colors;

        public Material CurrentMaterial => EditableModelManager.m_Instance.m_Materials[MaterialIndex];

        public PolyRecipe Clone()
        {
            var clone = this;
            clone.Colors = (Color[])Colors?.Clone();
            clone.Operators = (Operators == null) ? null : new List<PreviewPolyhedron.OpDefinition>(Operators);
            return clone;
        }

        public static PolyRecipe FromDef(EditableModelDefinition emd)
        {
            var recipe = new PolyRecipe
            {
                GeneratorType = emd.GeneratorType
            };

            var p = emd.GeneratorParameters;

            switch (emd.GeneratorType)
            {
                case GeneratorTypes.GeometryData:
                    recipe.Vertices = emd.Vertices.ToList();
                    recipe.Faces = emd.Faces.ToList();
                    recipe.FaceRoles = emd.FaceRoles.Select(r => (int)r).ToList();
                    recipe.FaceTags = emd.FaceTags;
                    break;
                // case GeneratorTypes.Johnson:
                //     recipe.JohnsonPolyType = Convert.ToInt32(p.GetValueOrDefault("type"]);)                //     break;
                case GeneratorTypes.Shapes:
                    recipe.ShapeType = (ShapeTypes)Convert.ToInt32(p.GetValueOrDefault("type"));
                    recipe.Param1Float = Convert.ToSingle(p.GetValueOrDefault("a"));
                    recipe.Param2Float = Convert.ToSingle(p.GetValueOrDefault("b"));
                    recipe.Param3Float = Convert.ToSingle(p.GetValueOrDefault("c"));
                    break;
                case GeneratorTypes.Radial:
                    recipe.RadialPolyType = (RadialSolids.RadialPolyType)Convert.ToInt32(p.GetValueOrDefault("type"));
                    recipe.Param1Int = Convert.ToInt32(p.GetValueOrDefault("sides"));
                    switch (recipe.RadialPolyType)
                    {
                        case RadialSolids.RadialPolyType.Prism:
                        case RadialSolids.RadialPolyType.Antiprism:
                        case RadialSolids.RadialPolyType.Pyramid:
                        case RadialSolids.RadialPolyType.Dipyramid:
                        case RadialSolids.RadialPolyType.OrthoBicupola:
                        case RadialSolids.RadialPolyType.GyroBicupola:
                        case RadialSolids.RadialPolyType.Cupola:
                            recipe.Param2Float = Convert.ToSingle(p.GetValueOrDefault("height"));
                            recipe.Param2Float = Convert.ToSingle(p.GetValueOrDefault("capheight"));
                            break;
                        default:
                            recipe.Param2Float = Convert.ToSingle(p.GetValueOrDefault("height"));
                            recipe.Param3Float = Convert.ToSingle(p.GetValueOrDefault("capheight"));
                            break;
                    }
                    break;
                case GeneratorTypes.Uniform:
                    recipe.UniformPolyType = (UniformTypes)Convert.ToInt32(p.GetValueOrDefault("type"));
                    break;
                case GeneratorTypes.Waterman:
                    recipe.Param1Int = Convert.ToInt32(p.GetValueOrDefault("root"));
                    recipe.Param2Int = Convert.ToInt32(p.GetValueOrDefault("c"));
                    break;
                case GeneratorTypes.RegularGrids:
                case GeneratorTypes.CatalanGrids:
                case GeneratorTypes.OneUniformGrids:
                case GeneratorTypes.TwoUniformGrids:
                case GeneratorTypes.DurerGrids:
                    recipe.GridType = (GridEnums.GridTypes)Convert.ToInt32(p.GetValueOrDefault("type"));
                    recipe.GridShape = (GridEnums.GridShapes)Convert.ToInt32(p.GetValueOrDefault("shape"));
                    recipe.Param1Int = Convert.ToInt32(p.GetValueOrDefault("x"));
                    recipe.Param2Int = Convert.ToInt32(p.GetValueOrDefault("y"));
                    break;
                case GeneratorTypes.Various:
                    recipe.VariousSolidsType = (VariousSolidTypes)Convert.ToInt32(p.GetValueOrDefault("type"));
                    switch (recipe.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            recipe.Param1Int = Convert.ToInt32(p.GetValueOrDefault("x"));
                            recipe.Param2Int = Convert.ToInt32(p.GetValueOrDefault("y"));
                            recipe.Param3Int = Convert.ToInt32(p.GetValueOrDefault("z"));
                            break;
                        case VariousSolidTypes.Torus:
                            recipe.Param1Int = Convert.ToInt32(p.GetValueOrDefault("x"));
                            recipe.Param2Int = Convert.ToInt32(p.GetValueOrDefault("y"));
                            recipe.Param1Float = Convert.ToSingle(p.GetValueOrDefault("z"));
                            break;
                        case VariousSolidTypes.Stairs:
                            recipe.Param1Int = Convert.ToInt32(p.GetValueOrDefault("x"));
                            recipe.Param1Float = Convert.ToSingle(p.GetValueOrDefault("y"));
                            recipe.Param2Float = Convert.ToSingle(p.GetValueOrDefault("z"));
                            break;
                        case VariousSolidTypes.UvSphere:
                        case VariousSolidTypes.UvHemisphere:
                            recipe.Param1Int = Convert.ToInt32(p.GetValueOrDefault("x"));
                            recipe.Param2Int = Convert.ToInt32(p.GetValueOrDefault("y"));
                            break;
                    }
                    break;
            }

            recipe.Operators = new List<PreviewPolyhedron.OpDefinition>();
            if (emd.Operations != null)
            {
                foreach (var opDict in emd.Operations)
                {
                    bool disabled = Convert.ToBoolean(opDict.GetValueOrDefault("disabled"));
                    PolyMesh.Operation opType = (PolyMesh.Operation)Convert.ToInt32(opDict["operation"]);
                    float amount = Convert.ToSingle(opDict.GetValueOrDefault("param1"));
                    float amount2 = Convert.ToSingle(opDict.GetValueOrDefault("param2"));
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
                    bool amountRandomize = Convert.ToBoolean(opDict.GetValueOrDefault("param1Randomize"));
                    bool amount2Randomize = Convert.ToBoolean(opDict.GetValueOrDefault("param2Randomize"));
                    float filterParamFloat = Convert.ToSingle(opDict.GetValueOrDefault("filterParamFloat"));
                    int filterParamInt = Convert.ToInt32(opDict.GetValueOrDefault("filterParamInt"));
                    bool filterNot = Convert.ToBoolean(opDict.GetValueOrDefault("filterNot"));

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
            Color[] colors;
            if (emd.Colors == null || emd.Colors.Length == 0)
            {
                PolyhydraPanel polyhydraPanel = PanelManager.m_Instance.GetPanelByType(BasePanel.PanelType.Polyhydra) as PolyhydraPanel;
                colors = (Color[])polyhydraPanel.DefaultColorPalette.Clone();
            }
            else
            {
                colors = (Color[])emd.Colors.Clone();
            }
            recipe.Colors = colors;
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
                case GeneratorTypes.RegularGrids:
                case GeneratorTypes.CatalanGrids:
                case GeneratorTypes.OneUniformGrids:
                case GeneratorTypes.TwoUniformGrids:
                case GeneratorTypes.DurerGrids:
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
