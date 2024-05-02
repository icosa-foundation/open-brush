using UnityEngine;
using UnityEditor;
using System.IO;
using Unity.Collections;

public static class ConvertNormalMaps
{
    [MenuItem("Open Brush/Toolkit/Convert Bump Maps to Normal Maps")]
    static void Convert()
    {
        foreach (Object obj in Selection.objects)
        {
            if (obj is Material)
            {
                Material material = (Material)obj;
                Shader shader = material.shader;
                int propertiesCount = ShaderUtil.GetPropertyCount(shader);

                for (int j = 0; j < propertiesCount; j++)
                {
                    if (ShaderUtil.GetPropertyType(shader, j) == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        string propertyName = ShaderUtil.GetPropertyName(shader, j);
                        Texture texture = material.GetTexture(propertyName);

                        if (texture is Texture2D normalMap)
                        {
                            string texturePath = AssetDatabase.GetAssetPath(normalMap);
                            bool isNormal = ReimportTexture(texturePath, true);
                            if (!isNormal) continue;
                            Texture2D remappedTexture = new Texture2D(normalMap.width, normalMap.height);

                            // Get the pixels from the source texture
                            NativeArray<Color32> pixels = normalMap.GetPixelData<Color32>(0);

                            for (int i = 0; i < pixels.Length; i++)
                            {
                                byte red = pixels[i].a;
                                byte green = pixels[i].g;
                                byte blue = 255; // pixels[i].b;
                                byte alpha = 255; // pixels[i].r;
                                pixels[i] = new Color32(red, green, blue, alpha);
                            }
                            remappedTexture.SetPixels32(pixels.ToArray());

                            // Apply changes
                            remappedTexture.Apply();

                            File.WriteAllBytes(texturePath, remappedTexture.EncodeToPNG());
                            Debug.Log("Exported Normal Map: " + texturePath);
                            RemoveConversion(texturePath);
                        }
                        else
                        {
                            Debug.LogWarning("No normal map found in material: " + material.name);
                        }
                    }
                }
            }
        }
        AssetDatabase.Refresh();
    }

    private static bool ReimportTexture(string assetPath, bool isReadable)
    {
        TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (textureImporter != null)
        {
            if (textureImporter.textureType != TextureImporterType.NormalMap)
            {
                return false;
            }
            textureImporter.isReadable = isReadable;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
        return true;
    }

    private static bool RemoveConversion(string assetPath)
    {
        TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (textureImporter != null)
        {
            if (textureImporter.textureType != TextureImporterType.NormalMap)
            {
                return false;
            }
            textureImporter.convertToNormalmap = false;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
        return true;
    }
}