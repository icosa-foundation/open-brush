// Copyright 2020 The Tilt Brush Authors
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
using System.Linq;
using UnityEngine;

using Brush = TiltBrush.BrushDescriptor;

namespace TiltBrush {

[System.Serializable]
public struct BlocksMaterial {
  public Brush brushDescriptor;
}

public class BrushCatalog : MonoBehaviour {
  static public BrushCatalog m_Instance;

#if UNITY_EDITOR
  /// Pass a GameObject to receive the newly-created singleton BrushCatalog
  /// Useful for unit tests because a ton of Tilt Brush uses GetBrush(Guid).
  /// TODO: change TB to use BrushDescriptor directly rather than indirect through Guids
  public static void UnitTestSetUp(GameObject container) {
    Debug.Assert(m_Instance == null);
    m_Instance = container.AddComponent<BrushCatalog>();

    // For unit testing, probably best to have all the descriptors available,
    // rather than just a subset of them that are in a manifest.
    m_Instance.m_GuidToBrush = UnityEditor.AssetDatabase.FindAssets("t:BrushDescriptor")
        .Select(name => UnityEditor.AssetDatabase.LoadAssetAtPath<BrushDescriptor>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(name)))
        .ToDictionary(desc => (Guid)desc.m_Guid);
  }

  /// The inverse of UnitTestSetUp
  public static void UnitTestTearDown(GameObject container) {
    Debug.Assert(m_Instance == container.GetComponent<BrushCatalog>());
    m_Instance = null;
  }
#endif

  public event Action BrushCatalogChanged;
  public Texture2D m_GlobalNoiseTexture;

  [SerializeField] private Brush m_DefaultBrush;
  private bool m_IsLoading;
  private Dictionary<Guid, Brush> m_GuidToBrush;
  private HashSet<Brush> m_AllBrushes;
  private List<Brush> m_GuiBrushList;
  private Dictionary<Guid, Brush> m_SceneBrushes;

  [SerializeField] public BlocksMaterial[] m_BlocksMaterials;
  private Dictionary<Material, Brush> m_MaterialToBrush;

  public bool IsLoading { get { return m_IsLoading; } }
  public Brush GetBrush(Guid guid) {
    Brush brush;
    if (m_GuidToBrush.TryGetValue(guid, out brush)) {
      return brush;
    }
    if (m_SceneBrushes.TryGetValue(guid, out brush)) {
      return brush;
    }
    return null;
  }
  public Brush DefaultBrush => m_DefaultBrush;

  public IEnumerable<Brush> AllBrushes => m_AllBrushes;
  
  public List<Brush> GuiBrushList => m_GuiBrushList;

  void Awake() {
    m_Instance = this;
    m_GuidToBrush = new Dictionary<Guid, Brush>();
    m_MaterialToBrush = new Dictionary<Material, Brush>();
    m_AllBrushes = new HashSet<Brush>();
    m_GuiBrushList = new List<Brush>();
    m_SceneBrushes = new Dictionary<Guid, BrushDescriptor>();

    // Move blocks materials in to a dictionary for quick lookup.
    for (int i = 0; i < m_BlocksMaterials.Length; ++i) {
      m_MaterialToBrush.Add(m_BlocksMaterials[i].brushDescriptor.Material,
                            m_BlocksMaterials[i].brushDescriptor);
    }

    Shader.SetGlobalTexture("_GlobalNoiseTexture", m_GlobalNoiseTexture);
  }

  /// Begins reloading any brush assets that come from loose files.
  /// The "BrushCatalogChanged" event will be fired when this is complete.
  public void BeginReload() {
    m_IsLoading = true;

    // Recreate m_GuidToBrush
    {
      var manifestBrushes = LoadBrushesInManifest();
      manifestBrushes.Add(DefaultBrush);

      m_GuidToBrush.Clear();
      m_AllBrushes = null;

      foreach (var brush in manifestBrushes) {
        Brush tmp;
        if (m_GuidToBrush.TryGetValue(brush.m_Guid, out tmp) && tmp != brush) {
          Debug.LogErrorFormat("Guid collision: {0}, {1}", tmp, brush);
          continue;
        }
        m_GuidToBrush[brush.m_Guid] = brush;
      }

      // Add reverse links to the brushes
      // Auto-add brushes as compat brushes
      foreach (var brush in manifestBrushes) { brush.m_SupersededBy = null; }
      foreach (var brush in manifestBrushes) {
        var older = brush.m_Supersedes;
        if (older == null) { continue; }
        // Add as compat
        if (!m_GuidToBrush.ContainsKey(older.m_Guid)) {
          m_GuidToBrush[older.m_Guid] = older;
          older.m_HiddenInGui = true;
        }
        // Set reverse link
        if (older.m_SupersededBy != null) {
          // No need to warn if the superseding brush is the same
          if (older.m_SupersededBy.name != brush.name) {
            Debug.LogWarningFormat(
                "Unexpected: {0} is superseded by both {1} and {2}",
                older.name, older.m_SupersededBy.name, brush.name);
          }
        } else {
          older.m_SupersededBy = brush;
        }
      }

      m_AllBrushes = new HashSet<Brush>(m_GuidToBrush.Values);
    }

    // Postprocess: put brushes into parse-friendly list
    m_GuiBrushList = m_GuidToBrush.Values.Where(x => !x.m_HiddenInGui).ToList();
  }

  void Update() {
    if (m_IsLoading) {
      m_IsLoading = false;
      Resources.UnloadUnusedAssets();
      if (BrushCatalogChanged != null) {
        BrushCatalogChanged();
      }
    }
  }

  // Returns brushes in both sections of the manifest (compat and non-compat)
  // Brushes that are found only in the compat section will have m_HiddenInGui = true
  static private List<Brush> LoadBrushesInManifest() {
    List<Brush> output = new List<Brush>();
    var manifest = App.Instance.m_Manifest;
    foreach (var desc in manifest.Brushes) {
      if (desc != null) {
        output.Add(desc);
      }
    }

    // Additional hidden brushes
    var hidden = manifest.CompatibilityBrushes.Except(manifest.Brushes);
    foreach (var desc in hidden) {
      if (desc != null) {
        desc.m_HiddenInGui = true;
        output.Add(desc);
      }
    }
    
    // Add user variant brushes
    output.AddRange(manifest.UserVariantBrushDescriptors);
    return output;
  }

  public void DuplicateBrush(Guid fromGuid, Guid toGuid) {
    BrushDescriptor from = GetBrush(fromGuid);
    if (!from) {
      Debug.LogWarning($"Error! Brush {from} not found!");
      from = m_DefaultBrush;
    }

    BrushDescriptor to = Instantiate(from);
    to.m_Guid = toGuid;
    to.BaseGuid = fromGuid;
    to.m_Supersedes = null;
    to.m_SupersededBy = null;
    m_GuidToBrush[toGuid] = to;
    m_AllBrushes.Add(to);
    // Do not add to the Gui brushes, as this is used for missing brushes.
  }

  public void AddBrush(BrushDescriptor brush) {
    m_AllBrushes.RemoveWhere(x => x.m_Guid == brush.m_Guid);
    m_GuiBrushList.RemoveAll(x => x.m_Guid == brush.m_Guid);
    m_GuidToBrush[brush.m_Guid] = brush;
    m_AllBrushes.Add(brush);
    m_GuiBrushList.Add(brush);
  }

  public void AddSceneBrush(BrushDescriptor brush) {
    m_AllBrushes.RemoveWhere(x => x.m_Guid == brush.m_Guid);
    m_GuiBrushList.RemoveAll(x => x.m_Guid == brush.m_Guid);
    m_SceneBrushes[brush.m_Guid] = brush;
    m_AllBrushes.Add(brush);
    m_GuiBrushList.Add(brush);
    m_IsLoading = true;
  }
  
  public void ClearSceneBrushes() {
    m_SceneBrushes.Clear();
    m_AllBrushes.Clear();
    m_AllBrushes.UnionWith(m_GuidToBrush.Values);
    m_GuiBrushList = m_GuidToBrush.Values.Where(x => !x.m_HiddenInGui).ToList();
    Resources.UnloadUnusedAssets();
    m_IsLoading = true;
  }
  
}
}  // namespace TiltBrush
