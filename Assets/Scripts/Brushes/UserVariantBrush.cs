// Copyright 2020 The Open Brush Authors
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
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TiltBrush;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;

#endif

/// <summary>
/// A variant Brush based on an existing brush, but with different:
/// * Texture
/// * Icon
/// * Name
/// * (Optional) Sound
/// </summary>
public class UserVariantBrush
{
    public const string kConfigFile = "Brush.cfg";
    public const int kBrushDescriptionVersion = 1;

    /// <summary>
    /// Used to map fields in the BrushProperties to BrushDescriptor fields.
    /// </summary>
    public class MapTo : Attribute
    {
        public string FieldName { get; set; }
        public MapTo(string fieldName)
        {
            if (fieldName == "m_Description")
            {
                fieldName = "m_DescriptionOverride";
            }
            FieldName = fieldName;
        }
    }

    /// <summary>
    /// Used to mark a subclass of BrushProperties as a subsection.
    /// </summary>
    public class SubSection : Attribute { }

    /// <summary>
    /// How a brush may be shared.
    /// This controls whether a brush will be saved in to any sketches that use it, as well as whether
    /// the brush may be used or saved from a sketch.
    /// </summary>
    public enum CopyRestrictions
    {
        EmbedAndShare,
        EmbedAndDoNotShare,
        DoNotEmbed
    }

    /// <summary>
    /// This class is used to serialize in the brush data. Most of the fields have MapTo attributes
    /// attached, which means that they map directly to fields on BrushDescriptor.
    /// </summary>
    [Serializable]
    public class BrushProperties
    {
        [JsonProperty(Required = Required.Always)] public string VariantOf;
        [JsonProperty(Required = Required.Always)] public string GUID;
        public string Author;
        [JsonProperty(Required = Required.Always)] [MapTo("m_DurableName")] public string Name;
        [JsonProperty(Required = Required.Always)] [MapTo("m_Description")] public string Description;
        [JsonConverter(typeof(StringEnumConverter))] public CopyRestrictions CopyRestrictions;
        public string ButtonIcon;

        [Serializable]
        public class AudioProperties
        {
            [CanBeNull] string[] AudioClips;
            [MapTo("m_BrushAudioMaxPitchShift")] public float? MaxPitchShift;
            [MapTo("m_BrushAudioMaxVolume")] public float? MaxVolume;
            [MapTo("m_BrushVolumeUpSpeed")] public float? VolumeUpSpeed;
            [MapTo("m_BrushVolumeDownSpeed")] public float? VolumeDownSpeed;
            [MapTo("m_VolumeVelocityRangeMultiplier")] public float? VolumeVelocityRangeMultiplier;
            [MapTo("m_AudioReactive")] public bool? IsAudioReactive;
            [CanBeNull] private string ButtonAudio;
        }
        [CanBeNull] [SubSection] public AudioProperties Audio;

        [Serializable]
        public class MaterialProperties
        {
            [CanBeNull] public string Shader;
            public Dictionary<string, float> FloatProperties;
            public Dictionary<string, float[]> ColorProperties;
            public Dictionary<string, float[]> VectorProperties;
            public Dictionary<string, string> TextureProperties;
            [MapTo("m_TextureAtlasV")] public int? TextureAtlasV;
            [MapTo("m_TileRate")] public float? TileRate;
            [MapTo("m_UseBloomSwatchOnColorPicker")] public bool? UseBloomSwatchOnColorPicker;
        }
        [CanBeNull] [SubSection] public MaterialProperties Material;

        [Serializable]
        public class SizeProperties
        {
            [MapTo("m_BrushSizeRange")] public float[] BrushSizeRange;
            [MapTo("m_PressureSizeRange")] public float[] PressureSizeRange;
            [MapTo("m_SizeVariance")] public float? SizeVariance;
            [MapTo("m_PreviewPressureSizeMin")] public float? PreviewPressureSizeMin;
        }
        [CanBeNull] [SubSection] public SizeProperties Size;

        [Serializable]
        public class ColorProperties
        {
            [MapTo("m_Opacity")] public float? Opacity;
            [MapTo("m_PressureOpacityRange")] public float[] PressureOpacityRange;
            [MapTo("m_ColorLuminanceMin")] public float? LuminanceMin;
            [MapTo("m_ColorSaturationMax")] public float? SaturationMax;
        }
        [CanBeNull] [SubSection] public ColorProperties Color;

        [Serializable]
        public class ParticleProperties
        {
            [MapTo("m_ParticleSpeed")] public float? Speed;
            [MapTo("m_ParticleRate")] public float? Rate;
            [MapTo("m_ParticleInitialRotationRange")] public float? InitialRotationRange;
            [MapTo("m_RandomizeAlpha")] public bool? RandomizeAlpha;
        }
        [CanBeNull] [SubSection] public ParticleProperties Particle;

        [Serializable]
        public class QuadBatchProperties
        {
            [MapTo("m_SprayRateMultiplier")] public float? SprayRateMultiplier;
            [MapTo("m_RotationVariance")] public float? RotationVariance;
            [MapTo("m_PositionVariance")] public float? PositionVariance;
            [MapTo("m_SizeRatio")] public float[] SizeRatio;
        }
        [CanBeNull] [SubSection] public QuadBatchProperties QuadBatch;

        [Serializable]
        public class TubeProperties
        {
            [MapTo("m_SolidMinLengthMeters_PS")] public float? MinLength;
            [MapTo("m_TubeStoreRadiusInTexcoord0Z")] public bool? StoreRadiusInTexCoord;
        }
        [CanBeNull] [SubSection] public TubeProperties Tube;

        [Serializable]
        public class MiscProperties
        {
            [MapTo("m_RenderBackfaces")] public bool? RenderBackFaces;
            [MapTo("m_BackIsInvisible")] public bool? BackIsInvisible;
            [MapTo("m_BackfaceHueShift")] public float? BackfaceHueShift;
            [MapTo("m_BoundsPadding")] public float? BoundsPadding;
            [MapTo("m_PlayBackAtStrokeGranularity")] public bool? PlaybackAtStrokeGranularity;
        }
        [CanBeNull] [SubSection] public MiscProperties Misc;

        [Serializable]
        public class ExportProperties
        {
            [MapTo("m_EmissiveFactor")] public float? EmissiveFactor;
            [MapTo("m_AllowExport")] public bool? AllowExport;
        }
        [CanBeNull] [SubSection] public ExportProperties Export;

        [Serializable]
        public class SimplificationProperties
        {
            [MapTo("m_SupportsSimplification")] public bool? SupportsSimplification;
            [MapTo("m_HeadMinPoints")] public int? HeadMinPoints;
            [MapTo("m_HeadPointStep")] public int? HeadPointStep;
            [MapTo("m_TailMinPoints")] public int? TailMinPoints;
            [MapTo("m_TailPointStep")] public int? TailPointStep;
            [MapTo("m_MiddlePointStep")] public int? MiddlePointStep;
        }
        [CanBeNull] [SubSection] public SimplificationProperties Simplification;
        public int BrushDescriptionVersion = kBrushDescriptionVersion;
    }

    public BrushDescriptor Descriptor { get; private set; }
    public string Author { get; set; }
    public bool ShowInGUI => m_ShowInGUI;
    public bool EmbedInSketch => m_EmbedInSketch;

    public string Location => m_Location;

    private BrushProperties m_BrushProperties;
    private string m_ConfigData;
    private Dictionary<string, byte[]> m_FileData;
    private string m_Location;
    private bool m_ShowInGUI;
    private bool m_EmbedInSketch;

    private UserVariantBrush() { }

    /// <summary>
    /// Creates a User Variant Brush from a given source file or folder. Used for creating brushes
    /// from the user's Open Brush/Brushes folder.
    /// </summary>
    /// <param name="sourceFolder">Pathname of a folder or zip file containing the brush.</param>
    /// <returns>A created UserVariantBrush, or null if it couldn't be created.</returns>
    public static UserVariantBrush Create(string sourceFolder)
    {
        var brush = new UserVariantBrush();
        brush.m_Location = Path.GetFileName(sourceFolder);
        FolderOrZipReader brushFile = new FolderOrZipReader(sourceFolder);
        if (brushFile.IsZip)
        {
            string configDir = brushFile.Find(kConfigFile);
            if (configDir == null)
            {
                return null;
            }

            brushFile.SetRootFolder(Path.GetDirectoryName(configDir));
        }
        if (brush.Initialize(brushFile, forceInGui: true))
        {
            return brush;
        }
        return null;
    }

    /// <summary>
    /// Creates a user variant brush from a SceneFileInfo object. This is used to load a brush from
    /// inside a tilt file.
    /// </summary>
    /// <param name="fileInfo">The tile file.</param>
    /// <param name="subfolder">The folder within the tilt file containing the brush data</param>
    /// <returns>A created UserVariantBrush, or null if it couldn't be created.</returns>
    public static UserVariantBrush Create(SceneFileInfo fileInfo, string subfolder)
    {
        var brush = new UserVariantBrush();
        brush.m_Location = fileInfo.FullPath;
        FolderOrZipReader brushFile = new FolderOrZipReader(fileInfo.FullPath);
        brushFile.SetRootFolder(subfolder);
        string configDir = brushFile.Find(kConfigFile);
        if (configDir == null)
        {
            return null;
        }
        brushFile.SetRootFolder(Path.GetDirectoryName(configDir));
        if (brush.Initialize(brushFile))
        {
            return brush;
        }
        return null;
    }

    /// <summary>
    /// Initialize a brush from a file or folder.
    /// </summary>
    /// <param name="brushFile">The FolderOrZipReader used to read the brush.</param>
    /// <param name="forceInGui">Whether to force inclusion of the brush into the brushes panel.</param>
    /// <returns>Success or failure.</returns>
    private bool Initialize(FolderOrZipReader brushFile, bool forceInGui = false)
    {
        m_FileData = new Dictionary<string, byte[]>();
		
        if (!brushFile.Exists(kConfigFile))
        {
            return false;
        }

        string warning;
        try
        {
            using (var fileReader = new StreamReader(brushFile.GetReadStream(kConfigFile)))
            {
                m_ConfigData = fileReader.ReadToEnd();
                m_BrushProperties = App.DeserializeObjectWithWarning<BrushProperties>(m_ConfigData, out warning);
            }
        }
        catch (JsonException e)
        {
            Debug.Log($"Error reading {m_Location}/{kConfigFile}: {e.Message}");
            return false;
        }

        if (m_BrushProperties.BrushDescriptionVersion > kBrushDescriptionVersion)
        {
            Debug.LogError($"WARNING! This version of Open Brush supports version {kBrushDescriptionVersion} of User Brushes. " +
                           $"User Brush {m_BrushProperties} was made with version {m_BrushProperties.BrushDescriptionVersion}, " +
                           $"and may not work properly.");
        }

        if (!string.IsNullOrEmpty(warning))
        {
            Debug.Log($"Could not load brush at {m_Location}\n{warning}");
            return false;
        }

        var baseBrush = App.Instance.m_Manifest.Brushes.FirstOrDefault(
          x => x.m_Guid.ToString() == m_BrushProperties.VariantOf);

        if (baseBrush == null)
        {
            Debug.Log(
              $"In brush at {m_Location}, no brush named {m_BrushProperties.VariantOf} could be found.");
            return false;
        }

        if (App.Instance.m_Manifest.UniqueBrushes().Any(x => x.m_Guid.ToString() == m_BrushProperties.GUID))
        {
            Debug.Log(
              $"Cannot load brush at {m_Location} because its GUID matches {baseBrush.name}.");
            return false;
        }

        Descriptor = UnityEngine.Object.Instantiate(baseBrush);
        Descriptor.UserVariantBrush = this;
        Descriptor.m_Guid = new SerializableGuid(m_BrushProperties.GUID);
        Descriptor.BaseGuid = baseBrush.m_Guid;
        Descriptor.name = m_BrushProperties.Name;
        Descriptor.m_Supersedes = null;
        Descriptor.m_SupersededBy = null;
        Descriptor.m_HiddenInGui = !forceInGui &&
                                   m_BrushProperties.CopyRestrictions != CopyRestrictions.EmbedAndShare;
        m_EmbedInSketch = m_BrushProperties.CopyRestrictions != CopyRestrictions.DoNotEmbed;
        Author = m_BrushProperties.Author;

        if (!string.IsNullOrEmpty(m_BrushProperties.ButtonIcon))
        {
            Texture2D icon = LoadTexture(brushFile, m_BrushProperties.ButtonIcon);
            if (icon == null)
            {
                Debug.Log($"Brush at {m_Location} has no icon texture.");
                return false;
            }
            Descriptor.m_ButtonTexture = icon;
        }

        CopyPropertiesToDescriptor(m_BrushProperties, Descriptor);
        ApplyMaterialProperties(brushFile, m_BrushProperties.Material);

        return true;
    }

    /// <summary>
    /// Copies the fields in an object into a BrushDescriptor.
    /// This is primarily managed by whether or not the fields have a MapTo attribute.
    /// </summary>
    /// <param name="propertiesObject">An object to be copied from.</param>
    /// <param name="descriptor">The destination BrushDescriptor.</param>
    private void CopyPropertiesToDescriptor(System.Object propertiesObject, BrushDescriptor descriptor)
    {
        foreach (FieldInfo field in propertiesObject.GetType().GetFields())
        {
            object fieldValue = field.GetValue(propertiesObject);
            if (fieldValue == null)
            {
                continue;
            }
            MapTo mapTo = field.GetCustomAttributes<MapTo>(true).FirstOrDefault();
            if (mapTo != null)
            {
                FieldInfo descriptorField = typeof(BrushDescriptor).GetField(mapTo.FieldName);
                if (descriptorField == null)
                {
                    Debug.LogError(
                      $"Tried to set a value {mapTo.FieldName} on BrushDescriptor, but it doesn't exist!");
                    continue;
                }

                if (descriptorField.FieldType == typeof(Vector2))
                {
                    float[] floatArray = fieldValue as float[];
                    Vector2 vector = new Vector2(floatArray[0], floatArray[1]);
                    descriptorField.SetValue(descriptor, vector);
                }
                else
                {
                    descriptorField.SetValue(descriptor, fieldValue);
                }
            }
            else
            {
                if (field.GetCustomAttributes<SubSection>(true).Any())
                {
                    CopyPropertiesToDescriptor(fieldValue, descriptor);
                }
            }
        }
    }

    /// <summary>
    /// Applies material properties from BrushProperties to the User Brush.
    /// </summary>
    /// <param name="brushFile">FileOrZipReader to load textures from.</param>
    /// <param name="properties">Material properties to read from.</param>
    private void ApplyMaterialProperties(FolderOrZipReader brushFile,
        BrushProperties.MaterialProperties properties)
    {
        Descriptor.Material = new Material(Descriptor.Material);
        if (!string.IsNullOrEmpty(properties.Shader)
              && properties.Shader != Descriptor.Material.shader.name)
        {
            Shader shader = Shader.Find(properties.Shader);
            if (shader != null)
            {
                Descriptor.Material = new Material(shader);
            }
            else
            {
                Debug.LogError($"Cannot find shader {properties.Shader}.");
            }
        }

        if (properties.FloatProperties != null)
        {
            foreach (var item in properties.FloatProperties)
            {
                if (!Descriptor.Material.HasProperty(item.Key))
                {
                    Debug.LogError($"Material does not have property ${item.Key}.");
                    continue;
                }

                Descriptor.Material.SetFloat(item.Key, item.Value);
            }
        }

        if (properties.ColorProperties != null)
        {
            foreach (var item in properties.ColorProperties)
            {
                if (!Descriptor.Material.HasProperty(item.Key))
                {
                    Debug.LogError($"Material does not have property ${item.Key}.");
                    continue;
                }

                if (item.Value.Length != 4)
                {
                    Debug.LogError($"Color value {item.Key} in Material does not have four values.");
                    continue;
                }
                Color color = new Color(item.Value[0], item.Value[1], item.Value[2], item.Value[3]);
                Descriptor.Material.SetColor(item.Key, color);
            }
        }

        if (properties.VectorProperties != null)
        {
            foreach (var item in properties.VectorProperties)
            {
                if (!Descriptor.Material.HasProperty(item.Key))
                {
                    Debug.LogError($"Material does not have property ${item.Key}.");
                    continue;
                }

                if (item.Value.Length != 4)
                {
                    Debug.LogError($"Vector value {item.Key} in Material does not have four values.");
                    continue;
                }
                Vector4 vector = new Vector4(item.Value[0], item.Value[1], item.Value[2], item.Value[3]);
                Descriptor.Material.SetVector(item.Key, vector);
            }
        }

        if (properties.TextureProperties != null)
        {
            foreach (var item in properties.TextureProperties)
            {
                if (!Descriptor.Material.HasProperty(item.Key))
                {
                    Debug.LogError($"Material does not have property ${item.Key}.");
                    continue;
                }
                if (!string.IsNullOrEmpty(item.Value))
                {
                    Texture2D texture = LoadTexture(brushFile, item.Value);
                    if (texture != null)
                    {
                        Descriptor.Material.SetTexture(item.Key, texture);
                    }
                    else
                    {
                        Debug.LogError($"Couldn't load texture {item.Value} for material property {item.Key}.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Loads a texture from a FolderOrZipReader.
    /// </summary>
    /// <param name="brushFile">FolderOrZipReader to load the texture from.</param>
    /// <param name="filename">Texture filename.</param>
    /// <returns>A Texture2D, or null if it could not be loaded.</returns>
    private Texture2D LoadTexture(FolderOrZipReader brushFile, string filename)
    {
        if (brushFile.Exists(filename))
        {
            Texture2D texture = new Texture2D(16, 16);
            var buffer = new MemoryStream();
            using (var bufferStream = brushFile.GetReadStream(filename))
            {
                bufferStream.CopyTo(buffer);
            }
            byte[] data = buffer.ToArray();
            m_FileData[filename] = data;
            if (ImageConversion.LoadImage(texture, data, true))
            {
                texture.name = Path.GetFileNameWithoutExtension(filename);
                return texture;
            }
        }
        return null;
    }

    /// <summary>
    /// Saves the UserVariantBrush.
    /// </summary>
    /// <param name="writer">AtomicWriter to write to.</param>
    /// <param name="subfolder">Subfolder within the writer to write to.</param>
    public void Save(AtomicWriter writer, string subfolder)
    {
        string configPath = Path.Combine(subfolder, Path.Combine(m_Location, kConfigFile));
        using (var configStream = new StreamWriter(writer.GetWriteStream(configPath)))
        {
            configStream.Write(m_ConfigData);
        }

        foreach (var item in m_FileData)
        {
            string path = Path.Combine(subfolder, Path.Combine(m_Location, item.Key));
            using (var dataWriter = writer.GetWriteStream(path))
            {
                dataWriter.Write(item.Value, 0, item.Value.Length);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Static function to export all the standard Brush Descriptors to
    /// Support/Brush/ExportedProperties.
    /// </summary>

    [MenuItem("Tilt/Brushes/Duplicate Existing Brush")]
    public static void DoExportDuplicateDescriptor()
    {
        var absolutePath = EditorUtility.OpenFilePanel(
            "Choose a brush asset",
            Path.Combine(Application.dataPath, "Resources/Brushes"),
            "asset"
        );
        if (absolutePath.StartsWith(Application.dataPath))
        {
            var relativePath = absolutePath.Replace(Application.dataPath, "");
            relativePath = Path.Combine("Assets/", relativePath.Remove(0,1));
            var brush = AssetDatabase.LoadAssetAtPath<BrushDescriptor>(relativePath);
            ExportDuplicateDescriptor(brush, $"{brush.DurableName}Copy");
        }
    }

    
    [MenuItem("Tilt/Brushes/Export Standard Brush Properties")]
    public static void ExportDescriptorDetails()
    {
        TiltBrushManifest manifest =
          AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest.asset");

        string destination = Path.GetFullPath(
          Path.Combine(Application.dataPath, "../Support/Brushes/ExportedProperties"));
        if (!Directory.Exists(destination))
        {
            Directory.CreateDirectory(destination);
        }

        foreach (var brush in manifest.Brushes)
        {
            ExportDescriptor(brush, Path.Combine(destination, brush.name + ".txt"));
        }

        Debug.Log($"Exported {manifest.Brushes.Length} brushes.");
    }
    
#endif

    public static void SaveDescriptor(BrushDescriptor brush, string filename, Dictionary<string, string> textureRefs)
    {
        
        BrushProperties properties = brush.UserVariantBrush.m_BrushProperties;
        
        CopyDescriptorToProperties(brush, properties);
        CopyMaterialToProperties(brush, properties);
        
        // Copy textures and update texture paths
        brush.UserVariantBrush.SaveorCopyTextures(brush.Material.shader, textureRefs);

        try
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            serializer.ContractResolver = new CustomJsonContractResolver();
            using (var writer = new CustomJsonWriter(new StreamWriter(filename)))
            {
                writer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, properties);
            }
        }
        catch (JsonException e)
        {
            Debug.LogWarning(e.Message);
        }
    }
    
    /// <summary>
    /// Exports a single descriptor to a file.
    /// </summary>
    /// <param name="brush">The BrushDescriptor.</param>
    /// <param name="filename">Destination file path.</param>
    public static void ExportDescriptor(BrushDescriptor brush, string filename)
    {
        BrushProperties properties = new BrushProperties();
        properties.VariantOf = "";
        properties.GUID = brush.m_Guid.ToString();
        properties.ButtonIcon = "blank.png";
        properties.Author = "Open Brush";
        properties.CopyRestrictions = CopyRestrictions.EmbedAndShare;

        CopyDescriptorToProperties(brush, properties);
        CopyMaterialToProperties(brush, properties);

        try
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            serializer.ContractResolver = new CustomJsonContractResolver();
            using (var writer = new CustomJsonWriter(new StreamWriter(filename)))
            {
                writer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, properties);
            }
        }
        catch (JsonException e)
        {
            Debug.LogWarning(e.Message);
        }
    }
    
    public static string ExportDuplicateDescriptor(BrushDescriptor brush, string newBrushName)
    {
        var brushesPath = GetBrushesPath();
        
        BrushProperties properties = new BrushProperties();
        CopyDescriptorToProperties(brush, properties);
        CopyMaterialToProperties(brush, properties);
        
        string oldGuid = brush.m_Guid.ToString();
        string newGuid = Guid.NewGuid().ToString();
        
        string newBrushPath = Path.Combine(brushesPath, $"{newBrushName}_{newGuid}");
        if (!Directory.Exists(newBrushPath)) Directory.CreateDirectory(newBrushPath);
        string filename = Path.Combine(newBrushPath, "brush.cfg");

        properties.VariantOf = oldGuid;
        properties.GUID = newGuid;
        properties.Description = $"Based on {properties.Name}";
        properties.Name = newBrushName;
        properties.ButtonIcon = "";
        properties.Author = "";
        properties.CopyRestrictions = CopyRestrictions.EmbedAndShare;

        try
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            serializer.ContractResolver = new CustomJsonContractResolver();
            using (var writer = new CustomJsonWriter(new StreamWriter(filename)))
            {
                writer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, properties);
            }
        }
        catch (JsonException e)
        {
            Debug.LogWarning(e.Message);
        }

        return newBrushPath;
    }
    public static string GetBrushesPath()
    {
#if UNITY_EDITOR
        // TODO Refactor so we can reuse the logic in App.InitUserPath
        var userPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        var brushesPath = Path.Combine(userPath, "Open Brush", "Brushes");
#else
        var brushesPath = App.UserBrushesPath();
#endif
        return brushesPath;
    }

    /// <summary>
    /// Converts a Vector2 or Color struct to an array of floats.
    /// If the object is not a Vector2 or Color, it will return itself.
    /// </summary>
    /// <param name="obj">Object to convert.</param>
    /// <returns>The possibly-converted object.</returns>
    private static System.Object ConvertStructsToArrays(System.Object obj)
    {
        if (obj.GetType() == typeof(Vector2))
        {
            Vector2 vector = (Vector2)obj;
            obj = new[] { vector.x, vector.y };
        }
        else if (obj.GetType() == typeof(Color))
        {
            Color color = (Color)obj;
            obj = new[] { color.r, color.g, color.b, color.a };
        }

        return obj;
    }
    
    
    /// <summary>
    /// Copies the details of a BrushDescriptor to an object.
    /// </summary>
    /// <param name="descriptor">The BrushDescriptor.</param>
    /// <param name="propertiesObject">The destination object.</param>
    private static void CopyDescriptorToProperties(BrushDescriptor descriptor,
                                                   System.Object propertiesObject)
    {
        foreach (FieldInfo field in propertiesObject.GetType().GetFields())
        {
            try
            {
                MapTo mapTo = field.GetCustomAttributes<MapTo>(true).FirstOrDefault();
                if (mapTo != null)
                {
                    FieldInfo descriptorField = typeof(BrushDescriptor).GetField(mapTo.FieldName);
                    if (descriptorField == null)
                    {
                        Debug.LogError(
                          $"Tried to set a value {mapTo.FieldName} on BrushDescriptor, but it doesn't exist!");
                        continue;
                    }

                    System.Object fieldValue = ConvertStructsToArrays(descriptorField.GetValue(descriptor));
                    field.SetValue(propertiesObject, fieldValue);
                }
                else
                {
                    if (field.GetCustomAttributes<SubSection>(true).Any())
                    {
                        System.Object fieldValue = field.GetValue(propertiesObject);
                        if (fieldValue == null)
                        {
                            fieldValue = ConvertStructsToArrays(Activator.CreateInstance(field.FieldType));
                            field.SetValue(propertiesObject, fieldValue);
                        }

                        CopyDescriptorToProperties(descriptor, fieldValue);
                    }
                }
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"Trying to convert ${field.Name}. {e.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Copies material properties from a BrushDescriptor to properties object.
    /// </summary>
    /// <param name="descriptor">The BrushDescriptor.</param>
    /// <param name="properties">Target object.</param>
    private static void CopyMaterialToProperties(BrushDescriptor descriptor,
       BrushProperties properties)
    {
        Material material = descriptor.Material;
        properties.Material.Shader = material.shader.name;

        properties.Material.FloatProperties = new Dictionary<string, float>();
        properties.Material.ColorProperties = new Dictionary<string, float[]>();
        properties.Material.VectorProperties = new Dictionary<string, float[]>();
        properties.Material.TextureProperties = new Dictionary<string, string>();

        Shader shader = material.shader;

        for (int i = 0; i < shader.GetPropertyCount(); ++i)
        {
            string propertyName = shader.GetPropertyName(i);
            switch (shader.GetPropertyType(i))
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    properties.Material.FloatProperties.Add(propertyName, material.GetFloat(propertyName));
                    break;
                case ShaderPropertyType.Color:
                    Color color = material.GetColor(propertyName);
                    float[] colorArray = { color.r, color.g, color.b, color.a };
                    properties.Material.ColorProperties.Add(propertyName, colorArray);
                    break;
                case ShaderPropertyType.Vector:
                    Vector4 vector = material.GetVector(propertyName);
                    float[] floatArray = { vector.x, vector.y, vector.z, vector.w };
                    properties.Material.VectorProperties.Add(propertyName, floatArray);
                    break;
                case ShaderPropertyType.Texture:
                    properties.Material.TextureProperties.Add(propertyName, "");
                    break;
                default:
                    Debug.LogWarning($"Shader {shader.name} from material {material.name} has property {propertyName} of unsupported type {shader.GetPropertyType(i)}.");
                    break;
            }
        }
    }
    
    /// <summary>
    /// Fills in values for m_BrushProperties.Material.TextureProperties
    /// Also - if the texture is internal (i.e. in Resources) it saves it to the brush directory
    /// If the texture is external but from a different brush then it creates a copy in the brush directory
    /// Used when saving a user brush created at runtime 
    /// </summary>
    /// <param name="material">The BrushDescriptor.</param>
    /// <param name="textureRefs">A dictionary mapping texture property names to texture paths.</param>
    public void SaveorCopyTextures(Shader shader, Dictionary<string, string> textureRefs)
    {
        
        for (int i = 0; i < shader.GetPropertyCount(); ++i)
        {
            
            var propertyType = shader.GetPropertyType(i);
            string propertyName = shader.GetPropertyName(i);
            
            if (propertyType==ShaderPropertyType.Texture)
            {
                if (textureRefs.ContainsKey(propertyName))
                {
                    
                    string textureFullPath = textureRefs[propertyName];
                    var userPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                    
                    string thisUserBrushDir = Path.Combine(GetBrushesPath(), Location);
                    string textureName;
                    string newTextureFullPath;

                    if (textureFullPath.StartsWith(thisUserBrushDir))
                    {
                        // A texture that is already saved
                        newTextureFullPath = textureFullPath;
                    }
                    else if (textureFullPath.StartsWith(userPath))
                    {
                        // A texture from somewhere else in the Open Brush user folder. Copy it.
                        textureName = Path.GetFileName(textureFullPath + ".png");
                        newTextureFullPath = Path.Combine(thisUserBrushDir, textureName + ".png");
                        File.Copy(textureFullPath, newTextureFullPath);
                    }
                    else if (textureFullPath.StartsWith("__Resources__"))
                    {
                        // A built in texture. Save it to the user brush folder
                        Texture2D tex = (Texture2D)Descriptor.Material.GetTexture(propertyName);
                        // Can't EncodeToPNG for compressed textures
                        // TODO switch to GetPixelData/SetPixelData when we update Unity
                        var validTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, true);
                        validTex.SetPixels32(tex.GetPixels32());
                        validTex.Apply();
                        var bytes = ImageConversion.EncodeToPNG(validTex);
                        newTextureFullPath = Path.Combine(thisUserBrushDir, tex.name + ".png");
                        File.WriteAllBytes(newTextureFullPath, bytes);

                    }
                    else
                    {
                        Debug.LogError("User brush textures must be inside brush folder or brush resources folder");
                        return;
                    }
                    var textureRelativePath = newTextureFullPath.Substring(thisUserBrushDir.Length + 1);
                    m_BrushProperties.Material.TextureProperties[propertyName] = textureRelativePath;
                }
                else
                {
                    Debug.LogWarning($"No textureRef found for {propertyName}");
                }
            }
        }
    }
    
}
