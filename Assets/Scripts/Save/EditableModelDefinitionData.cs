// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TiltBrush.DocumentImport;

namespace TiltBrush
{
    // Shared saved definition. Geometry evaluation consumes Recipe; appearance and
    // additional source data survive loading even when this application cannot use them.
    [JsonConverter(typeof(EditableModelDefinitionDataJsonConverter))]
    public sealed class EditableModelDefinitionData
    {
        public GeometryRecipeData Recipe { get; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public JObject Appearance { get; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }

        [JsonIgnore]
        public Dictionary<string, byte[]> SourceFiles { get; set; } = new Dictionary<string, byte[]>();

        [JsonIgnore]
        public string GeometryFile
        {
            get
            {
                if (ExtraData == null || !ExtraData.TryGetValue("Geometry", out var geometry)) { return null; }
                return geometry is JObject source && source["GeometryFile"]?.Type == JTokenType.String
                    ? (string)source["GeometryFile"] : null;
            }
        }

        [JsonConstructor]
        public EditableModelDefinitionData(GeometryRecipeData recipe, JObject appearance = null)
        {
            Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            Appearance = (JObject)appearance?.DeepClone();
        }

        public EditableModelDefinitionData WithRecipe(GeometryRecipeData recipe)
            => new EditableModelDefinitionData(recipe, Appearance)
            {
                ExtraData = ExtraData?.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone()),
                SourceFiles = new Dictionary<string, byte[]>(SourceFiles)
            };

        public static EditableModelDefinitionData FromJson(JObject data)
        {
            if (!(data["Recipe"] is JObject recipe)) { throw new JsonSerializationException("Missing geometry recipe"); }
            var appearance = data["Appearance"];
            if (appearance != null && appearance.Type != JTokenType.Null && !(appearance is JObject))
            {
                throw new JsonSerializationException("Shape appearance must be an object");
            }
            return new EditableModelDefinitionData(new GeometryRecipeData(recipe), appearance as JObject)
            {
                ExtraData = data.Properties().Where(p => p.Name != "Recipe" && p.Name != "Appearance")
                    .ToDictionary(p => p.Name, p => p.Value.DeepClone())
            };
        }

        public void LoadGeometryFile(Func<string, Stream> open)
        {
            string file = GeometryFile;
            if (file == null || SourceFiles.ContainsKey(file)) { return; }
            if (!file.StartsWith("editable-model-", StringComparison.Ordinal) ||
                !file.EndsWith(".bin", StringComparison.Ordinal) || Path.GetFileName(file) != file)
            {
                throw new InvalidDataException("Invalid editable geometry filename");
            }
            using var stream = open(file) ?? throw new InvalidDataException($"Missing editable geometry: {file}");
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            SourceFiles.Add(file, copy.ToArray());
        }

        // A host may not understand a generator, but must still retain its referenced
        // binary source when saving the unchanged definition into another archive.
        public static void CollectGeometryFiles(IEnumerable<EditableModelDefinitionData> definitions,
            IDictionary<string, byte[]> output)
        {
            if (definitions == null) { return; }
            foreach (var definition in definitions)
            {
                string file = definition.GeometryFile;
                if (file == null || output.ContainsKey(file)) { continue; }
                if (!definition.SourceFiles.TryGetValue(file, out var bytes))
                {
                    throw new InvalidDataException($"Missing editable geometry source: {file}");
                }
                output.Add(file, bytes);
            }
        }
    }

    public sealed class EditableModelDefinitionDataJsonConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanConvert(Type type) => type == typeof(EditableModelDefinitionData);
        public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
            => EditableModelDefinitionData.FromJson(GeometryRecipeData.LoadObject(reader));
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            => throw new NotSupportedException();
    }

}
