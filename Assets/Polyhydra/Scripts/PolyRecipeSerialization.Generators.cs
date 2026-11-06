using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    internal static partial class PolyRecipeSerialization
    {
        public static string UpgradePresetPath(string path, PolyRecipe recipe)
        {
            path = UpgradeLegacyPath(path);
            const string oldParameters = "$.GeneratorParameters.";
            if (path.StartsWith(oldParameters, StringComparison.Ordinal))
            {
                string name = path.Substring(oldParameters.Length);
                if ((string)recipe.SourceDefinition?.Recipe.ToJson()["Generator"]?["Type"] == "capsule")
                {
                    if (name == "x") name = "sides";
                    else if (name == "y") name = "capSegments";
                    else if (name == "z") name = "cylinderHeight";
                }
                return $"$.Recipe.Generator.Parameters.{name}";
            }
            if (path.StartsWith("$.Recipe.", StringComparison.Ordinal)) return path;
            return $"$.Recipe{path.Substring(1)}";
        }

        public static bool IsCoreGenerator(string type)
            => type == "panel" || type == "rectangle" || type == "frame" ||
               type == "cylinder" || type == "capsule" || type == "conic";

        private static string Name<T>(object value) where T : struct, Enum
        {
            var parsed = (T)Enum.ToObject(typeof(T), Convert.ToInt32(value));
            if (!Enum.IsDefined(typeof(T), parsed)) throw new NotSupportedException($"Unknown {typeof(T).Name}: {value}");
            return parsed.ToString();
        }

        private static T Named<T>(JToken value) where T : struct, Enum
        {
            string name = (string)value;
            if (name == null || !Enum.TryParse(name, out T result) || !Enum.IsDefined(typeof(T), result) || result.ToString() != name)
                throw new NotSupportedException($"Unknown {typeof(T).Name}: {name}");
            return result;
        }

        private static string LowerFirst(string value) => $"{char.ToLowerInvariant(value[0])}{value.Substring(1)}";
        private static string UpperFirst(string value) => $"{char.ToUpperInvariant(value[0])}{value.Substring(1)}";

        private static JObject Generator(GeneratorTypes type, Dictionary<string, object> source)
        {
            var p = JObject.FromObject(source ?? new Dictionary<string, object>());
            string name;
            switch (type)
            {
                case GeneratorTypes.Uniform:
                    name = "uniform"; p["solid"] = Name<UniformTypes>(source["type"]); p.Remove("type"); break;
                case GeneratorTypes.Radial:
                    name = "radial"; p["solid"] = Name<RadialSolids.RadialPolyType>(source["type"]); p.Remove("type"); break;
                case GeneratorTypes.RegularGrids:
                case GeneratorTypes.CatalanGrids:
                case GeneratorTypes.OneUniformGrids:
                case GeneratorTypes.TwoUniformGrids:
                case GeneratorTypes.DurerGrids:
                    name = "grid"; p["family"] = type.ToString();
                    p["type"] = Name<GridEnums.GridTypes>(source["type"]);
                    p["shape"] = Name<GridEnums.GridShapes>(source["shape"]); break;
                case GeneratorTypes.Shapes:
                    name = LowerFirst(Name<ShapeTypes>(source["type"])); p.Remove("type"); break;
                case GeneratorTypes.Various:
                    var solid = (VariousSolidTypes)Convert.ToInt32(source["type"]);
                    name = LowerFirst(Name<VariousSolidTypes>(source["type"])); p.Remove("type");
                    if (solid == VariousSolidTypes.Capsule)
                    {
                        p = new JObject { ["sides"] = p["x"], ["capSegments"] = p["y"],
                            ["cylinderHeight"] = p["z"], ["radius"] = 1f };
                    }
                    break;
                case GeneratorTypes.Waterman: name = "waterman"; break;
                case GeneratorTypes.FileSystem:
                case GeneratorTypes.GeometryData: name = "mesh"; break;
                default: throw new NotSupportedException($"Unsupported legacy generator: {type}");
            }
            return new JObject { ["Type"] = name, ["Parameters"] = p };
        }

        private static (GeneratorTypes, Dictionary<string, object>) LegacyGenerator(JObject generator)
        {
            string type = (string)generator["Type"];
            var p = generator["Parameters"] as JObject ?? new JObject();
            p = (JObject)p.DeepClone();
            GeneratorTypes category;
            switch (type)
            {
                case "uniform": category = GeneratorTypes.Uniform; p["type"] = (int)Named<UniformTypes>(p["solid"]); p.Remove("solid"); break;
                case "radial": category = GeneratorTypes.Radial; p["type"] = (int)Named<RadialSolids.RadialPolyType>(p["solid"]); p.Remove("solid"); break;
                case "grid":
                    category = Named<GeneratorTypes>(p["family"]); p.Remove("family");
                    if (category != GeneratorTypes.RegularGrids && category != GeneratorTypes.CatalanGrids &&
                        category != GeneratorTypes.OneUniformGrids && category != GeneratorTypes.TwoUniformGrids &&
                        category != GeneratorTypes.DurerGrids) throw new InvalidDataException("Invalid grid family");
                    p["type"] = (int)Named<GridEnums.GridTypes>(p["type"]);
                    p["shape"] = (int)Named<GridEnums.GridShapes>(p["shape"]); break;
                case "capsule":
                    category = GeneratorTypes.Various;
                    p = new JObject { ["type"] = (int)VariousSolidTypes.Capsule, ["x"] = p["sides"] ?? 24,
                        ["y"] = p["capSegments"] ?? 8, ["z"] = p["cylinderHeight"] ?? 2f }; break;
                case "waterman": category = GeneratorTypes.Waterman; break;
                case "mesh": category = GeneratorTypes.GeometryData; break;
                default:
                    if (IsCoreGenerator(type)) return (GeneratorTypes.Procedural, new Dictionary<string, object>());
                    if (Enum.TryParse(UpperFirst(type), out ShapeTypes shape) && Enum.IsDefined(typeof(ShapeTypes), shape))
                    { category = GeneratorTypes.Shapes; p["type"] = (int)shape; }
                    else if (Enum.TryParse(UpperFirst(type), out VariousSolidTypes various) && Enum.IsDefined(typeof(VariousSolidTypes), various))
                    { category = GeneratorTypes.Various; p["type"] = (int)various; }
                    else throw new NotSupportedException($"Unknown generator: {type}");
                    break;
            }
            return (category, p.ToObject<Dictionary<string, object>>());
        }

        private static float LegacyOutputScale(GeneratorTypes type, Dictionary<string, object> p)
        {
            float scale = 1f;
            if (type == GeneratorTypes.Uniform) scale = .864f;
            else if (type == GeneratorTypes.Radial || type == GeneratorTypes.RegularGrids || type == GeneratorTypes.CatalanGrids ||
                     type == GeneratorTypes.OneUniformGrids || type == GeneratorTypes.TwoUniformGrids || type == GeneratorTypes.DurerGrids)
                scale = Mathf.Sqrt(2f) / 2f;
            else if (type == GeneratorTypes.Shapes)
            {
                var shape = (ShapeTypes)Convert.ToInt32(p.GetValueOrDefault("type"));
                if (shape == ShapeTypes.Polygon || shape == ShapeTypes.Star)
                    scale = 1f / (2f * Mathf.Sin(Mathf.PI / Mathf.Max(3, Convert.ToInt32(p.GetValueOrDefault("sides")))));
            }
            else if (type == GeneratorTypes.Various)
            {
                var solid = (VariousSolidTypes)Convert.ToInt32(p.GetValueOrDefault("type"));
                scale = solid == VariousSolidTypes.Box || solid == VariousSolidTypes.Torus || solid == VariousSolidTypes.Stairs
                    ? 1f / Mathf.Sqrt(2f) : .5f;
            }
            return scale / App.METERS_TO_UNITS;
        }

        public static GeometryRecipeData ToRecipe(PolyRecipe p)
        {
            JObject source = p.SourceDefinition?.Recipe.ToJson();
            JObject generator;
            if (p.GeneratorType == GeneratorTypes.Procedural)
                generator = (JObject)(source?["Generator"]?.DeepClone() ?? throw new InvalidDataException("Missing procedural generator"));
            else generator = Generator(p.GeneratorType, ControlParameters(p));
            bool sameGenerator = (string)source?["Generator"]?["Type"] == (string)generator["Type"];
            if (sameGenerator)
            {
                var merged = (JObject)(source["Generator"]["Parameters"]?.DeepClone() ?? new JObject());
                foreach (var property in ((JObject)generator["Parameters"]).Properties())
                {
                    // The 3D generator UI has no radius control for capsules yet.
                    if ((string)generator["Type"] == "capsule" && property.Name == "radius") continue;
                    merged[property.Name] = property.Value.DeepClone();
                }
                generator["Parameters"] = merged;
            }
            var post = JObject.FromObject(p.PostProcessing ?? new GeometryPostProcessingSettings());
            if (p.SourceDefinition == null || !sameGenerator)
                post["OutputScale"] = LegacyOutputScale(p.GeneratorType, ControlParameters(p));
            var modifiers = new JArray();
            var operators = new Queue<Dictionary<string, object>>(WriteModifiers(p.Operators));
            if (source?["Modifiers"] is JArray original)
            {
                foreach (JObject step in original)
                {
                    if ((string)step["Type"] != "operator") modifiers.Add(step.DeepClone());
                    else if (operators.Count > 0) modifiers.Add(JObject.FromObject(operators.Dequeue()));
                }
            }
            while (operators.Count > 0) modifiers.Add(JObject.FromObject(operators.Dequeue()));
            return new GeometryRecipeData(new JObject { ["Version"] = 1, ["Generator"] = generator,
                ["Modifiers"] = modifiers, ["PostProcessing"] = post });
        }

        public static EditableModelDefinitionData ToDefinition(PolyRecipe p)
        {
            var appearance = (JObject)(p.SourceDefinition?.Appearance?.DeepClone() ?? new JObject());
            appearance["ColorMethod"] = p.ColorMethod.ToString();
            appearance["MaterialIndex"] = p.MaterialIndex;
            if (p.Colors != null) appearance["Colors"] = new JArray(p.Colors.Select(c => new JArray(c.r, c.g, c.b, c.a)));
            var result = new EditableModelDefinitionData(ToRecipe(p), appearance)
            {
                ExtraData = p.SourceDefinition?.ExtraData?.ToDictionary(e => e.Key, e => e.Value?.DeepClone()),
                SourceFiles = p.SourceDefinition == null ? new Dictionary<string, byte[]>()
                    : new Dictionary<string, byte[]>(p.SourceDefinition.SourceFiles)
            };
            if (p.GeneratorType == GeneratorTypes.GeometryData || p.GeneratorType == GeneratorTypes.FileSystem)
            {
                result.ExtraData ??= new Dictionary<string, JToken>();
                result.ExtraData["Geometry"] = Geometry(p.Vertices, p.Faces, p.FaceRoles, p.VertexRoles, p.FaceTags, null);
            }
            return result;
        }

        private static JObject Geometry(IList<Vector3> vertices, IEnumerable<List<int>> faces, IEnumerable<int> faceRoles,
            IEnumerable<int> vertexRoles, IEnumerable<HashSet<string>> faceTags, string file)
        {
            var data = new JObject();
            if (vertices != null) data["Vertices"] = new JArray(vertices.Select(v => new JArray(v.x, v.y, v.z)));
            if (faces != null) data["Faces"] = new JArray(faces.Select(f => new JArray(f)));
            if (faceRoles != null) data["FaceRoles"] = new JArray(faceRoles);
            if (vertexRoles != null) data["VertexRoles"] = new JArray(vertexRoles);
            if (faceTags != null) data["FaceTags"] = new JArray(faceTags.Select(tags => new JArray(tags ?? new HashSet<string>())));
            if (file != null) data["GeometryFile"] = file;
            return data;
        }

        public static void StoreGeometry(EditableModelDefinitionData definition, LegacyEditableModelDefinition data)
        {
            definition.ExtraData ??= new Dictionary<string, JToken>();
            definition.ExtraData["Geometry"] = Geometry(data.Vertices, data.Faces,
                data.FaceRoles?.Select(r => (int)r), data.VertexRoles?.Select(r => (int)r), data.FaceTags, data.GeometryFile);
        }

        public static LegacyEditableModelDefinition LegacyView(EditableModelDefinitionData definition)
        {
            JObject recipe = definition.Recipe.ToJson();
            var (type, parameters) = LegacyGenerator((JObject)recipe["Generator"]);
            var appearance = definition.Appearance;
            JToken geometryToken = null;
            definition.ExtraData?.TryGetValue("Geometry", out geometryToken);
            var geometry = geometryToken as JObject;
            var serializer = new JsonSerializer { ContractResolver = new CustomJsonContractResolver() };
            var colorMethod = appearance?["ColorMethod"];
            var method = colorMethod == null ? default : colorMethod.Type == JTokenType.String
                ? Named<ColorMethods>(colorMethod) : (ColorMethods)colorMethod.Value<int>();
            return new LegacyEditableModelDefinition(
                geometry?["Vertices"]?.ToObject<Vector3[]>(serializer), geometry?["Faces"]?.ToObject<List<int>[]>(),
                geometry?["FaceRoles"]?.ToObject<List<Roles>>(), geometry?["VertexRoles"]?.ToObject<List<Roles>>(),
                geometry?["FaceTags"]?.ToObject<List<HashSet<string>>>(),
                appearance?["Colors"]?.ToObject<Color[]>(serializer), method, (int?)appearance?["MaterialIndex"] ?? 0,
                type, parameters, geometryFile: (string)geometry?["GeometryFile"], recipeVersion: 1,
                modifiers: (recipe["Modifiers"] as JArray ?? new JArray()).OfType<JObject>()
                    .Where(m => (string)m["Type"] == "operator").Select(m => m.ToObject<Dictionary<string, object>>()).ToList(),
                postProcessing: recipe["PostProcessing"]?.ToObject<GeometryPostProcessingSettings>());
        }

        public static EditableModelDefinitionData ReadDefinition(JObject json, JsonSerializer serializer)
        {
            if (json["Recipe"] != null) return EditableModelDefinitionData.FromJson(json);
            if (json["Version"] != null) return new EditableModelDefinitionData(new GeometryRecipeData(json));
            var legacy = json.ToObject<LegacyEditableModelDefinition>(serializer);
            if (legacy == null) throw new JsonSerializationException("Missing legacy shape definition");
            var post = JObject.FromObject(legacy.PostProcessing);
            post["OutputScale"] = legacy.PostProcessing.OutputScale * LegacyOutputScale(legacy.GeneratorType, legacy.GeneratorParameters);
            var appearance = new JObject { ["ColorMethod"] = legacy.ColorMethod.ToString(), ["MaterialIndex"] = legacy.MaterialIndex };
            if (legacy.Colors != null) appearance["Colors"] = new JArray(legacy.Colors.Select(c => new JArray(c.r, c.g, c.b, c.a)));
            var result = new EditableModelDefinitionData(new GeometryRecipeData(new JObject
            {
                ["Version"] = 1, ["Generator"] = Generator(legacy.GeneratorType, legacy.GeneratorParameters),
                ["Modifiers"] = JArray.FromObject(legacy.Modifiers), ["PostProcessing"] = post
            }), appearance);
            if (legacy.Vertices != null || legacy.GeometryFile != null) StoreGeometry(result, legacy);
            return result;
        }
    }

    internal sealed class LegacyEditableModelDefinitionDataConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanConvert(Type type) => type == typeof(EditableModelDefinitionData);
        public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
            => PolyRecipeSerialization.ReadDefinition(GeometryRecipeData.LoadObject(reader), serializer);
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => throw new NotSupportedException();
    }
}
