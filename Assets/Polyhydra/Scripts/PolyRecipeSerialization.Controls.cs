using System;
using System.Collections.Generic;
using Polyhydra.Core;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    internal static partial class PolyRecipeSerialization
    {
        // Adapter for the existing 3D panel's enum/slider state. These dictionaries
        // are mapped to named shared generators before any current-format write.
        public static Dictionary<string, object> ControlParameters(PolyRecipe recipe)
        {
            var generatorParameters = new Dictionary<string, object>();

            switch (recipe.GeneratorType)
            {
                case GeneratorTypes.Uniform:
                    generatorParameters = new Dictionary<string, object>
                    {
                        { "type", recipe.UniformPolyType },
                    };
                    break;
                case GeneratorTypes.Waterman:
                    generatorParameters = new Dictionary<string, object>
                    {
                        { "root", recipe.Param1Int },
                        { "c", recipe.Param2Int },
                    };
                    break;
                case GeneratorTypes.RegularGrids:
                case GeneratorTypes.CatalanGrids:
                case GeneratorTypes.OneUniformGrids:
                case GeneratorTypes.TwoUniformGrids:
                case GeneratorTypes.DurerGrids:
                    generatorParameters = new Dictionary<string, object>
                    {
                        { "type", recipe.GridType },
                        { "shape", recipe.GridShape },
                        { "x", recipe.Param1Int },
                        { "y", recipe.Param2Int },
                    };
                    break;
                case GeneratorTypes.Radial:
                    recipe.Param1Int = Mathf.Max(recipe.Param1Int, 3);
                    float height, capHeight;
                    switch (recipe.RadialPolyType)
                    {
                        case RadialSolids.RadialPolyType.Prism:
                        case RadialSolids.RadialPolyType.Antiprism:
                        case RadialSolids.RadialPolyType.Pyramid:
                        case RadialSolids.RadialPolyType.Dipyramid:
                        case RadialSolids.RadialPolyType.OrthoBicupola:
                        case RadialSolids.RadialPolyType.GyroBicupola:
                        case RadialSolids.RadialPolyType.Cupola:
                            height = recipe.Param2Float;
                            capHeight = recipe.Param2Float;
                            break;
                        default:
                            height = recipe.Param2Float;
                            capHeight = recipe.Param3Float;
                            break;
                    }

                    generatorParameters = new Dictionary<string, object>
                    {
                        { "type", recipe.RadialPolyType },
                        { "sides", recipe.Param1Int },
                        { "height", height },
                        { "capheight", capHeight },
                    };
                    break;
                case GeneratorTypes.Shapes:
                    switch (recipe.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            recipe.Param1Int = Mathf.Max(recipe.Param1Int, 3);
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.Polygon },
                                { "sides", recipe.Param1Int },
                            };
                            // Intentionally different to radial scaling.
                            // Set so side lengths will match for any polygon
                            break;
                        case ShapeTypes.Star:
                            recipe.Param1Int = Mathf.Max(recipe.Param1Int, 3);
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.Star },
                                { "sides", recipe.Param1Int },
                                { "sharpness", recipe.Param2Float },
                            };
                            break;
                        case ShapeTypes.L_Shape:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.L_Shape },
                                { "a", recipe.Param1Float },
                                { "b", recipe.Param2Float },
                                { "c", recipe.Param3Float },
                            };
                            break;
                        case ShapeTypes.C_Shape:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.C_Shape },
                                { "a", recipe.Param1Float },
                                { "b", recipe.Param2Float },
                                { "c", recipe.Param3Float },
                            };
                            break;
                        case ShapeTypes.H_Shape:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.H_Shape },
                                { "a", recipe.Param1Float },
                                { "b", recipe.Param2Float },
                                { "c", recipe.Param3Float },
                            };
                            break;
                        case ShapeTypes.Arc:
                        case ShapeTypes.Arch:
                        case ShapeTypes.Ring:
                        case ShapeTypes.Triangle:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", recipe.ShapeType },
                                { "a", recipe.Param1Int },
                                { "b", recipe.Param2Float },
                                { "c", recipe.Param3Float },
                            };
                            break;
                    }
                    if (recipe.ShapeExtrusion > 0f)
                    {
                        generatorParameters["height"] = recipe.ShapeExtrusion;
                    }
                    break;
                case GeneratorTypes.Various:
                    switch (recipe.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.Box },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Int },
                                { "z", recipe.Param3Int },
                            };
                            break;
                        case VariousSolidTypes.UvSphere:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.UvSphere },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Int },
                                { "z", recipe.Param3Float <= 0f ? 1f : recipe.Param3Float },
                            };
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.UvHemisphere },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Int },
                            };
                            break;
                        case VariousSolidTypes.Torus:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.Torus },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Int },
                                { "z", recipe.Param3Float },
                            };
                            break;
                        case VariousSolidTypes.Stairs:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.Stairs },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Float },
                                { "z", recipe.Param3Float },
                            };
                            break;
                        case VariousSolidTypes.Capsule:
                        case VariousSolidTypes.ChamferedCylinder:
                        case VariousSolidTypes.HollowHemisphere:
                        case VariousSolidTypes.PartialTorus:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", recipe.VariousSolidsType },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Int },
                                { "z", recipe.Param3Float },
                            };
                            break;
                        case VariousSolidTypes.ChamferedBox:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.ChamferedBox },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Float },
                                { "z", recipe.Param3Float },
                            };
                            break;
                        case VariousSolidTypes.WireframeBox:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.WireframeBox },
                                { "z", recipe.Param3Float },
                            };
                            break;
                    }
                    break;
            }
            return generatorParameters;
        }

    }
}
