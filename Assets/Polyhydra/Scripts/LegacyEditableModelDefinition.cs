using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Polyhydra.Core;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    [Serializable]
    internal class LegacyEditableModelDefinition
    {
        [JsonConstructor]
        public LegacyEditableModelDefinition(Vector3[] vertices, List<int>[] faces, List<Roles> faceRoles,
                                       List<Roles> vertexRoles, List<HashSet<string>> faceTags,
                                       Color[] colors, ColorMethods colorMethod, int materialIndex,
                                       GeneratorTypes generatorType, Dictionary<string, object> generatorParameters,
                                       List<Dictionary<string, object>> operations = null,
                                       float? autoSmoothAngle = null, string geometryFile = null,
                                       int? recipeVersion = null, List<Dictionary<string, object>> modifiers = null,
                                       GeometryPostProcessingSettings postProcessing = null)
        {
            Vertices = vertices;
            Faces = faces;
            FaceRoles = faceRoles;
            VertexRoles = vertexRoles;
            FaceTags = faceTags;
            Colors = colors;
            ColorMethod = colorMethod;
            MaterialIndex = materialIndex;
            GeneratorType = generatorType;
            RecipeVersion = PolyRecipeSerialization.CurrentVersion;
            int inputVersion = recipeVersion ?? 0;
            if (inputVersion == 0)
            {
                if (modifiers != null || postProcessing != null)
                    throw new JsonSerializationException("Current recipe fields require RecipeVersion.");
                GeneratorParameters = PolyRecipeSerialization.ReadLegacyGenerator(generatorType, generatorParameters);
                Modifiers = PolyRecipeSerialization.ReadLegacyModifiers(operations);
                PostProcessing = new GeometryPostProcessingSettings { AutoSmoothAngle = autoSmoothAngle ?? 0f };
            }
            else if (inputVersion == PolyRecipeSerialization.CurrentVersion)
            {
                if (operations != null || autoSmoothAngle.HasValue)
                    throw new JsonSerializationException("A current recipe cannot contain legacy modifier fields.");
                GeneratorParameters = generatorParameters;
                Modifiers = modifiers ?? new List<Dictionary<string, object>>();
                PostProcessing = postProcessing?.Copy() ?? new GeometryPostProcessingSettings();
            }
            else throw new NotSupportedException($"Unsupported 3D recipe version: {inputVersion}");
            GeometryFile = geometryFile;
        }

        public Color[] Colors { get; }
        public ColorMethods ColorMethod { get; }
        public int MaterialIndex { get; }
        public GeneratorTypes GeneratorType { get; }
        public Dictionary<string, object> GeneratorParameters { get; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string GeometryFile { get; internal set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Vector3[] Vertices { get; internal set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<int>[] Faces { get; internal set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Roles> FaceRoles { get; internal set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Roles> VertexRoles { get; internal set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<HashSet<string>> FaceTags { get; internal set; }
        public int RecipeVersion { get; }
        public List<Dictionary<string, object>> Modifiers { get; }
        public GeometryPostProcessingSettings PostProcessing { get; }

    }

}
