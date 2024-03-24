// Copyright 2022 The Tilt Brush Authors
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
using System.IO;
using Newtonsoft.Json;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    using UnityEditor;

    [Serializable]
    public class OldOp
    {
        public string OpType;
        public string FaceSelections;
        public float Amount;
        public float Amount2;
        public bool Randomize;
        public bool Disabled;
    }

    [Serializable]
    public class OldPreset
    {
        public string Name;
        public string ShapeType;
        public string PolyTypeCategory;
        public string PolyType;
        public string JohnsonPolyType;
        public string OtherPolyType;
        public string GridType;
        public string GridShape;
        public bool BypassOps;
        public int PrismP;
        public int PrismQ;
        public List<OldOp> Ops;
    }

    public class PresetConverter
    {
        private static List<string> errors;
        private static List<string> warnings;

        [MenuItem("Open Brush/Convert Old Polyhydra Presets")]
        public static void Convert()
        {

            errors = new List<string>();
            warnings = new List<string>();

            var userPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                App.kAppFolderName
            );
            var oldPath = Path.Combine(userPath, "Media Library", "Old Presets");
            var newPath = Path.Combine(userPath, "Media Library", "Shape Recipes");

            var dirInfo = new DirectoryInfo(oldPath);
            FileInfo[] AllFileInfo = dirInfo.GetFiles("*.json");

            foreach (var f in AllFileInfo)
            {
                ConvertOldPreset(f, newPath);
            }
            Debug.LogError(String.Join("\n", errors));
            Debug.LogWarning(String.Join("\n", warnings));
        }
        private static void ConvertOldPreset(FileInfo fileInfo, string newPath)
        {
            var presetName = fileInfo.Name.Replace("PolyPreset-", "").Replace(".json", "");
            var jsonDeserializer = new JsonSerializer();
            jsonDeserializer.ContractResolver = new CustomJsonContractResolver();
            OldPreset oldPreset;
            using (var textReader = new StreamReader(fileInfo.FullName))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                oldPreset = jsonDeserializer.Deserialize<OldPreset>(jsonReader);

                GeneratorTypes generatorType;
                switch (oldPreset.ShapeType)
                {
                    case "Grid":
                        generatorType = GeneratorTypes.RegularGrids;
                        break;
                    case "Johnson":
                        generatorType = GeneratorTypes.Radial;
                        break;
                    case "Waterman":
                        generatorType = GeneratorTypes.Waterman;
                        break;
                    case "Uniform":
                        generatorType = GeneratorTypes.Uniform;
                        break;
                    case "Other":
                        generatorType = GeneratorTypes.Various;
                        break;
                    default:
                        generatorType = GeneratorTypes.Various;
                        break;
                }

                var generatorParameters = new Dictionary<string, object>();

                switch (generatorType)
                {
                    case GeneratorTypes.Uniform:
                        UniformTypes polyType;
                        if (Enum.TryParse(oldPreset.PolyType, true, out polyType))
                        {

                            if ((int)polyType < 6)
                            {
                                generatorType = GeneratorTypes.Radial;
                                RadialSolids.RadialPolyType radialType = RadialSolids.RadialPolyType.Prism;
                                switch (polyType)
                                {
                                    case UniformTypes.Polygonal_Prism:
                                        radialType = RadialSolids.RadialPolyType.Prism;
                                        break;
                                    case UniformTypes.Polygonal_Antiprism:
                                        radialType = RadialSolids.RadialPolyType.Antiprism;
                                        break;
                                    default:
                                        warnings.Add($"Unsupported prism type for {oldPreset.Name}. Defaulting to prism");
                                        break;
                                }
                                generatorParameters = new Dictionary<string, object>
                                {
                                    { "type", radialType},
                                    { "sides", oldPreset.PrismP},
                                    { "height", 1f },
                                    { "capheight", 0.707f },
                                };
                            }
                            else
                            {
                                generatorParameters = new Dictionary<string, object>
                                {
                                    { "type", polyType },
                                };
                            }
                        }
                        else
                        {
                            errors.Add($"Failed to parse: {oldPreset.PolyType} for {fileInfo.Name}");
                        }
                        break;
                    case GeneratorTypes.Waterman:
                        generatorParameters = new Dictionary<string, object>
                        {
                            { "root", oldPreset.PrismP },
                            { "c", oldPreset.PrismQ },
                        };
                        break;
                    case GeneratorTypes.RegularGrids:
                        GridEnums.GridTypes gridType;
                        GridEnums.GridShapes gridShape;

                        switch (oldPreset.GridType)
                        {
                            case "Square":
                                oldPreset.GridType = "Square";
                                break;
                            case "Isometric":
                                oldPreset.GridType = "Triangular";
                                break;
                            case "Hex":
                                oldPreset.GridType = "Hexagonal";
                                break;
                            case "Polar":
                                oldPreset.GridShape = "Polar";
                                oldPreset.GridType = "Square";
                                break;
                            case "U_3_6_3_6":
                                oldPreset.GridType = "SnubTrihexagonal";
                                break;
                            case "U_3_3_3_4_4":
                                oldPreset.GridType = "ElongatedTriangular";
                                break;
                            case "U_3_3_4_3_4":
                                oldPreset.GridType = "SnubSquare";
                                break;
                            case "U_3_12_12":
                                oldPreset.GridType = "Rhombitrihexagonal";
                                break;
                            case "U_4_8_8":
                                oldPreset.GridType = "Trihexagonal";
                                break;
                            case "U_3_4_6_4":
                                oldPreset.GridType = "TruncatedHexagonal";
                                break;
                            case "U_4_6_12":
                                oldPreset.GridType = "TruncatedTrihexagonal";
                                break;

                        }

                        if (oldPreset.GridShape == "Torus")
                        {
                            oldPreset.GridShape = "Sphere";
                            warnings.Add($"Unsupported grid shape: Torus. Concerted to Sphere.");
                        }

                        if (Enum.TryParse(oldPreset.GridType, true, out gridType) &&
                            Enum.TryParse(oldPreset.GridShape, true, out gridShape))
                        {
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", gridType },
                                { "shape", gridShape },
                                { "x", oldPreset.PrismP },
                                { "y", oldPreset.PrismP },
                            };
                        }
                        else
                        {
                            errors.Add($"Failed to parse: {oldPreset.GridType}/{oldPreset.GridShape} for {fileInfo.Name}");
                        }
                        break;
                    case GeneratorTypes.Radial:
                        RadialSolids.RadialPolyType radialPolyType;

                        // Rotundae aren't currently supported so swap out for Cupolae
                        oldPreset.JohnsonPolyType = oldPreset.JohnsonPolyType.Replace("Rotunda", "Cupola");
                        oldPreset.JohnsonPolyType = oldPreset.JohnsonPolyType.Replace("rotunda", "cupola");

                        if (Enum.TryParse(oldPreset.JohnsonPolyType, true, out radialPolyType))
                        {
                            float height, capHeight;
                            switch (oldPreset.JohnsonPolyType)
                            {
                                case "Prism":
                                case "Antiprism":
                                case "Pyramid":
                                case "Dipyramid":
                                case "OrthoBicupola":
                                case "GyroBicupola":
                                case "Cupola":
                                case "Rotunda":
                                    height = 1f;
                                    capHeight = .707f;
                                    break;
                                default:
                                    height = 1f;
                                    capHeight = .707f;
                                    break;
                            }
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", radialPolyType },
                                { "sides", oldPreset.PrismP },
                                { "height", height },
                                { "capheight", capHeight },
                            };
                        }
                        else
                        {
                            errors.Add($"Failed to parse: {oldPreset.JohnsonPolyType} for {fileInfo.Name}");
                        }

                        break;
                    case GeneratorTypes.Various:
                        switch (oldPreset.OtherPolyType)
                        {
                            case "Polygon":
                                generatorType = GeneratorTypes.Shapes;
                                oldPreset.PrismP = Mathf.Max(oldPreset.PrismP, 3);
                                generatorParameters = new Dictionary<string, object>
                                {
                                    { "type", ShapeTypes.Polygon },
                                    { "sides", oldPreset.PrismP },
                                };
                                break;
                            case "L_Shape":
                            case "L_Alt_Shape":
                                generatorParameters = new Dictionary<string, object>
                                {
                                    { "type", ShapeTypes.L_Shape },
                                    { "a", oldPreset.PrismP },
                                    { "b", oldPreset.PrismQ },
                                    { "c", 1 },
                                };
                                break;
                            case "C_Shape":
                                generatorParameters = new Dictionary<string, object>
                                {
                                    { "type", ShapeTypes.C_Shape },
                                    { "a", oldPreset.PrismP },
                                    { "b", oldPreset.PrismQ },
                                    { "c", 1 },
                                };
                                break;
                            case "H_Shape":
                                generatorParameters = new Dictionary<string, object>
                                {
                                    { "type", ShapeTypes.H_Shape },
                                    { "a", oldPreset.PrismP },
                                    { "b", oldPreset.PrismQ },
                                    { "c", 1 },
                                };
                                break;
                            case "GriddedCube":
                                oldPreset.Ops.Insert(0, new OldOp { OpType = "Recenter" });
                                generatorParameters = new Dictionary<string, object>
                                {
                                    { "type", VariousSolidTypes.Box },
                                    { "x", oldPreset.PrismP },
                                    { "y", oldPreset.PrismP },
                                    { "z", oldPreset.PrismP },
                                };
                                break;
                            case "UvSphere":
                                generatorParameters = new Dictionary<string, object>
                                {
                                    { "type", VariousSolidTypes.UvSphere },
                                    { "x", oldPreset.PrismP },
                                    { "y", oldPreset.PrismP },
                                };
                                break;
                            case "UvHemisphere":
                                generatorParameters = new Dictionary<string, object>
                                {
                                    { "type", VariousSolidTypes.UvHemisphere },
                                    { "x", oldPreset.PrismP },
                                    { "y", oldPreset.PrismP },
                                };
                                break;
                        }
                        break;
                }

                generatorParameters["ColorMethod"] = ColorMethods.ByRole;

                var operations = new List<PreviewPolyhedron.OpDefinition>();

                bool skipped = false;
                foreach (var oldOp in oldPreset.Ops)
                {
                    var newOp = new PreviewPolyhedron.OpDefinition();
                    PolyMesh.Operation opType;

                    if (oldOp.Disabled) continue;

                    if (oldOp.OpType == "FaceRotate") oldOp.OpType = "FaceRotateZ";
                    if (oldOp.OpType == "AddCopyX") oldOp.OpType = "DuplicateX";
                    if (oldOp.OpType == "AddCopyY") oldOp.OpType = "DuplicateY";
                    if (oldOp.OpType == "AddCopyZ") oldOp.OpType = "DuplicateZ";
                    if (oldOp.OpType == "AddMirrorX") oldOp.OpType = "MirrorX";
                    if (oldOp.OpType == "AddMirrorY") oldOp.OpType = "MirrorY";
                    if (oldOp.OpType == "AddMirrorZ") oldOp.OpType = "MirrorZ";
                    if (oldOp.OpType == "VertexFlex") oldOp.OpType = "VertexOffset";


                    if (oldOp.OpType == "Slice" ||
                        oldOp.OpType == "Stretch" ||
                        oldOp.OpType == "TagFaces" ||
                        oldOp.OpType == "Stash" ||
                        oldOp.OpType == "Unstash" ||
                        oldOp.OpType == "Hinge" ||
                        oldOp.OpType == "FaceMerge" ||
                        oldOp.OpType == "Stack"
                        )
                    {
                        warnings.Add($"Skipping {oldOp.OpType} on {fileInfo.Name}");
                        skipped = true;
                        continue;
                    }

                    if (oldOp.OpType == "FaceScale") oldOp.Amount += 1f;

                    if (oldOp.OpType == "FaceKeep")
                    {
                        oldOp.OpType = "FaceRemove";
                        newOp.filterNot = true;
                    }

                    if (Enum.TryParse(oldOp.OpType, true, out opType))
                    {
                        newOp.opType = opType;
                        newOp.amount = oldOp.Amount;
                        newOp.amountRandomize = oldOp.Randomize;
                        newOp.amount2 = oldOp.Amount2;
                        newOp.disabled = oldOp.Disabled;

                        switch (oldOp.FaceSelections)
                        {
                            case "All":
                                break;
                            case "ThreeSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 3;
                                break;
                            case "FourSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 4;
                                break;
                            case "FiveSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 5;
                                break;
                            case "SixSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 6;
                                break;
                            case "SevenSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 7;
                                break;
                            case "EightSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 8;
                                break;
                            case "NineSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 9;
                                break;
                            case "TenSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 10;
                                break;
                            case "ElevenSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 11;
                                break;
                            case "TwelveSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = 12;
                                break;
                            case "PSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = oldPreset.PrismP;
                                break;
                            case "QSided":
                                newOp.filterType = FilterTypes.NSided;
                                newOp.filterParamInt = oldPreset.PrismQ;
                                break;
                            case "EvenSided":
                                newOp.filterType = FilterTypes.EvenSided;
                                break;
                            case "OddSided":
                                newOp.filterType = FilterTypes.EvenSided;
                                newOp.filterNot = true;
                                break;
                            case "FacingUp":
                                newOp.filterType = FilterTypes.FacingUp;
                                newOp.filterParamFloat = 90;
                                break;
                            case "FacingStraightUp":
                                newOp.filterType = FilterTypes.FacingUp;
                                newOp.filterParamFloat = 1;
                                break;
                            case "FacingDown":
                                newOp.filterType = FilterTypes.FacingUp;
                                newOp.filterParamFloat = 90;
                                newOp.filterNot = true;
                                break;
                            case "FacingStraightDown":
                                newOp.filterType = FilterTypes.FacingUp;
                                newOp.filterParamFloat = 179;
                                newOp.filterNot = true;
                                break;
                            case "FacingForward":
                                newOp.filterType = FilterTypes.FacingForward;
                                newOp.filterParamFloat = 90;
                                break;
                            case "FacingBackward":
                                newOp.filterType = FilterTypes.FacingForward;
                                newOp.filterParamFloat = 90;
                                newOp.filterNot = true;
                                break;
                            case "FacingStraightForward":
                                newOp.filterType = FilterTypes.FacingForward;
                                newOp.filterParamFloat = 90;
                                break;
                            case "FacingStraightBackward":
                                newOp.filterType = FilterTypes.FacingForward;
                                newOp.filterParamFloat = 90;
                                newOp.filterNot = true;
                                break;
                            case "FacingLevel":
                                newOp.filterType = FilterTypes.FacingVertical;
                                newOp.filterParamFloat = 45;
                                newOp.filterNot = true;
                                break;
                            case "FacingCenter":
                                break;
                            case "FacingIn":
                                break;
                            case "FacingOut":
                                break;
                            case "Ignored":
                                newOp.filterType = FilterTypes.Role;
                                newOp.filterParamInt = (int)Roles.Ignored;
                                break;
                            case "Existing":
                                newOp.filterType = FilterTypes.Role;
                                newOp.filterParamInt = (int)Roles.Existing;
                                break;
                            case "New":
                                newOp.filterType = FilterTypes.Role;
                                newOp.filterParamInt = (int)Roles.New;
                                break;
                            case "NewAlt":
                                newOp.filterType = FilterTypes.Role;
                                newOp.filterParamInt = (int)Roles.NewAlt;
                                break;
                            case "AllNew":
                                newOp.filterType = FilterTypes.Role;
                                newOp.filterParamInt = (int)Roles.New;
                                break;
                            case "Odd":
                                newOp.filterType = FilterTypes.EveryNth;
                                newOp.filterParamInt = 2;
                                break;
                            case "Even":
                                newOp.filterType = FilterTypes.EveryNth;
                                newOp.filterParamInt = 2;
                                newOp.filterNot = true;
                                break;
                            case "OnlyFirst":
                                newOp.filterType = FilterTypes.FirstN;
                                newOp.filterParamInt = 1;
                                break;
                            case "ExceptFirst":
                                newOp.filterType = FilterTypes.FirstN;
                                newOp.filterParamInt = 1;
                                newOp.filterNot = true;
                                break;
                            case "OnlyLast":
                                newOp.filterType = FilterTypes.LastN;
                                newOp.filterParamInt = 1;
                                break;
                            case "ExceptLast":
                                newOp.filterType = FilterTypes.LastN;
                                newOp.filterParamInt = 1;
                                newOp.filterNot = true;
                                break;
                            case "Random":
                                newOp.filterType = FilterTypes.Random;
                                newOp.filterParamFloat = 0.5f;
                                break;
                            case "Inner":
                                newOp.filterType = FilterTypes.Inner;
                                break;
                            case "Outer":
                                newOp.filterType = FilterTypes.Inner;
                                newOp.filterNot = true;
                                break;
                            case "TopHalf":
                                newOp.filterType = FilterTypes.PositionY;
                                newOp.filterParamFloat = 0.5f;
                                break;
                            // case "Smaller":
                            //     break;
                            // case "Larger":
                            //     break;
                            case "None":
                                break;
                        }
                        operations.Add(newOp);
                    }
                    else
                    {
                        errors.Add($"Failed to parse: {oldOp.OpType} for {fileInfo.Name}");
                        skipped = true;
                    }
                }
                if (skipped) return;
                var recipe = new PolyRecipe
                {
                    Operators = operations,
                };
                var emd = new EditableModelDefinition(recipe);

                var jsonSerializer = new JsonSerializer
                {
                    ContractResolver = new CustomJsonContractResolver()
                };

                using (var textWriter = new StreamWriter($"{newPath}\\{presetName}.json"))
                using (var jsonWriter = new CustomJsonWriter(textWriter))
                {
                    jsonSerializer.Serialize(jsonWriter, emd);
                }

                string thumbnailSource = Path.Combine(fileInfo.Directory.ToString(), $"preset_{presetName}.jpg");
                string thumbNailDestination = Path.Combine(newPath, $"{presetName}.jpg");
                try
                {
                    File.Copy(thumbnailSource, thumbNailDestination);
                }
                catch (Exception e)
                {
                    warnings.Add($"Failed to copy thumbnail from {thumbnailSource} to {thumbNailDestination}");
                }
            }
        }
    }
}
