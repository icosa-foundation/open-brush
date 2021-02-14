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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TiltBrush;
using UnityEngine;

/// <summary>
/// A variant Brush based on an existing brush, but with different:
/// * Texture
/// * Icon
/// * Name
/// * (Optional) Sound
/// </summary>
[Serializable]
public class UserVariantBrush {
  public const string kConfigFile = "Brush.cfg";
  public const string kIconTexture = "icon";
  public const string kMainTexture = "main";
  public const string kNormalTexture = "normal";
  public readonly string[] kSupportedImageTypes = {"png", "jpg"};
  public readonly string kSoundFile = "sound.wav";

  [Serializable]
  public class Config {
    public string VariantOf;
    public string GUID;
    public string Name;
  }

  public BrushDescriptor Descriptor { get; private set; } = null;

  private const string kNormalMapName = "_BumpMap";
  private Config m_Config;
  
  private UserVariantBrush() {}

  public static UserVariantBrush Create(string sourceFolder) {
    var brush = new UserVariantBrush();
    if (brush.Initialize(sourceFolder)) {
      return brush;
    }
    return null;
  }

  private bool Initialize(string sourceFolder) {
    FileOrZip brushFile = new FileOrZip(sourceFolder);
    if (!brushFile.Exists(kConfigFile)) {
      return false;
    }

    string warning;
    try {
      var fileReader = new StreamReader(brushFile.GetReadStream(kConfigFile));
      m_Config = App.DeserializeObjectWithWarning<Config>(fileReader.ReadToEnd(), out warning);
    } catch (JsonReaderException e) {
      Debug.Log($"Error reading {sourceFolder}/{kConfigFile}: {e.Message}");
      return false;
    }

    if (!string.IsNullOrEmpty(warning)) {
      Debug.Log($"Could not load brush at {sourceFolder}\n{warning}");
      return false;
    }

    var baseBrush = App.Instance.m_Manifest.Brushes.FirstOrDefault(
      x => x.m_Guid.ToString() == m_Config.VariantOf);
    if (baseBrush == null) {
      baseBrush = App.Instance.m_Manifest.Brushes.FirstOrDefault(
        x => x.m_Description == m_Config.VariantOf);
    }

    if (baseBrush == null) {
      Debug.Log(
        $"In brush at {sourceFolder}, no brush named {m_Config.VariantOf} could be found.");
    }

    if (App.Instance.m_Manifest.UniqueBrushes().Any(x => x.m_Guid.ToString() == m_Config.GUID)) {
      Debug.Log(
        $"Cannot load brush at {sourceFolder} because its GUID matches {baseBrush.name}.");
    }

    Descriptor = UnityEngine.Object.Instantiate(baseBrush);
    Descriptor.m_DurableName = m_Config.Name;
    Descriptor.m_Description = m_Config.Name;
    Descriptor.m_Guid = new SerializableGuid(m_Config.GUID);
    Descriptor.name = m_Config.Name;
    Descriptor.IsUserVariant = true;
    Descriptor.m_Supersedes = null;
    Descriptor.m_SupersededBy = null;

    Texture2D icon = LoadTexture(brushFile, kIconTexture);
    if (icon == null) {
      Debug.Log($"Brush at {sourceFolder} has no icon texture.");
      return false;
    }

    Descriptor.m_ButtonTexture = icon;
    
    Descriptor.Material = new Material(Descriptor.Material);

    Texture2D main = LoadTexture(brushFile, kMainTexture);
    if (main) {
      Descriptor.Material.mainTexture = main;
    }

    Texture2D normal = LoadTexture(brushFile, kNormalTexture);
    if (normal) {
      if (Descriptor.Material.HasProperty(kNormalMapName)) {
        Descriptor.Material.SetTexture(kNormalMapName, normal);
      }
    }

    return true;
  }

  private Texture2D LoadTexture(FileOrZip brushFile, string baseName) {
    foreach (var extension in kSupportedImageTypes) {
      string filename = $"{baseName}.{extension}";
      if (brushFile.Exists(filename)) {
        Texture2D texture = new Texture2D(16, 16);
        var buffer = new MemoryStream();
        brushFile.GetReadStream(filename).CopyTo(buffer);
        if (ImageConversion.LoadImage(texture, buffer.ToArray(), true)) {
          return texture;
        }
      }
    }
    return null;
  }
  
  
}
